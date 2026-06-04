namespace DueMap.Tenancy.Services;

/// <summary>
/// All tenant-portal authentication operations. Sits over three tables:
/// tenant_logins, tenant_magic_links, tenant_sessions. The service is the
/// security boundary — callers never see raw token / cookie values that
/// haven't been validated through here.
/// </summary>
public interface ITenantPortalAuth
{
    /// <summary>
    /// Mints a magic-link token for the given email. Returns the raw token
    /// (only time it ever exists in plaintext — caller emails it and forgets).
    /// Returns null when no <c>tenancy.customers</c> row with that email
    /// exists for any PM, but the page should always show the same
    /// "if your email is on file..." confirmation regardless to prevent
    /// account enumeration.
    /// </summary>
    Task<MagicLinkIssued?> RequestMagicLinkAsync(string email, string? requestIp, CancellationToken ct);

    /// <summary>
    /// Validates a raw magic-link token, marks it consumed, opens a session.
    /// Returns the issued cookie value (raw — set as cookie, never store) or
    /// null when the token is unknown / expired / already consumed.
    /// </summary>
    Task<SessionIssued?> RedeemMagicLinkAsync(string rawToken, string? requestIp, CancellationToken ct);

    /// <summary>
    /// Password sign-in for tenants who've opted in. Returns null on bad
    /// credentials — caller shows the generic error, never distinguishes
    /// "no such email" from "wrong password" (account-enumeration defence).
    /// </summary>
    Task<SessionIssued?> SignInWithPasswordAsync(string email, string password, string? requestIp, CancellationToken ct);

    /// <summary>
    /// Validates a cookie value against an active, non-expired session.
    /// Bumps last_seen_at on hit. Returns null on miss — caller treats as
    /// anonymous.
    /// </summary>
    Task<TenantSessionContext?> GetActiveSessionAsync(string rawCookie, CancellationToken ct);

    /// <summary>Marks the session revoked. Idempotent.</summary>
    Task RevokeSessionAsync(string rawCookie, CancellationToken ct);

    /// <summary>
    /// Sets / rotates a password on the login. Called from /t/set-password
    /// after first redeem. Hashes via ASP.NET Core PasswordHasher.
    /// </summary>
    Task SetPasswordAsync(int tenantLoginId, string newPassword, CancellationToken ct);
}

/// <summary>Returned by RequestMagicLinkAsync — caller must email <see cref="RawToken"/>.</summary>
public sealed record MagicLinkIssued(int TenantLoginId, string Email, string RawToken, DateTime ExpiresAt);

/// <summary>Returned by both redeem paths — caller sets cookie with <see cref="RawCookie"/>.</summary>
public sealed record SessionIssued(int TenantLoginId, string RawCookie, DateTime ExpiresAt);

/// <summary>
/// What every authenticated tenant-portal page sees about the caller. Joined
/// at session-lookup time so individual pages don't re-query.
/// </summary>
public sealed record TenantSessionContext(
    int      TenantLoginId,
    int      PropertyManagerId,
    int      CustomerId,
    string   Email,
    string?  DisplayName,
    bool     HasPassword);
