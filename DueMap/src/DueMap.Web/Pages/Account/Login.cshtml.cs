using System.ComponentModel.DataAnnotations;
using DueMap.Identity.Domain;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DueMap.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly SsoProviderRegistry _sso;

    public LoginModel(SignInManager<ApplicationUser> signIn, SsoProviderRegistry sso)
    {
        _signIn = signIn;
        _sso = sso;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        return Page();
    }

    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        var result = await _signIn.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }
        if (result.IsLockedOut)
        {
            ErrorMessage = "Account locked. Try again later.";
            return Page();
        }

        ErrorMessage = "Invalid email or password.";
        return Page();
    }

    // Sign-in via Intuit / Xero. The brand buttons post here. If the OIDC
    // scheme isn't registered (Wave 1 ships unconfigured by default — secrets
    // come via `dotnet user-secrets`), we fall back to a friendly notice so
    // the user is never stranded.
    public IActionResult OnPostExternalSignIn(string provider, string? returnUrl = null)
    {
        if (!_sso.IsEnabled(provider))
        {
            TempData["SsoNotice"] = provider switch
            {
                "intuit" => "Sign in with Intuit isn't configured on this server yet. " +
                            "Please use email and password to sign in.",
                "xero"   => "Sign in with Xero isn't configured on this server yet. " +
                            "Please use email and password to sign in.",
                _        => "Single sign-on isn't configured on this server yet."
            };
            return RedirectToPage();
        }

        // Intuit pivot: the OIDC Challenge path fails because most sandbox apps
        // aren't approved for "Sign in with Intuit" identity flow — Intuit
        // returns access_token without id_token, blowing up id_token validation
        // (IDX10000). Route the button to our QBO-OAuth-for-identity orchestrator
        // instead, which works against any Connect-to-QuickBooks app and also
        // bundles the accounting consent into sign-in (closes task #59 partially).
        if (string.Equals(provider, "intuit", StringComparison.OrdinalIgnoreCase))
        {
            return LocalRedirect("/oauth/intuit/signin");
        }

        var scheme = _sso.ToSchemeName(provider);
        var redirectUrl = Url.Page("/Account/ExternalLoginCallback", pageHandler: null,
            values: new { returnUrl });
        var properties = _signIn.ConfigureExternalAuthenticationProperties(scheme, redirectUrl);
        return Challenge(properties, scheme);
    }
}
