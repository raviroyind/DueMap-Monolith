using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PmDailyCloseSettingsService : IPmDailyCloseSettingsService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PmDailyCloseSettingsService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<PmDailyCloseSettings> GetOrDefaultAsync(int pmId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.PmDailyCloseSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PropertyManagerId == pmId, ct);
        if (existing is not null) return existing;

        var tz = await db.PropertyManagers
            .AsNoTracking()
            .Where(p => p.Id == pmId)
            .Select(p => p.TimeZoneId)
            .FirstOrDefaultAsync(ct) ?? "UTC";

        return new PmDailyCloseSettings
        {
            PropertyManagerId = pmId,
            Enabled = true,
            SendHourLocal = 7,
            TimeZoneId = tz
        };
    }

    public async Task UpsertAsync(PmDailyCloseSettings settings, CancellationToken ct)
    {
        if (settings.SendHourLocal < 0 || settings.SendHourLocal > 23)
            throw new ArgumentOutOfRangeException(nameof(settings),
                settings.SendHourLocal, "SendHourLocal must be 0–23.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.PmDailyCloseSettings
            .FirstOrDefaultAsync(s => s.PropertyManagerId == settings.PropertyManagerId, ct);

        if (existing is null)
        {
            settings.UpdatedAt = DateTime.UtcNow;
            db.PmDailyCloseSettings.Add(settings);
        }
        else
        {
            existing.Enabled           = settings.Enabled;
            existing.SendHourLocal     = settings.SendHourLocal;
            existing.TimeZoneId        = settings.TimeZoneId;
            existing.RecipientOverride = settings.RecipientOverride;
            existing.CcList            = settings.CcList;
            existing.UpdatedAt         = DateTime.UtcNow;
        }

        // Keep PM.TimeZoneId in lockstep — worker schedules per-PM chains off it.
        var pm = await db.PropertyManagers
            .FirstOrDefaultAsync(p => p.Id == settings.PropertyManagerId, ct);
        if (pm is not null && pm.TimeZoneId != settings.TimeZoneId)
        {
            pm.TimeZoneId = settings.TimeZoneId;
        }

        await db.SaveChangesAsync(ct);
    }
}
