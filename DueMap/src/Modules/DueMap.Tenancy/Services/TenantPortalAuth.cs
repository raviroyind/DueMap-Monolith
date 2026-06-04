using System.Security.Cryptography;
using System.Text;
using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

/// <summary>
/// All tenant-portal auth happens here. Uses <see cref="IDbContextFactory{TenancyDbContext}"/>
/// so concurrent /t/* requests inside one Blazor circuit don't race on a
/// shared scoped DbContext (same reason as the v71 refactor).
///
/// Token / cookie strategy:
///   * Raw values are 32 bytes of cryptographic randomness, base64url-encoded
///     (43 chars). They appear ONLY in the email body (magic link) or the
///     Set-Cookie header (session) — never on disk.
///   * On disk we store SHA-256(raw) hex-encoded (64 chars). A DB read can't
///     yield an active session.
///   * All comparisons against persisted hashes are constant-time.
///
/// TTLs:
///   * Magic link: 15 minutes.
///   * Session: 30 days sliding (last_seen_at bumped on every authenticated
///     request; expires_at is created_at + 30d, not last_seen_at + 30d, so a
///     compromised cookie can't be kept alive forever).
/// </summary>
internal sealed class TenantPortalAuth : ITenantPortalAuth
{
    private const int    TokenBytes        = 32;
    private static readonly TimeSpan MagicLinkTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SessionTtl   = TimeSpan.FromDays(30);

    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public TenantPortalAuth(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    // ---------------------------------------------------------------------
    // Magic link issue
    // ---------------------------------------------------------------------

    public async Task<MagicLinkIssued?> RequestMagicLinkAsync(
        string email, string? requestIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Find the customer row matching this email. If a single email lives
        // under multiple PMs (rare but possible), pick the most recently
        // updated — proper multi-PM disambiguation UI is a Phase 2 task.
        // Email comparison relies on SQL Server's default case-insensitive
        // collation (SQL_Latin1_General_CP1_CI_AS); we normalise the input
        // anyway so the persisted tenant_logins.email is stable.
        var customer = await db.Customers
            .AsNoTracking()
            .Where(c => c.Email != null && c.Email == normalized && c.IsActive)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new { c.Id, c.PropertyManagerId })
            .FirstOrDefaultAsync(ct);
        if (customer is null) return null;

        // Find-or-create the tenant_login row for this (PM, email).
        var login = await db.TenantLogins
            .FirstOrDefaultAsync(l => l.PropertyManagerId == customer.PropertyManagerId
                                   && l.Email == normalized, ct);
        if (login is null)
        {
            login = new TenantLogin
            {
                PropertyManagerId = customer.PropertyManagerId,
                CustomerId = customer.Id,
                Email = normalized,
                CreatedAt = DateTime.UtcNow
            };
            db.TenantLogins.Add(login);
            await db.SaveChangesAsync(ct);
        }

        var (raw, hash) = NewTokenPair();
        var expiresAt = DateTime.UtcNow.Add(MagicLinkTtl);

        db.TenantMagicLinks.Add(new TenantMagicLink
        {
            TokenHash = hash,
            TenantLoginId = login.Id,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            RequestIp = requestIp
        });
        await db.SaveChangesAsync(ct);

        return new MagicLinkIssued(login.Id, login.Email, raw, expiresAt);
    }

    // ---------------------------------------------------------------------
    // Magic link redeem
    // ---------------------------------------------------------------------

    public async Task<SessionIssued?> RedeemMagicLinkAsync(
        string rawToken, string? requestIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = HashToken(rawToken);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var link = await db.TenantMagicLinks
            .FirstOrDefaultAsync(m => m.TokenHash == hash, ct);
        if (link is null) return null;
        if (link.ConsumedAt is not null) return null;          // single-use
        if (link.ExpiresAt <= DateTime.UtcNow) return null;     // expired

        // Atomic-ish: mark consumed first so a parallel redeem loses the race
        // (the unique index on token_hash ensures we found the row exactly once).
        link.ConsumedAt = DateTime.UtcNow;
        link.ConsumeIp = requestIp;

        var session = await CreateSessionAsync(db, link.TenantLoginId, requestIp, ct);

        // Bump login.last_login_at — used by the UI greeting & for inactive
        // login pruning later.
        var login = await db.TenantLogins.FirstOrDefaultAsync(l => l.Id == link.TenantLoginId, ct);
        if (login is not null) login.LastLoginAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return session;
    }

    // ---------------------------------------------------------------------
    // Password sign-in
    // ---------------------------------------------------------------------

