using System.ComponentModel.DataAnnotations;
using DueMap.Tenancy.Services;
using DueMap.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DueMap.Web.Pages.Tenant;

[AllowAnonymous]
public sealed class SetPasswordModel : PageModel
{
    private readonly ITenantPortalAuth _auth;

    public SetPasswordModel(ITenantPortalAuth auth) => _auth = auth;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool SignedOut { get; set; }

    public sealed class InputModel
    {
        [Required, DataType(DataType.Password), StringLength(200, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password),
         Compare(nameof(NewPassword), ErrorMessage = "Passwords don't match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        // Bouncer: must have a valid session cookie. We re-resolve here rather
        // than via TenantContext (which is a Blazor-scoped service) because
        // Razor Pages run outside the Blazor circuit.
        var ctx = await ResolveSessionAsync();
        SignedOut = ctx is null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var ctx = await ResolveSessionAsync();
        if (ctx is null)
        {
            SignedOut = true;
            return Page();
        }

        try
        {
            await _auth.SetPasswordAsync(ctx.TenantLoginId, Input.NewPassword, HttpContext.RequestAborted);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        return LocalRedirect("/t/profile");
    }

    private async Task<TenantSessionContext?> ResolveSessionAsync()
    {
        var cookie = Request.Cookies[TenantContext.CookieName];
        if (string.IsNullOrEmpty(cookie)) return null;
        return await _auth.GetActiveSessionAsync(cookie, HttpContext.RequestAborted);
    }
}
