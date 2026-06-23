using System.ComponentModel.DataAnnotations;
using System.Text;
using DueMap.Identity.Domain;
using DueMap.Integrations.Notices;
using DueMap.Tenancy;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace DueMap.Web.Pages.Account;

[AllowAnonymous]
public sealed partial class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IPropertyManagerWriter _pmWriter;
    private readonly SsoProviderRegistry _sso;
    private readonly IEmailSender _email;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IPropertyManagerWriter pmWriter,
        SsoProviderRegistry sso,
        IEmailSender email,
        ILogger<RegisterModel> logger)
    {
        _users = users;
        _signIn = signIn;
        _pmWriter = pmWriter;
        _sso = sso;
        _email = email;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        // Organization name removed from the sign-up form (Nov 2026).
        // We now collect zero workspace metadata up-front and instead pull the
        // real company name from QuickBooks / Xero during onboarding step 1.
        // The PM row gets a placeholder name derived from the email until then.

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password),
            ErrorMessage = "Passwords don't match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var ct = HttpContext.RequestAborted;

        // Step 1: materialize the PM org. The user FK depends on this row.
        // We don't ask the user for the company name any more — derive a
        // readable placeholder from the email so the sidebar shows something
        // friendly until the QuickBooks/Xero sync overwrites it with the
        // real CompanyName (task #61).
        var placeholderName = DeriveTemporaryName(Input.Email);
        var pm = await _pmWriter.CreateAsync(placeholderName, ct);

        // Step 2: create the user. If this fails, the PM row from step 1 is
        // orphaned — compensate by deleting it. We can't wrap both steps in a
        // single EF transaction because they live on different DbContexts;
        // TransactionScope across contexts would force MSDTC enlistment. The
        // compensating delete keeps the registration atomic from the user's
        // perspective without the infrastructure tax.
        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            PropertyManagerId = pm.Id,
            CreatedAt = DateTime.UtcNow
        };

        IdentityResult result;
        try
        {
            result = await _users.CreateAsync(user, Input.Password);
        }
        catch (Exception ex)
        {
            // CreateAsync itself threw (e.g. DB error). Compensate then rethrow
            // — this is a 500-class failure that callers shouldn't paper over.
            await TryCleanupPmAsync(pm.Id, ct);
            LogUserCreateThrew(_logger, ex, pm.Id);
            throw;
        }

        if (!result.Succeeded)
        {
            await TryCleanupPmAsync(pm.Id, ct);
            ErrorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
            return Page();
        }

        // The account is created but INACTIVE (EmailConfirmed=false). We do NOT
        // sign the user in — they must click the confirmation link first
        // (SignIn.RequireConfirmedAccount blocks password login until then).
        var confirmUrl = await BuildConfirmationUrlAsync(user);

        // Confirmation email — best-effort send, but always route the user to the
        // "check your email" page. If SendGrid is down they can hit Resend there.
        // The dev fallback in SendGridEmailSender logs the link so local dev works
        // with no real SendGrid setup.
        try { await AccountEmails.SendConfirmationAsync(_email, user.Email!, placeholderName, confirmUrl, ct); }
        catch (Exception ex) { LogConfirmationFailed(_logger, ex, pm.Id); }

        return RedirectToPage("RegisterConfirmation", new { email = Input.Email });
    }

    // Generates a single-use email-confirmation token and packs it (URL-safe
    // base64, since the raw token contains characters that don't survive a query
    // string) into a link to the ConfirmEmail page.
    private async Task<string> BuildConfirmationUrlAsync(ApplicationUser user)
    {
        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return $"{Request.Scheme}://{Request.Host}/Account/ConfirmEmail?userId={user.Id}&token={encoded}";
    }


    // Sign-up via Intuit / Xero. Same shape as Login.OnPostExternalSignIn —
    // the callback page distinguishes new vs returning users by looking up
    // AspNetUserLogins, so the two POSTs converge to the same OIDC challenge.
    public IActionResult OnPostExternalSignUp(string provider)
    {
        if (!_sso.IsEnabled(provider))
        {
            TempData["SsoNotice"] = provider switch
            {
                "intuit" => "Sign up with Intuit isn't configured on this server yet. " +
                            "Please create an account with email and password.",
                "xero"   => "Sign up with Xero isn't configured on this server yet. " +
                            "Please create an account with email and password.",
                _        => "Single sign-on isn't configured on this server yet."
            };
            return RedirectToPage();
        }

        // Intuit pivot — see Login.cshtml.cs OnPostExternalSignIn for rationale.
        // Sign-up and sign-in both flow through /oauth/intuit/signin; the
        // orchestrator's identify-or-provision logic handles both cases.
        if (string.Equals(provider, "intuit", StringComparison.OrdinalIgnoreCase))
        {
            return LocalRedirect("/oauth/intuit/signin");
        }

        var scheme = _sso.ToSchemeName(provider);
        var redirectUrl = Url.Page("/Account/ExternalLoginCallback");
        var properties = _signIn.ConfigureExternalAuthenticationProperties(scheme, redirectUrl);
        return Challenge(properties, scheme);
    }

    private async Task TryCleanupPmAsync(int pmId, CancellationToken ct)
    {
        try
        {
            await _pmWriter.DeleteAsync(pmId, ct);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup. If this throws, an orphan PM remains;
            // an admin sweep can pick it up. Log loudly so it doesn't pass silently.
            LogPmCleanupFailed(_logger, ex, pmId);
        }
    }

    // Local-part separators we split on to title-case the email into a name.
    // static readonly so CA1861 doesn't flag the literal each call.
    private static readonly char[] NameSeparators = { '.', '_', '-', '+' };

    // Turns "jane.doe@example.com" → "Jane Doe", "carlos_r@x.com" → "Carlos R".
    // Falls back to "My workspace" for degenerate emails ("@example.com" etc).
    // Keep this stable — it's the displayed PM name until accounting sync runs.
    private static string DeriveTemporaryName(string email)
    {
        var local = (email ?? string.Empty).Split('@')[0];
        if (string.IsNullOrWhiteSpace(local)) return "My workspace";
        var parts = local
            .Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 0)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant());
        var joined = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(joined) ? "My workspace" : joined;
    }

    [LoggerMessage(EventId = 8001, Level = LogLevel.Warning,
        Message = "Compensating PM delete failed after user creation error. Orphan PM id={PropertyManagerId}")]
    static partial void LogPmCleanupFailed(ILogger logger, Exception ex, int propertyManagerId);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error,
        Message = "UserManager.CreateAsync threw during registration. Attempting compensating delete of PM id={PropertyManagerId}")]
    static partial void LogUserCreateThrew(ILogger logger, Exception ex, int propertyManagerId);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Warning,
        Message = "Confirmation email failed to send for newly-registered PM id={PropertyManagerId} — user routed to the check-your-email page where they can resend.")]
    static partial void LogConfirmationFailed(ILogger logger, Exception ex, int propertyManagerId);
}
