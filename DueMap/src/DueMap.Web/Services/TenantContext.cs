using DueMap.Tenancy.Services;
using Microsoft.AspNetCore.Http;

namespace DueMap.Web.Services;

/// <summary>
/// Resolves "who is the current tenant?" from the portal session cookie.
/// Mirrors PmContext's shape so pages can inject + await a single async
/// accessor without thinking about the underlying cookie / DB lookup.
///
/// Scoped — one instance per Blazor circuit / HTTP request. The first call
/// hits the DB; subsequent calls in the same scope return the cached value.
/// </summary>
public sealed class TenantContext
{
    /// <summary>Cookie name the portal sets after a successful sign-in.</summary>
    public const string CookieName = "dm_tenant";

    private readonly ITenantPortalAuth _auth;
    private readonly IHttpContextAccessor _http;
    private TenantSessionContext? _cached;
    private bool _resolved;

    public TenantContext(ITenantPortalAuth auth, IHttpContextAccessor http)
    {
        _auth = auth;
        _http = http;
    }

    /// <summary>
    /// Returns the active session or null when the request is anonymous /
    /// the cookie has expired or been revoked. Caller decides whether to
    /// redirect to /t/login.
    /// </summary>
    public async ValueTask<TenantSessionContext?> GetAsync(CancellationToken ct = default)
    {
        if (_resolved) return _cached;
        _resolved = true;

        var cookie = _http.HttpContext?.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookie)) return null;

        _cached = await _auth.GetActiveSessionAsync(cookie, ct);
        return _cached;
    }
}
