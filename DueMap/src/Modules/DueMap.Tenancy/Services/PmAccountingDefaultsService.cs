using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PmAccountingDefaultsService : IPmAccountingDefaultsService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PmAccountingDefaultsService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<PmAccountingDefaults> GetOrCreateAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var row = await db.PmAccountingDefaults
            .FirstOrDefaultAsync(d => d.PropertyManagerId == propertyManagerId, ct);
        if (row is not null) return row;

        row = new PmAccountingDefaults
        {
            PropertyManagerId = propertyManagerId,
            UpdatedAt = DateTime.UtcNow
        };
        db.PmAccountingDefaults.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<PmAccountingDefaults> UpsertAsync(PmAccountingDefaults row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.PmAccountingDefaults
            .FirstOrDefaultAsync(d => d.PropertyManagerId == row.PropertyManagerId, ct);

        if (existing is null)
        {
            row.UpdatedAt = DateTime.UtcNow;
            db.PmAccountingDefaults.Add(row);
            await db.SaveChangesAsync(ct);
            return row;
        }

        existing.DefaultCurrency             = row.DefaultCurrency;
        existing.DefaultPaymentTermsDays     = row.DefaultPaymentTermsDays;
        existing.DefaultLateFeeItemExternalId = row.DefaultLateFeeItemExternalId;
        existing.TimezoneId                  = row.TimezoneId;
        existing.LastSyncedAt                = row.LastSyncedAt;
        existing.UpdatedAt                   = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
