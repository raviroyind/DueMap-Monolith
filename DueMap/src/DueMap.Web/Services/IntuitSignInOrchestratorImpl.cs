using System.Security.Cryptography;
using System.Text.Json;
using DueMap.Identity.Domain;
using DueMap.Integrations.Accounting;
using DueMap.Integrations.Accounting.OAuth;
using DueMap.Integrations.Accounting.Sync;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DueMap.Web.Services;

internal sealed partial class IntuitSignInOrchestratorImpl : IIntuitSignInOrchestrator
{
    // Lifetime of the signed state token. Long enough to cover a user that
    // started the flow, took 5 minutes on the Intuit consent screen, and
    // came back — short enough that a leaked state can't be replayed days later.
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private readonly IEnumerable<IOAuthProvider> _oauthProviders;
    private readonly IEnumerable<IAccountingDataClient> _clients;
    private readonly IAccountingConnectionService _connections;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPropertyManagerWriter _pmWriter;
    private readonly IOnboardingProgressService _progress;
    private readonly IDataProtector _stateProtector;
    private readonly ILogger<IntuitSignInOrchestratorImpl> _logger;

    public IntuitSignInOrchestratorImpl(
        IEnumerable<IOAuthProvider> oauthProviders,
        IEnumerable<IAccountingDataClient> clients,
        IAccountingConnectionService connections,
        UserManager<ApplicationUser> users,
        IPropertyManagerWriter pmWriter,
        IOnboardingProgressService progress,
        IDataProtectionProvider dpProvider,
        ILogger<IntuitSignInOrchestratorImpl> logger)
    {
        _oauthProviders = oauthProviders;
        _clients = clients;
        _connections = connections;
        _users = users;
        _pmWriter = pmWriter;
        _progress = progress;
        _stateProtector = dpProvider.CreateProtector("DueMap.IntuitSignIn.v1");
        _logger = logger;
    }

    // ---------------------------------------------------------------------
    // Build the authorize URL
    // ---------------------------------------------------------------------

    public string BuildSignInUrl(string callbackUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);

        // Nonce defeats CSRF — Intuit echoes state back, we verify it's one
        // we issued and hasn't expired.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var payload = JsonSerializer.Serialize(new StateClaims(nonce, DateTime.UtcNow));
        var state = _stateProtector.Protect(payload);

