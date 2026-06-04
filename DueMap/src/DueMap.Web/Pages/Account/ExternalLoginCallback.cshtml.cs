using System.Security.Claims;
using DueMap.Identity.Domain;
using DueMap.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace DueMap.Web.Pages.Account;

/// <summary>
/// Lands here AFTER the OIDC middleware has authenticated the user against
/// Intuit or Xero. Responsibilities, in order:
///   1. Read the ExternalLoginInfo (provider + sub + claims) the middleware
///      stashed in the external-cookie scheme.
///   2. Already-linked? → sign in, redirect to returnUrl or "/".
///   3. Not linked, but a user exists with the IdP-verified email? → link
///      via UserManager.AddLoginAsync, sign in, redirect.
///   4. Brand-new user? → create PropertyManager + ApplicationUser, link, sign
///      in, redirect to /connections so they can wire QBO/Xero data next.
///
/// Failure modes are translated to TempData notices on /Account/Login rather
/// than 500s — IdP hiccups should not break the auth experience.
/// </summary>
[AllowAnonymous]
public sealed partial class ExternalLoginCallbackModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPropertyManagerWriter _pmWriter;
    private readonly ILogger<ExternalLoginCallbackModel> _logger;

    public ExternalLoginCallbackModel(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users,
        IPropertyManagerWriter pmWriter,
        ILogger<ExternalLoginCallbackModel> logger)
    {
        _signIn = signIn;
        _users = users;
        _pmWriter = pmWriter;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            return BackToLogin($"The provider returned an error: {remoteError}");
        }

        var info = await _signIn.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return BackToLogin("Sign-in handshake didn't complete. Please try again.");
        }

        // (1) Already linked: existing AspNetUserLogins row.
        var existing = await _signIn.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (existing.Succeeded)
        {
            LogSsoSignIn(_logger, info.LoginProvider, info.ProviderKey);
            return LocalRedirectSafe(returnUrl, fallback: "/");
        }
        if (existing.IsLockedOut)
        {
            return BackToLogin("Account locked. Try again later.");
        }

        // (2) Not linked, but matching email already exists locally? Auto-link
        // (only when the IdP marked the email verified — both Intuit and Xero
        // return verified emails for confirmed accounts).
        var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                  ?? info.Principal.FindFirstValue("email");
        var emailVerifiedRaw = info.Principal.FindFirstValue("email_verified");
        var emailVerified = string.Equals(emailVerifiedRaw, "true", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(email))
        {
            var match = await _users.FindByEmailAsync(email);
            if (match is not null)
            {
                if (!emailVerified)
                {
                    return BackToLogin(
                        $"An account already exists for {email}, but {info.LoginProvider} did not " +
                        "confirm the email. Sign in with your password, then link this provider from Settings.");
                }

                var link = await _users.AddLoginAsync(match, info);
                if (!link.Succeeded)
                {
                    return BackToLogin($"Could not link your {info.LoginProvider} account: " +
                        string.Join("; ", link.Errors.Select(e => e.Description)));
                }
                await _signIn.SignInAsync(match, isPersistent: false);
                LogSsoLinkedExisting(_logger, info.LoginProvider, match.Id);
                return LocalRedirectSafe(returnUrl, fallback: "/");
            }
        }

        // (3) Brand new: provision PM + ApplicationUser, link external login.
        return await ProvisionAndSignInAsync(info, email);
    }

    private async Task<IActionResult> ProvisionAndSignInAsync(ExternalLoginInfo info, string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            // Both Intuit and Xero include email in the OIDC userinfo response
            // when the email scope is granted. If it's missing, we can't
            // provision — the user must use email/password registration.
            return BackToLogin(
                $"{info.LoginProvider} did not share an email address. " +
                "Please use email and password to create your account.");
        }

        var ct = HttpContext.RequestAborted;

        // Best-effort organization name: prefer 'name' claim, then full name
        // assembled from given/family, then email-local-part as last resort.
        var name = info.Principal.FindFirstValue(ClaimTypes.Name)
                ?? info.Principal.FindFirstValue("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            var given = info.Principal.FindFirstValue(ClaimTypes.GivenName)
                     ?? info.Principal.FindFirstValue("given_name");
            var family = info.Principal.FindFirstValue(ClaimTypes.Surname)
                      ?? info.Principal.FindFirstValue("family_name");
            name = $"{given} {family}".Trim();
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            name = email.Split('@')[0];
        }

        var pm = await _pmWriter.CreateAsync(name!, ct);

        var user = new ApplicationUser
        {
            UserName          = email,
            Email             = email,
            EmailConfirmed    = true,   // IdP confirmed it for us
            PropertyManagerId = pm.Id,
            CreatedAt         = DateTime.UtcNow
        };

        var create = await _users.CreateAsync(user);
        if (!create.Succeeded)
        {
            await TryCleanupPmAsync(pm.Id, ct);
            return BackToLogin("Could not create your account: " +
                string.Join("; ", create.Errors.Select(e => e.Description)));
        }

        var link = await _users.AddLoginAsync(user, info);
        if (!link.Succeeded)
        {
            // We could delete the just-created user here too, but a half-linked
            // user is recoverable — they can sign in with the same SSO and we'd
            // hit the "matching email" branch above. Log and continue.
            LogLinkFailedAfterCreate(_logger, info.LoginProvider, user.Id);
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        LogSsoProvisioned(_logger, info.LoginProvider, user.Id, pm.Id);

        // New users go to /connections so they can wire up their accounting
        // data with a single click. Wave 2 will short-circuit this if the
        // bundled accounting scope was already granted.
        return LocalRedirect("/connections");
    }

    private async Task TryCleanupPmAsync(int pmId, CancellationToken ct)
    {
        try { await _pmWriter.DeleteAsync(pmId, ct); }
        catch (Exception ex) { LogPmCleanupFailed(_logger, ex, pmId); }
    }

    private RedirectToPageResult BackToLogin(string message)
    {
        TempData["SsoNotice"] = message;
        return RedirectToPage("/Account/Login");
    }

    private LocalRedirectResult LocalRedirectSafe(string? returnUrl, string fallback) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect(fallback);

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information,
        Message = "SSO sign-in: provider={Provider} key={Key}")]
    static partial void LogSsoSignIn(ILogger logger, string provider, string key);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information,
        Message = "SSO link to existing user: provider={Provider} userId={UserId}")]
    static partial void LogSsoLinkedExisting(ILogger logger, string provider, int userId);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Information,
        Message = "SSO provisioned new account: provider={Provider} userId={UserId} pmId={PropertyManagerId}")]
    static partial void LogSsoProvisioned(ILogger logger, string provider, int userId, int propertyManagerId);

    [LoggerMessage(EventId = 9004, Level = LogLevel.Warning,
        Message = "AddLoginAsync failed AFTER user creation succeeded. Recoverable on next sign-in. provider={Provider} userId={UserId}")]
    static partial void LogLinkFailedAfterCreate(ILogger logger, string provider, int userId);

    [LoggerMessage(EventId = 9005, Level = LogLevel.Warning,
        Message = "Compensating PM delete failed after user-create error. Orphan pm id={PropertyManagerId}")]
    static partial void LogPmCleanupFailed(ILogger logger, Exception ex, int propertyManagerId);
}