    public async Task<SessionIssued?> SignInWithPasswordAsync(
        string email, string password, string? requestIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return null;
        var normalized = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Cross-PM email lookup: pick the most recent. Same caveat as
        // RequestMagicLinkAsync.
        var login = await db.TenantLogins
            .Where(l => l.Email == normalized && l.PasswordHash != null)
            .OrderByDescending(l => l.LastLoginAt ?? l.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (login is null || login.PasswordHash is null) return null;

        if (!PortalPasswordHasher.Verify(login.PasswordHash, password)) return null;

        var session = await CreateSessionAsync(db, login.Id, requestIp, ct);
        login.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return session;
    }

    // ---------------------------------------------------------------------
    // Session read / revoke
    // ---------------------------------------------------------------------

    public async Task<TenantSessionContext?> GetActiveSessionAsync(
        string rawCookie, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawCookie)) return null;
        var hash = HashToken(rawCookie);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Join session -> login -> customer (for display name) in one round-trip.
        var row = await (
            from s in db.TenantSessions
            where s.CookieHash == hash && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow
            join l in db.TenantLogins on s.TenantLoginId equals l.Id
            join c in db.Customers    on l.CustomerId    equals c.Id
            select new
            {
                Session = s,
                LoginId = l.Id,
                l.PropertyManagerId,
                l.CustomerId,
                l.Email,
                HasPassword = l.PasswordHash != null,
                c.DisplayName
            }).FirstOrDefaultAsync(ct);
        if (row is null) return null;

        // Sliding last_seen — cheap UPDATE, batched with nothing else.
        row.Session.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new TenantSessionContext(
            row.LoginId,
            row.PropertyManagerId,
            row.CustomerId,
            row.Email,
            row.DisplayName,
            row.HasPassword);
    }

    public async Task RevokeSessionAsync(string rawCookie, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawCookie)) return;
        var hash = HashToken(rawCookie);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var session = await db.TenantSessions
            .FirstOrDefaultAsync(s => s.CookieHash == hash && s.RevokedAt == null, ct);
        if (session is null) return;

        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------
    // Password set
    // ---------------------------------------------------------------------

    public async Task SetPasswordAsync(int tenantLoginId, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(newPassword));

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var login = await db.TenantLogins.FirstOrDefaultAsync(l => l.Id == tenantLoginId, ct)
            ?? throw new InvalidOperationException($"Tenant login {tenantLoginId} not found.");

        login.PasswordHash = PortalPasswordHasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------

    private static async Task<SessionIssued> CreateSessionAsync(
        TenancyDbContext db, int tenantLoginId, string? createdIp, CancellationToken ct)
    {
        var (raw, hash) = NewTokenPair();
        var expiresAt = DateTime.UtcNow.Add(SessionTtl);

        db.TenantSessions.Add(new TenantSession
        {
            CookieHash = hash,
            TenantLoginId = tenantLoginId,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            CreatedIp = createdIp
        });
        await db.SaveChangesAsync(ct);

        return new SessionIssued(tenantLoginId, raw, expiresAt);
    }

    /// <summary>32 random bytes -> (base64url raw, sha256 hex hash).</summary>
    private static (string Raw, string Hash) NewTokenPair()
    {
        Span<byte> bytes = stackalloc byte[TokenBytes];
        RandomNumberGenerator.Fill(bytes);
        var raw = Base64UrlEncode(bytes);
        return (raw, HashToken(raw));
    }

    private static string HashToken(string raw)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), hash);
        return Convert.ToHexString(hash);   // upper-case hex, 64 chars
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

/// <summary>
/// Tiny PBKDF2 wrapper — we don't pull in Microsoft.AspNetCore.Identity here
/// because Tenancy module is intentionally Identity-free. Same family of
/// algorithms (PBKDF2 + HMAC-SHA256) used by Identity's PasswordHasher.
/// Hash string format: "v1${iterations}${base64_salt}${base64_hash}".
/// </summary>
internal static class PortalPasswordHasher
{
    private const int Iterations = 210_000;   // OWASP 2023 floor for PBKDF2-HMAC-SHA256
    private const int SaltBytes  = 16;
    private const int HashBytes  = 32;

    public static string Hash(string password)
    {
        Span<byte> salt = stackalloc byte[SaltBytes];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string stored, string password)
    {
        // Defensive — never throw on a malformed hash; just fail the verify.
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "v1") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1) return false;

        byte[] salt, expected;
        try
        {
            salt     = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
