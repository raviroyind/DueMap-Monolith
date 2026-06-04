using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class LeaseNoticeSettingsService : ILeaseNoticeSettingsService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public LeaseNoticeSettingsService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<LeaseNoticeSettings?> GetAsync(int leaseId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.LeaseNoticeSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.LeaseId == leaseId, ct);
    }

    public async Task<IReadOnlyList<LeaseNoticeSettings>> ListForPropertyManagerAsync(
        int propertyManagerId,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Outer-join leases → settings so leases with no row appear as a synthesized
        // defaults record. The PM admin grid shows one row per lease either way.
        var rows = await (
            from l in db.Leases.AsNoTracking()
            where l.PropertyManagerId == propertyManagerId
            join s in db.LeaseNoticeSettings.AsNoTracking() on l.Id equals s.LeaseId into sj
            from s in sj.DefaultIfEmpty()
            select new { LeaseId = l.Id, Settings = s }
        ).ToListAsync(ct);

        return rows
            .Select(r => r.Settings ?? new LeaseNoticeSettings { LeaseId = r.LeaseId })
            .ToList();
    }

    public async Task<LeaseNoticeSettings> UpsertAsync(LeaseNoticeSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.LeaseNoticeSettings
            .FirstOrDefaultAsync(s => s.LeaseId == settings.LeaseId, ct);

        if (existing is null)
        {
            settings.UpdatedAt = DateTime.UtcNow;
            db.LeaseNoticeSettings.Add(settings);
            await db.SaveChangesAsync(ct);
            return settings;
        }

        existing.PreDueEnabled    = settings.PreDueEnabled;
        existing.PreDueDaysBefore = settings.PreDueDaysBefore;
        existing.DueDateEnabled   = settings.DueDateEnabled;
        existing.PostDueEnabled   = settings.PostDueEnabled;
        existing.PostDueMode      = settings.PostDueMode;
        existing.PostDueGraceDays = settings.PostDueGraceDays;
        existing.UpdatedAt        = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<int> BulkApplyAsync(
        IReadOnlyCollection<int> leaseIds,
        LeaseNoticeSettingsPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(leaseIds);
        ArgumentNullException.ThrowIfNull(patch);
        if (leaseIds.Count == 0) return 0;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Materialize the set: any lease without a row gets a default row so the
        // patch has something to write into. This keeps the contract "after
        // bulk-apply, every selected lease has the requested settings".
        var idSet = leaseIds.ToHashSet();
        var existing = await db.LeaseNoticeSettings
            .Where(s => idSet.Contains(s.LeaseId))
            .ToListAsync(ct);

        var existingIds = existing.Select(s => s.LeaseId).ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var id in idSet.Where(id => !existingIds.Contains(id)))
        {
            var row = new LeaseNoticeSettings { LeaseId = id, UpdatedAt = now };
            db.LeaseNoticeSettings.Add(row);
            existing.Add(row);
        }

        foreach (var row in existing)
        {
            ApplyPatch(row, patch);
            row.UpdatedAt = now;
        }

        return await db.SaveChangesAsync(ct);
    }

    private static void ApplyPatch(LeaseNoticeSettings row, LeaseNoticeSettingsPatch patch)
    {
        if (patch.PreDueEnabled is bool preEnabled)         row.PreDueEnabled = preEnabled;
        if (patch.ClearPreDueDaysBefore)                    row.PreDueDaysBefore = null;
        else if (patch.PreDueDaysBefore is int preDays)     row.PreDueDaysBefore = preDays;

        if (patch.DueDateEnabled is bool dueEnabled)        row.DueDateEnabled = dueEnabled;

        if (patch.PostDueEnabled is bool postEnabled)       row.PostDueEnabled = postEnabled;
        if (patch.ClearPostDueMode)                         row.PostDueMode = null;
        else if (patch.PostDueMode is PostDueMode mode)     row.PostDueMode = mode;

        if (patch.ClearPostDueGraceDays)                    row.PostDueGraceDays = null;
        else if (patch.PostDueGraceDays is int graceDays)   row.PostDueGraceDays = graceDays;
    }
}
