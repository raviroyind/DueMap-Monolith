using DueMap.Tenancy.Services;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DueMap.Web.Pages.Tenant;

[AllowAnonymous]
[IgnoreAntiforgeryToken]   // the form is rendered by Blazor; same accepted
                            // trade-off as the PM /Account/Logout page.
public sealed class LogoutModel : PageModel
{
    private readonly ITenantPortalAuth _auth;

    public LogoutModel(ITenantPortalAuth auth) => _auth = auth;

    public IActionResult OnGet() => LocalRedirect("/t/login");

    public async Task<IActionResult> OnPostAsync()
    {
        var cookie = Request.Cookies[TenantContext.CookieName];
        if (!string.IsNullOrEmpty(cookie))
        {
            await _auth.RevokeSessionAsync(cookie, HttpContext.RequestAborted);
        }

        // Clear with the same Path the cookie was set with — otherwise the
        // browser keeps the dm_tenant cookie at /t.
        Response.Cookies.Delete(TenantContext.CookieName, new CookieOptions { Path = "/t" });
        return LocalRedirect("/t/login");
    }
}
