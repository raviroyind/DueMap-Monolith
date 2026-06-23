using System.Text;
using DueMap.Identity.Domain;
using DueMap.Integrations.Notices;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace DueMap.Web.Pages.Account;

[AllowAnonymous]
public sealed class RegisterConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;

    public RegisterConfirmationModel(UserManager<ApplicationUser> users, IEmailSender email)
    {
        _users = users;
        _email = email;
    }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    /// <summary>Banner shown after a resend attempt.</summary>
    public string? Notice { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostResendAsync()
    {
        // Always respond with the same generic message — never reveal whether an
        // address is registered or already confirmed (account-enumeration guard).
        Notice = "If that address still needs confirming, we've sent a fresh link.";

        if (string.IsNullOrWhiteSpace(Email)) return Page();

        var user = await _users.FindByEmailAsync(Email);
        if (user is null || user.EmailConfirmed) return Page();

        try
        {
            var token = await _users.GenerateEmailConfirmationTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmUrl = $"{Request.Scheme}://{Request.Host}/Account/ConfirmEmail?userId={user.Id}&token={encoded}";
            await AccountEmails.SendConfirmationAsync(_email, user.Email!, user.Email!, confirmUrl, HttpContext.RequestAborted);
        }
        catch
        {
            // Best-effort: the generic notice already shown covers a provider hiccup.
        }

        return Page();
    }
}
