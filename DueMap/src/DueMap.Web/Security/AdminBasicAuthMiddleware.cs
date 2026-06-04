using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DueMap.Web.Security;

/// <summary>
/// Developer-only Basic-Auth gate for <c>/admin/worker-logs</c>.
///
/// The worker-logs page is a raw, cross-tenant view of the structured log
/// stream — it must NOT be reachable by signed-in property managers, who
/// would otherwise pass the global <c>RequireAuthenticatedUser</c> fallback
/// policy. Until we have a proper platform-admin role on
/// <c>ApplicationUser</c>, this middleware short-circuits the path with an
/// HTTP Basic challenge and hardcoded credentials.
///
/// Placed in the pipeline AFTER <c>UseAuthentication</c> and BEFORE
/// <c>UseAuthorization</c>:
///   * On Basic-Auth success we overwrite <c>HttpContext.User</c> with an
///     authenticated principal so the fallback authorization policy passes.
///   * On failure we return 401 with <c>WWW-Authenticate: Basic</c> and
///     short-circuit — no Blazor render, no cookie redirect-to-login.
///
/// TODO: Replace with role-based gating once an Admin role lands. Also
/// extend <see cref="DueMap.Web.Services.WorkerLogReader"/> to enforce
/// tenant scoping at the SQL layer at that time.
/// </summary>
internal sealed class AdminBasicAuthMiddleware
{
    // Hardcoded for now — see class summary. When we move to a real admin
    // role this whole file goes away.
    private const string ExpectedUser     = "ravi@orcasoft.in";
    private const string ExpectedPassword = "Vegas@Fontana%A91";
    private const string Realm            = "DueMap Admin";

    // Any path under this prefix triggers the Basic challenge.
    private static readonly PathString GatedPrefix = new("/admin/worker-logs");

    private readonly RequestDelegate _next;

    public AdminBasicAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(GatedPrefix))
        {
            await _next(context);
            return;
        }

        if (TryValidateBasicAuth(context, out var user))
        {
            // Replace the principal so the fallback "must be authenticated"
            // policy passes and downstream Blazor sees an admin caller.
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, user) },
                authenticationType: "BasicAdmin"));
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Realm}\", charset=\"UTF-8\"";
    }

    private static bool TryValidateBasicAuth(HttpContext context, out string user)
    {
        user = string.Empty;

        string? header = context.Request.Headers.Authorization;
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        var colon = decoded.IndexOf(':');
        if (colon < 0) return false;

        var providedUser = decoded[..colon];
        var providedPass = decoded[(colon + 1)..];

        // Constant-time compare to avoid leaking match position via timing.
        var userOk = FixedTimeEquals(providedUser, ExpectedUser);
        var passOk = FixedTimeEquals(providedPass, ExpectedPassword);

        if (userOk && passOk)
        {
            user = ExpectedUser;
            return true;
        }
        return false;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        // CryptographicOperations.FixedTimeEquals requires equal lengths.
        // Pad to the longer one so we don't short-circuit on length mismatch.
        var len = Math.Max(ab.Length, bb.Length);
        Array.Resize(ref ab, len);
        Array.Resize(ref bb, len);
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
