using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DueMap.Web.Security;

/// <summary>
/// Basic-Auth gate in front of the operator surfaces under <c>/admin/*</c>.
///
/// Credentials come from <see cref="AdminBasicAuthOptions"/> — i.e. from
/// configuration / user-secrets / env vars. If either Username or Password
/// is missing the middleware returns <strong>503 Service Unavailable</strong>
/// with a clear log line; it deliberately does NOT fall back to a hardcoded
/// developer credential (P0-4 hardening).
///
/// Placed in the pipeline AFTER <c>UseAuthentication</c> and BEFORE
/// <c>UseAuthorization</c>:
///   * On Basic-Auth success we overwrite <c>HttpContext.User</c> with an
///     authenticated principal so the fallback authorization policy passes.
///   * On failure we return 401 with <c>WWW-Authenticate: Basic</c> and
///     short-circuit — no Blazor render, no cookie redirect-to-login.
///
/// TODO: Replace with role-based gating once a platform-admin role lands on
/// <c>ApplicationUser</c>.
/// </summary>
internal sealed partial class AdminBasicAuthMiddleware
{
    private const string Realm = "DueMap Admin";

    // Any path under one of these prefixes triggers the Basic challenge.
    private static readonly PathString[] GatedPrefixes =
    {
        new("/admin/worker-logs"),
        new("/admin/dry-run"),
        new("/admin/connection-health"),
        new("/admin/pm"),               // P0-4 — per-PM activity timeline
        new("/admin/feature-flags")     // reserved for the future cockpit UI
    };

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<AdminBasicAuthOptions> _options;
    private readonly ILogger<AdminBasicAuthMiddleware> _logger;

    public AdminBasicAuthMiddleware(
        RequestDelegate next,
        IOptionsMonitor<AdminBasicAuthOptions> options,
        ILogger<AdminBasicAuthMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var gated = false;
        foreach (var prefix in GatedPrefixes)
        {
            if (path.StartsWithSegments(prefix)) { gated = true; break; }
        }
        if (!gated)
        {
            await _next(context);
            return;
        }

        // Fail-CLOSED if creds aren't configured. No hardcoded fallback.
        var opts = _options.CurrentValue;
        if (!opts.IsConfigured)
        {
            LogNotConfigured(_logger, path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync(
                "Admin surface unavailable — Ops:AdminAuth:Username + Password are not configured. " +
                "Set them in user-secrets, environment variables, or appsettings.");
            return;
        }

        if (TryValidateBasicAuth(context, opts.Username!, opts.Password!, out var user))
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

    private static bool TryValidateBasicAuth(HttpContext context, string expectedUser, string expectedPassword, out string user)
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
        var userOk = FixedTimeEquals(providedUser, expectedUser);
        var passOk = FixedTimeEquals(providedPass, expectedPassword);

        if (userOk && passOk)
        {
            user = expectedUser;
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

    [LoggerMessage(EventId = 9401, Level = LogLevel.Error,
        Message = "Admin Basic Auth is not configured (Ops:AdminAuth:Username/Password missing). Returning 503 for {Path}.")]
    static partial void LogNotConfigured(ILogger logger, PathString path);
}
