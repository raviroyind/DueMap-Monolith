using System.ComponentModel.DataAnnotations;
using System.Text;
using DueMap.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace DueMap.Web.Pages.Account;

[AllowAnonymous]
public sealed partial class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<ResetPasswordModel> _logger;

    public ResetPasswordModel(UserManager<ApplicationUser> users, ILogger<ResetPasswordModel> logger)
    {
        _users  = users;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>True once the password has actually been changed.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>
    /// Set when the link itself is unusable (missing, malformed, expired, or
    /// already spent). Rendered as a dead-end with a path back to request a
    /// fresh one, rather than a form that cannot possibly succeed.
    /// </summary>
    public bool LinkInvalid { get; private set; }

    public IActionResult OnGet(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            LinkInvalid = true;
            return Page();
        }

        // Carried through the form post; the token is only validated on submit,
        // since that's the point at which we actually change anything.
        Input.UserId = userId;
        Input.Token  = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Input.UserId) || string.IsNullOrEmpty(Input.Token))
        {
            LinkInvalid = true;
            return Page();
        }

        if (!ModelState.IsValid) return Page();

        var user = await _users.FindByIdAsync(Input.UserId);
        if (user is null)
        {
            // Don't distinguish "no such user" from "bad token" — both are just
            // an unusable link from the visitor's point of view.
            LinkInvalid = true;
            return Page();
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Token));
        }
        catch (FormatException)
        {
            LinkInvalid = true;
            return Page();
        }

        var result = await _users.ResetPasswordAsync(user, decodedToken, Input.Password);
        if (result.Succeeded)
        {
            Succeeded = true;
            LogResetCompleted(_logger, user.Id);

            // Deliberately NOT signing them in. Making them enter the new
            // password once confirms they can actually reproduce it, and keeps
            // possession of the mailbox from being enough to hold a session.
            return Page();
        }

        // Identity reports password-policy failures and token failures through
        // the same result. Policy complaints are actionable, so surface those on
        // the form; a bad token means the link is spent and the form is pointless.
        var tokenFailed = result.Errors.Any(e => e.Code == "InvalidToken");
        if (tokenFailed)
        {
            LinkInvalid = true;
            return Page();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }

    public sealed class InputModel
    {
        public string? UserId { get; set; }
        public string? Token { get; set; }

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password),
            ErrorMessage = "Passwords don't match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    [LoggerMessage(EventId = 8104, Level = LogLevel.Information,
        Message = "Password reset completed for user {UserId}.")]
    static partial void LogResetCompleted(ILogger logger, int userId);
}