        var provider = ResolveQbProvider();
        return provider.BuildAuthorizationUrl(state, callbackUrl);
    }

    // ---------------------------------------------------------------------
    // Callback — exchange + identify + provision
    // ---------------------------------------------------------------------

    public async Task<IntuitSignInResult> HandleCallbackAsync(
        string code, string state, string realmId, string callbackUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))   return Error("Intuit didn't return an authorization code.");
        if (string.IsNullOrWhiteSpace(state))  return Error("Intuit didn't return a state token.");
        if (string.IsNullOrWhiteSpace(realmId)) return Error("Intuit didn't return a realm id — your app isn't returning accounting context.");

        // 1. Verify state -------------------------------------------------
        StateClaims claims;
        try
        {
            var payload = _stateProtector.Unprotect(state);
            claims = JsonSerializer.Deserialize<StateClaims>(payload)
                ?? throw new InvalidDataException("State payload was empty.");
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or InvalidDataException)
        {
            LogStateRejected(_logger, ex);
            return Error("Sign-in token was unrecognized. Please try again.");
        }
        if (DateTime.UtcNow - claims.IssuedAt > StateLifetime)
        {
            return Error("Sign-in token expired. Please try again.");
        }

        // 2. Exchange the code for tokens --------------------------------
        OAuthTokens tokens;
        try
        {
            var provider = ResolveQbProvider();
            tokens = await provider.ExchangeCodeAsync(code, callbackUrl, realmId, ct);
        }
        catch (Exception ex)
        {
            LogTokenExchangeFailed(_logger, ex);
            return Error("Could not exchange the authorization code with Intuit. Please try again.");
        }

        // 3. Resolve identity via CompanyInfo ----------------------------
        AccountingCompanyInfo? company;
        try
        {
            var client = ResolveQbClient();
            company = await client.GetCompanyInfoAsync(tokens.AccessToken, tokens.RealmId, ct);
        }
        catch (Exception ex)
        {
            LogCompanyInfoFailed(_logger, ex);
            return Error("Connected to QuickBooks but couldn't read your company profile. Please try again or sign up with email.");
        }

        var email = company?.PrimaryEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            // The sandbox demo company always has an email; production
            // companies usually do too. If it's missing we don't have any
            // way to identify the user — fall back to email sign-up.
            return Error("Your QuickBooks company doesn't have a contact email on file, so we can't sign you in this way. Please sign up with an email and password instead.");
        }
        var workspaceName = !string.IsNullOrWhiteSpace(company!.CompanyName) ? company.CompanyName : "My workspace";

        // 4. Find-or-create the user + PM --------------------------------
        var existing = await _users.FindByEmailAsync(email);
        bool isNewUser;
        ApplicationUser user;

        if (existing is not null)
        {
            user = existing;
            isNewUser = false;
            LogSignInExisting(_logger, user.Id, user.PropertyManagerId);
        }
        else
        {
            // First-time PM. Provision the workspace + identity in lock-step
            // with the accounting connection so the very first dashboard
            // hit is fully usable.
            PropertyManager pm;
            try
            {
                pm = await _pmWriter.CreateAsync(workspaceName, ct);
            }
            catch (Exception ex)
            {
                LogPmCreateFailed(_logger, ex, email);
                return Error("Couldn't set up your workspace. Please try again.");
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                PropertyManagerId = pm.Id,
                CreatedAt = DateTime.UtcNow
            };
            var create = await _users.CreateAsync(user);
            if (!create.Succeeded)
            {
                // Compensate the PM row — same pattern as Register.cshtml.cs.
                try { await _pmWriter.DeleteAsync(pm.Id, ct); }
                catch (Exception delEx) { LogPmCleanupFailed(_logger, delEx, pm.Id); }
                return Error(string.Join("; ", create.Errors.Select(e => e.Description)));
            }
            isNewUser = true;
            LogSignInProvisioned(_logger, user.Id, pm.Id, email);
        }

        // 5. Persist the accounting connection (closes onboarding step 1) -
        try
        {
            await _connections.PersistDirectAsync(user.PropertyManagerId, AccountingProvider.QuickBooks, tokens, ct);
            await _progress.MarkConnectDoneAsync(user.PropertyManagerId, ct);
        }
        catch (Exception ex)
        {
            // Non-fatal: the user is signed-up + signed-in. They'll just be
            // routed through onboarding step 1 like a normal new user.
            LogConnectionPersistFailed(_logger, ex, user.PropertyManagerId);
        }

        return new IntuitSignInResult(user, isNewUser, null);
    }

    // ---------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------

    private IOAuthProvider ResolveQbProvider() =>
        _oauthProviders.FirstOrDefault(p => p.Provider == AccountingProvider.QuickBooks)
        ?? throw new InvalidOperationException("QuickBooks OAuth provider is not registered.");

    private IAccountingDataClient ResolveQbClient() =>
        _clients.FirstOrDefault(c => c.Provider == AccountingProvider.QuickBooks)
        ?? throw new InvalidOperationException("QuickBooks accounting client is not registered.");

    private static IntuitSignInResult Error(string message) =>
        new(User: null, IsNewUser: false, Error: message);

    /// <summary>State payload encrypted into the OAuth <c>state</c> param.</summary>
    private sealed record StateClaims(string Nonce, DateTime IssuedAt);

    [LoggerMessage(EventId = 9101, Level = LogLevel.Warning,
        Message = "Intuit sign-in: state token failed to unprotect.")]
    static partial void LogStateRejected(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Warning,
        Message = "Intuit sign-in: code-for-tokens exchange failed.")]
    static partial void LogTokenExchangeFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 9103, Level = LogLevel.Warning,
        Message = "Intuit sign-in: CompanyInfo fetch failed.")]
    static partial void LogCompanyInfoFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 9104, Level = LogLevel.Error,
        Message = "Intuit sign-in: PM provisioning failed for email={Email}.")]
    static partial void LogPmCreateFailed(ILogger logger, Exception ex, string email);

    [LoggerMessage(EventId = 9105, Level = LogLevel.Warning,
        Message = "Intuit sign-in: compensating PM delete failed for orphan pm_id={PropertyManagerId}.")]
    static partial void LogPmCleanupFailed(ILogger logger, Exception ex, int propertyManagerId);

    [LoggerMessage(EventId = 9106, Level = LogLevel.Information,
        Message = "Intuit sign-in: existing user signed in user_id={UserId} pm_id={PropertyManagerId}")]
    static partial void LogSignInExisting(ILogger logger, int userId, int propertyManagerId);

    [LoggerMessage(EventId = 9107, Level = LogLevel.Information,
        Message = "Intuit sign-in: provisioned new user user_id={UserId} pm_id={PropertyManagerId} email={Email}")]
    static partial void LogSignInProvisioned(ILogger logger, int userId, int propertyManagerId, string email);

    [LoggerMessage(EventId = 9108, Level = LogLevel.Warning,
        Message = "Intuit sign-in: accounting connection persist failed for pm_id={PropertyManagerId}. User is signed-in; onboarding will re-prompt to connect.")]
    static partial void LogConnectionPersistFailed(ILogger logger, Exception ex, int propertyManagerId);
}
