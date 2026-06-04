using System.Security.Cryptography;
using DueMap.Integrations.Accounting.OAuth;
using DueMap.Integrations.Persistence;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DueMap.Integrations.Accounting.Services;

/// <summary>
/// Uses <see cref="IDbContextFactory{IntegrationsDbContext}"/> rather than a
/// scoped context. The service is called concurrently from MainLayout's gate
/// check AND from Connections / Home / OnboardingSyncing pages within the same
/// Blazor circuit; a shared scoped DbContext can't survive that race. Each
/// method here creates, uses, and disposes its own short-lived context.
/// </summary>
internal sealed partial class AccountingConnectionService : IAccountingConnectionService
{
    /// <summary>If the access token expires within this window, refresh proactively.</summary>
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);

    private readonly IDbContextFactory<IntegrationsDbContext> _dbFactory;
    private readonly ITokenProtector _protector;
    private readonly IEnumerable<IOAuthProvider> _providers;
    private readonly IPropertyManagerWriter _pmWriter;
    private readonly ILogger<AccountingConnectionService> _logger;

    public AccountingConnectionService(
        IDbContextFactory<IntegrationsDbContext> dbFactory,
        ITokenProtector protector,
        IEnumerable<IOAuthProvider> providers,
        IPropertyManagerWriter pmWriter,
        ILogger<AccountingConnectionService> logger)
    {
        _dbFactory = dbFactory;
        _protector = protector;
        _providers = providers;
        _pmWriter = pmWriter;
        _logger = logger;
    }

    public async Task<ConnectInitiationResult> InitiateAsync(
        int propertyManagerId,
        AccountingProvider provider,
        string redirectUri,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(redirectUri);

        var oauth = ResolveProvider(provider);
        var state = GenerateState();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.OAuthAttempts.Add(new OAuthAttempt
        {
            State = state,
            PropertyManagerId = propertyManagerId,
            Provider = provider,
            RedirectUri = redirectUri,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var url = oauth.BuildAuthorizationUrl(state, redirectUri);
        return new ConnectInitiationResult(url, state);
    }

    public async Task<PmAccountingConnection> CompleteAsync(ConnectionCallbackInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var attempt = await db.OAuthAttempts.FirstOrDefaultAsync(a => a.State == input.State, ct)
            ?? throw new InvalidOperationException("OAuth state does not match any pending attempt.");

        if (attempt.ConsumedAt is not null)
        {
            throw new InvalidOperationException("OAuth state has already been consumed.");
        }
        if (attempt.Provider != input.Provider)
        {
            throw new InvalidOperationException(
                $"OAuth state was issued for {attempt.Provider} but callback hit {input.Provider}.");
        }

        attempt.ConsumedAt = DateTime.UtcNow;

        var oauth = ResolveProvider(input.Provider);
        var tokens = await oauth.ExchangeCodeAsync(input.Code, attempt.RedirectUri, input.RealmId, ct);

        var conn = await db.PmAccountingConnections
            .FirstOrDefaultAsync(c => c.PropertyManagerId == attempt.PropertyManagerId, ct);

        if (conn is null)
        {
            conn = new PmAccountingConnection { PropertyManagerId = attempt.PropertyManagerId };
            db.PmAccountingConnections.Add(conn);
        }

        Hydrate(conn, input.Provider, tokens);
        await db.SaveChangesAsync(ct);

        // Advance onboarding status — Connected is now the minimum. The sweep
        // still won't process this PM until the first sync flips them to Active.
        await _pmWriter.SetMinimumStatusAsync(attempt.PropertyManagerId, OnboardingStatus.Connected, ct);

        LogConnected(_logger, attempt.PropertyManagerId, input.Provider, tokens.RealmId);
        return conn;
    }

    public async Task<PmAccountingConnection> PersistDirectAsync(
        int propertyManagerId,
        AccountingProvider provider,
        OAuthTokens tokens,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var conn = await db.PmAccountingConnections
            .FirstOrDefaultAsync(c => c.PropertyManagerId == propertyManagerId, ct);
        if (conn is null)
        {
            conn = new PmAccountingConnection { PropertyManagerId = propertyManagerId };
            db.PmAccountingConnections.Add(conn);
        }

        Hydrate(conn, provider, tokens);
        await db.SaveChangesAsync(ct);

        // Same status-advance semantics as CompleteAsync — this PM is now
        // at minimum "Connected" since they have tokens on file.
        await _pmWriter.SetMinimumStatusAsync(propertyManagerId, OnboardingStatus.Connected, ct);

        LogConnected(_logger, propertyManagerId, provider, tokens.RealmId);
        return conn;
    }

    public async Task<PmAccountingConnection?> GetActiveAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PmAccountingConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PropertyManagerId == propertyManagerId
                                   && c.Status == ConnectionStatus.Connected, ct);
    }

    public async Task<string?> GetAccessTokenAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var conn = await db.PmAccountingConnections
            .FirstOrDefaultAsync(c => c.PropertyManagerId == propertyManagerId, ct);
        if (conn is null || conn.Status != ConnectionStatus.Connected)
        {
            return null;
        }

        if (conn.AccessTokenExpiresAt - DateTime.UtcNow > RefreshSkew)
        {
            return _protector.Unprotect(conn.AccessTokenProtected);
        }

        // Token is about to expire (or already has) — refresh.
        var oauth = ResolveProvider(conn.Provider);
        var refresh = _protector.Unprotect(conn.RefreshTokenProtected);
        try
        {
            var tokens = await oauth.RefreshAsync(refresh, conn.RealmId, ct);
            Hydrate(conn, conn.Provider, tokens);
            await db.SaveChangesAsync(ct);
            return tokens.AccessToken;
        }
        catch (Exception ex)
        {
            conn.Status = ConnectionStatus.TokenExpired;
            conn.LastSyncError = ex.Message;
            conn.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            LogRefreshFailed(_logger, ex, propertyManagerId);
            return null;
        }
    }

    public async Task DisconnectAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var conn = await db.PmAccountingConnections
            .FirstOrDefaultAsync(c => c.PropertyManagerId == propertyManagerId, ct);
        if (conn is null) return;
        conn.Status = ConnectionStatus.Disconnected;
        conn.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private void Hydrate(PmAccountingConnection conn, AccountingProvider provider, OAuthTokens tokens)
    {
        var now = DateTime.UtcNow;
        conn.Provider                = provider;
        conn.RealmId                 = tokens.RealmId;
        conn.AccessTokenProtected    = _protector.Protect(tokens.AccessToken);
        conn.RefreshTokenProtected   = _protector.Protect(tokens.RefreshToken);
        conn.AccessTokenExpiresAt    = tokens.AccessTokenExpiresAt;
        conn.RefreshTokenExpiresAt   = tokens.RefreshTokenExpiresAt;
        conn.Scopes                  = tokens.Scopes;
        conn.Status                  = ConnectionStatus.Connected;
        conn.LastSyncError           = null;
        conn.UpdatedAt               = now;
        if (conn.ConnectedAt == default) conn.ConnectedAt = now;
    }

    private IOAuthProvider ResolveProvider(AccountingProvider provider) =>
        _providers.FirstOrDefault(p => p.Provider == provider)
            ?? throw new InvalidOperationException($"No OAuth provider registered for {provider}.");

    private static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information,
        Message = "Accounting connection established: pm={PropertyManagerId} provider={Provider} realm={RealmId}")]
    static partial void LogConnected(ILogger logger, int propertyManagerId, AccountingProvider provider, string realmId);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning,
        Message = "Token refresh failed for pm={PropertyManagerId}; connection marked TokenExpired")]
    static partial void LogRefreshFailed(ILogger logger, Exception ex, int propertyManagerId);
}
