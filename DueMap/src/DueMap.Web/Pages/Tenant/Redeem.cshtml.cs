using DueMap.Tenancy.Services;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DueMap.Web.Pages.Tenant;

[AllowAnonymous]
public sealed class RedeemModel : PageModel
{
    private readonly ITenantPortalAuth _auth;

    public RedeemModel(ITenantPortalAuth auth) => _auth = auth;

    public async Task<IActionResult> OnGetAsync(string token)
    {
        var ct = HttpContext.RequestAborted;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var session = await _auth.RedeemMagicLinkAsync(token, ip, ct);
        if (session is null)
        {
            // Render the page body (expired / used / unknown error message).
            return Page();
        }

        Response.Cookies.Append(TenantContext.CookieName, session.RawCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(session.ExpiresAt, TimeSpan.Zero),
            Path = "/t",
            IsEssential = true
        });

        return LocalRedirect("/t");
    }
}
