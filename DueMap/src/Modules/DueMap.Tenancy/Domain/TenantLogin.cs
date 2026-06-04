namespace DueMap.Tenancy.Domain;

/// <summary>
/// One per (PM, tenant email). Created on first magic-link request for a
/// recognised customer. <see cref="PasswordHash"/> is null for tenants who've
/// only ever used the magic link; populated when they opt into a password.
/// </summary>
public sealed class TenantLogin
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int CustomerId { get; set; }
    public string Email { get; set; } = default!;
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Outstanding magic-link token. Single-use: <see cref="ConsumedAt"/> is set
/// the first time it's redeemed; subsequent redemptions return null even if
/// the token hash matches. Raw token never persisted — only the SHA-256 hash.
/// </summary>
public sealed class TenantMagicLink
{
    public long Id { get; set; }
    public string TokenHash { get; set; } = default!;
    public int TenantLoginId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public string? RequestIp { get; set; }
    public string? ConsumeIp { get; set; }
}

/// <summary>
/// Active portal session, keyed by an opaque cookie. Cookie value itself never
/// touches the DB — we only persist the SHA-256 hash so a database read can't
/// reveal session tokens. Sliding TTL: <see cref="LastSeenAt"/> bumped on use.
/// </summary>
public sealed class TenantSession
{
    public long Id { get; set; }
    public string CookieHash { get; set; } = default!;
    public int TenantLoginId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? CreatedIp { get; set; }
}
