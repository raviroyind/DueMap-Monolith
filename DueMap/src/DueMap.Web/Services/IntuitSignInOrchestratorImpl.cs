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
    private readonly IIntuitUserInfoClient _userInfo;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPropertyManagerWriter _pmWriter;
    private readonly IOnboardingProgressService _progress;
    private readonly IDataProtector _stateProtector;
    private readonly ILogger<IntuitSignInOrchestratorImpl> _logger;

    public IntuitSignInOrchestratorImpl(
        IEnumerable<IOAuthProvider> oauthProviders,
        IEnumerable<IAccountingDataClient> clients,
        IAccountingConnectionService connections,
        IIntuitUserInfoClient userInfo,
        UserManager<ApplicationUser> users,
        IPropertyManagerWriter pmWriter,
        IOnboardingProgressService progress,
        IDataProtectionProvider dpProvider,
        ILogger<IntuitSignInOrchestratorImpl> logger)
    {
        _oauthProviders = oauthProviders;
        _clients = clients;
        _connections = connections;
        _userInfo = userInfo;
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

        // 3. Company profile — used for the WORKSPACE name (and as a legacy
        //    identity fallback), never as the primary user identity.
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

        var companyEmail = company?.PrimaryEmail?.Trim();
        var workspaceName = !string.IsNullOrWhiteSpace(company?.CompanyName) ? company!.CompanyName : "My workspace";

        // 3b. Resolve the HUMAN's identity (QA P3). The company's contact
        //     email is shared per-company (the sandbox literally returns
        //     noreply@quickbooks.com), so the signed-in identity must come
        //     from OpenID Connect: the id_token's email claim when present,
        //     else Intuit's userinfo endpoint. Only if BOTH are unavailable
        //     (legacy accounting-only consent) do we degrade to the company
        //     email, which older versions of this flow keyed accounts on.
        var userEmail = TryReadEmailFromIdToken(tokens.IdToken);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            try
            {
                userEmail = (await _userInfo.GetAsync(tokens.AccessToken, ct))?.Email?.Trim();
            }
            catch (Exception ex)
            {
                LogUserInfoFailed(_logger, ex);
            }
        }

        var email = !string.IsNullOrWhiteSpace(userEmail) ? userEmail : companyEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Error("We couldn't read an email for your Intuit account or your QuickBooks company, so we can't sign you in this way. Please sign up with an email and password instead.");
        }
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            LogIdentityFellBackToCompanyEmail(_logger, tokens.RealmId);
        }

        // 4. Find-or-create the user + PM --------------------------------
        var existing = await _users.FindByEmailAsync(email);

        // Legacy upgrade: accounts provisioned by older builds were keyed to
        // the COMPANY email. If the human's email finds no account but the
        // company email does, that row is this person's workspace — rename it
        // to the real identity so the header stops reading
        // "noreply@quickbooks.com" and the account stops being shareable by
        // coincidence.
        if (existing is null
            && !string.IsNullOrWhiteSpace(userEmail)
            && !string.IsNullOrWhiteSpace(companyEmail)
            && !string.Equals(userEmail, companyEmail, StringComparison.OrdinalIgnoreCase))
        {
            var legacy = await _users.FindByEmailAsync(companyEmail);
            if (legacy is not null)
            {
                var setEmail = await _users.SetEmailAsync(legacy, userEmail);
                var setName  = setEmail.Succeeded ? await _users.SetUserNameAsync(legacy, userEmail) : setEmail;
                if (setEmail.Succeeded && setName.Succeeded)
                {
                    existing = legacy;
                    LogLegacyIdentityMigrated(_logger, legacy.Id, companyEmail, userEmail);
                }
                else
                {
                    // e.g. the target email already belongs to another user.
                    // Don't half-rename; continue and provision fresh below.
                    LogLegacyIdentityMigrationSkipped(_logger, legacy.Id,
                        string.Join("; ", setEmail.Errors.Concat(setName.Errors).Select(e => e.Description)));
                }
            }
        }

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
            // The org name is the human-facing identity everywhere (topbar,
            // dashboard chip) — we already fetched CompanyInfo above.
            if (!string.IsNullOrWhiteSpace(company?.CompanyName))
            {
                await _connections.SetCompanyNameAsync(user.PropertyManagerId, company!.CompanyName, ct);
            }
            await _progress.MarkConnectDoneAsync(user.PropertyManagerId, ct);
        }
        catch (Exception ex)
        {
            // Non-fatal: the user is signed-up + signed-in. They'll just be
            // routed through onboarding step 1 like a normal new user.
            // (A RealmMismatchException lands here too — their workspace is
            // bound to a different company; the connections page explains.)
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

    /// <summary>
    /// Best-effort read of the <c>email</c> claim from an OIDC id_token.
    /// Signature is deliberately NOT validated: the token arrived directly
    /// from Intuit's token endpoint over TLS during the code exchange, so the
    /// transport is the trust boundary (same rationale the OIDC spec uses to
    /// make validation optional in the code flow). Null on any shape problem.
    /// </summary>
    private static string? TryReadEmailFromIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()?.Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

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

    [LoggerMessage(EventId = 9109, Level = LogLevel.Warning,
        Message = "Intuit sign-in: userinfo endpoint call failed; falling back to other identity sources.")]
    static partial void LogUserInfoFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 9110, Level = LogLevel.Warning,
        Message = "Intuit sign-in: no OIDC email available (legacy consent?) — identity fell back to the COMPANY email for realm={RealmId}.")]
    static partial void LogIdentityFellBackToCompanyEmail(ILogger logger, string realmId);

    [LoggerMessage(EventId = 9111, Level = LogLevel.Information,
        Message = "Intuit sign-in: migrated legacy account user_id={UserId} from company email {OldEmail} to user email {NewEmail}.")]
    static partial void LogLegacyIdentityMigrated(ILogger logger, int userId, string oldEmail, string newEmail);

    [LoggerMessage(EventId = 9112, Level = LogLevel.Warning,
        Message = "Intuit sign-in: legacy identity migration skipped for user_id={UserId}: {Reason}. Provisioning a fresh account instead.")]
    static partial void LogLegacyIdentityMigrationSkipped(ILogger logger, int userId, string reason);
}
