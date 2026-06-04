using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PmNoticePreferencesService : IPmNoticePreferencesService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PmNoticePreferencesService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<PmNoticePreferences> GetOrCreateAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.PmNoticePreferences
            .FirstOrDefaultAsync(p => p.PropertyManagerId == propertyManagerId, ct);
        if (existing is not null) return existing;

        var defaults = new PmNoticePreferences
        {
            PropertyManagerId = propertyManagerId,
            UpdatedAt = DateTime.UtcNow
        };
        db.PmNoticePreferences.Add(defaults);
        await db.SaveChangesAsync(ct);
        return defaults;
    }

    public async Task<PmNoticePreferences> UpdateAsync(PmNoticePreferences prefs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.PmNoticePreferences
            .FirstOrDefaultAsync(p => p.PropertyManagerId == prefs.PropertyManagerId, ct)
            ?? throw new InvalidOperationException(
                $"PM notice preferences row not found for property_manager_id={prefs.PropertyManagerId}. " +
                "Call GetOrCreateAsync first.");

        existing.PreDueMasterEnabled     = prefs.PreDueMasterEnabled;
        existing.PreDueDefaultDaysBefore = prefs.PreDueDefaultDaysBefore;
        existing.DueDateMasterEnabled    = prefs.DueDateMasterEnabled;
        existing.PostDueMasterEnabled    = prefs.PostDueMasterEnabled;
        existing.PostDueDefaultMode      = prefs.PostDueDefaultMode;
        existing.PostDueDefaultGraceDays = prefs.PostDueDefaultGraceDays;
        existing.UpdatedAt               = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return existing;
    }
}
