using System.Text;
using DueMap.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace DueMap.Web.Pages.Account;

[AllowAnonymous]
public sealed class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public ConfirmEmailModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    /// <summary>True once the address is confirmed (or was already confirmed).</summary>
    public bool Confirmed { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return Page();   // Confirmed stays false -> the page shows the error card.
        }

        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            // Don't reveal whether the id exists; just show the generic failure.
            return Page();
        }

        // Idempotent: a second click on the same link (already confirmed) is a success.
        if (user.EmailConfirmed)
        {
            Confirmed = true;
            return Page();
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return Page();   // Malformed token in the query string.
        }

        var result = await _users.ConfirmEmailAsync(user, decodedToken);
        Confirmed = result.Succeeded;

        if (Confirmed)
        {
            // Account is now active — sign them in so clicking the link lands
            // them straight in the app (onboarding kicks in from "/").
            await _signIn.SignInAsync(user, isPersistent: false);
        }

        return Page();
    }
}
