using DueMap.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DueMap.Web.Pages.Account;

// Antiforgery is disabled on this page because the Sign-out form lives inside
// a Blazor InteractiveServer layout (MainLayout.razor), which doesn't run the
// AspNetCore tag helper that auto-injects __RequestVerificationToken. The
// trade-off is acceptable for logout specifically: a CSRF-forged sign-out only
// annoys the user (no data write, no privilege escalation), and SameSite=Lax
// on the auth cookie already blocks the cross-site case in modern browsers.
[IgnoreAntiforgeryToken]
public sealed class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;

    public LogoutModel(SignInManager<ApplicationUser> signIn)
    {
        _signIn = signIn;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signIn.SignOutAsync();
        return LocalRedirect("/Account/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Account/Login");
}
