using System.ComponentModel.DataAnnotations;
using DueMap.Integrations.Notices;
using DueMap.Tenancy.Services;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DueMap.Web.Pages.Tenant;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly ITenantPortalAuth _auth;
    private readonly IEmailSender _email;

    public LoginModel(ITenantPortalAuth auth, IEmailSender email)
    {
        _auth = auth;
        _email = email;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? Confirmation { get; set; }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Optional — empty means "send me a magic link". When non-empty we
        // try the password path first; falling through silently to magic
        // link on bad password would leak which path is in use, so we just
        // error generically.
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var ct = HttpContext.RequestAborted;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // ---- Password path ----
        if (!string.IsNullOrEmpty(Input.Password))
        {
            var session = await _auth.SignInWithPasswordAsync(Input.Email, Input.Password, ip, ct);
            if (session is null)
            {
                // Generic message — never reveal whether the email exists.
                ErrorMessage = "Email or password didn't match. Try again, or leave the password blank to email yourself a sign-in link.";
                return Page();
            }
            SetSessionCookie(session.RawCookie, session.ExpiresAt);
            return LocalRedirect("/t");
        }

        // ---- Magic link path ----
        var issued = await _auth.RequestMagicLinkAsync(Input.Email, ip, ct);
        if (issued is not null)
        {
            await SendMagicLinkEmailAsync(issued, ct);
        }

        // Constant confirmation regardless of whether the email matched.
        // Prevents account enumeration ("does jane@x.com have an account here?").
        Confirmation = $"If {Input.Email} matches a tenant on file, a sign-in link is on its way. " +
                       "It expires in 15 minutes — check your inbox (and spam folder just in case).";
        return Page();
    }

    private void SetSessionCookie(string rawCookie, DateTime expiresAt)
    {
        // HttpOnly + Secure + SameSite=Lax. Path scoped to /t so the tenant
        // cookie never reaches the PM admin app.
        Response.Cookies.Append(TenantContext.CookieName, rawCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero),
            Path = "/t",
            IsEssential = true
        });
    }

    private async Task SendMagicLinkEmailAsync(MagicLinkIssued issued, CancellationToken ct)
    {
        var link = $"{Request.Scheme}://{Request.Host}/t/login/{issued.RawToken}";

        var branded = TransactionalEmailBuilder.Build(new TransactionalEmailRequest(
            Subject: "Your tenant-portal sign-in link",
            Headline: "Sign in to your tenant portal",
            IntroParagraph:
                "Click the button below to sign in. This link expires in <strong>15 minutes</strong> " +
                "and can only be used once.",
            AdditionalParagraphs: Array.Empty<string>(),
            PrimaryCtaLabel: "Sign in",
            PrimaryCtaUrl: link,
            FooterNote: "If you didn't request this, you can safely ignore the email — your account stays untouched.",
            // The portal is co-branded as the PM's product. We don't have the
            // PM workspace name in this scope yet — the auth service returns
            // only the tenant_login. Plumbing it through is a small follow-up;
            // for now the header reads "DueMap" which is acceptable for a
            // sign-in email.
            WorkspaceName: null,
            Preheader: "Single-use sign-in link, valid for 15 minutes."));

        // Fire-and-forget against transient SendGrid failures: a delivery error
        // here would otherwise look like "I never got the email" to the tenant.
        // The confirmation page is the same either way (anti-enumeration).
        try
        {
            await _email.SendAsync(new DispatchRequest(
                Channel: DispatchChannel.Email,
                To: issued.Email,
                ToDisplayName: null,
                Subject: branded.Subject,
                BodyHtml: branded.BodyHtml,
                BodyText: branded.BodyText), ct);
        }
        catch (Exception)
        {
            // Swallowed by design — see comment above.
        }
    }
}
