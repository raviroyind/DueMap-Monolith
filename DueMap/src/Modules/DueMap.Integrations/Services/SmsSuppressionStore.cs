using DueMap.Integrations.Notices;
using DueMap.Integrations.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Integrations.Services;

/// <summary>
/// EF-backed opt-out list keyed by normalized phone. One DbContext per call
/// (factory pattern, mirrors AccountingConnectionService). Suppress is an
/// idempotent upsert; unsuppress deletes any matching row.
/// </summary>
internal sealed class SmsSuppressionStore : ISmsSuppressionStore
{
    private readonly IDbContextFactory<IntegrationsDbContext> _dbFactory;

    public SmsSuppressionStore(IDbContextFactory<IntegrationsDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<bool> IsSuppressedAsync(string phone, CancellationToken ct)
    {
        var key = PhoneNormalizer.Normalize(phone);
        if (key.Length == 0) return false;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.SmsSuppressions.AsNoTracking().AnyAsync(s => s.Phone == key, ct);
    }

    public async Task SuppressAsync(string phone, string reason, int? propertyManagerId, CancellationToken ct)
    {
        var key = PhoneNormalizer.Normalize(phone);
        if (key.Length == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Idempotent: only insert if not already suppressed (unique index on phone).
        if (await db.SmsSuppressions.AnyAsync(s => s.Phone == key, ct)) return;

        db.SmsSuppressions.Add(new SmsSuppression
        {
            Phone = key,
            PropertyManagerId = propertyManagerId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task UnsuppressAsync(string phone, CancellationToken ct)
    {
        var key = PhoneNormalizer.Normalize(phone);
        if (key.Length == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.SmsSuppressions.Where(s => s.Phone == key).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.SmsSuppressions.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
