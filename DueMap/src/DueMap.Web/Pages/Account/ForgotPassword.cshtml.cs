using System.ComponentModel.DataAnnotations;
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
public sealed partial class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> users,
        IEmailSender email,
        ILogger<ForgotPasswordModel> logger)
    {
        _users  = users;
        _email  = email;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Set once a request has been accepted; flips the page to the confirmation state.</summary>
    public bool Submitted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Account-enumeration guard: every branch below lands on the same
        // confirmation screen. Whether the address is unknown, unconfirmed, or
        // a valid account, the visitor sees identical output. The only thing
        // that varies is whether an email actually goes out.
        Submitted = true;

        var user = await _users.FindByEmailAsync(Input.Email);

        if (user is null)
        {
            LogResetForUnknownAddress(_logger);
            return Page();
        }

        // An unconfirmed account can't receive a reset — the address hasn't been
        // proven to belong to them, so mailing a password-changing link to it
        // would let an impostor who typed someone else's address take the account.
        if (!user.EmailConfirmed)
        {
            LogResetForUnconfirmedUser(_logger, user.Id);
            return Page();
        }

        try
        {
            var token   = await _users.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetUrl =
                $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?userId={user.Id}&token={encoded}";

            await AccountEmails.SendPasswordResetAsync(
                _email, user.Email!, user.Email!, resetUrl, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Best-effort by design: the generic confirmation is already showing,
            // and telling the visitor "sending failed" would leak that the
            // address exists. Log it so the failure is still visible to us.
            LogResetEmailFailed(_logger, ex, user.Id);
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    [LoggerMessage(EventId = 8101, Level = LogLevel.Information,
        Message = "Password reset requested for an address with no account — generic confirmation shown.")]
    static partial void LogResetForUnknownAddress(ILogger logger);

    [LoggerMessage(EventId = 8102, Level = LogLevel.Information,
        Message = "Password reset requested for unconfirmed user {UserId} — no mail sent; the address isn't proven yet.")]
    static partial void LogResetForUnconfirmedUser(ILogger logger, int userId);

    [LoggerMessage(EventId = 8103, Level = LogLevel.Error,
        Message = "Password-reset email failed to send for user {UserId} — the visitor still saw the generic confirmation.")]
    static partial void LogResetEmailFailed(ILogger logger, Exception ex, int userId);
}
