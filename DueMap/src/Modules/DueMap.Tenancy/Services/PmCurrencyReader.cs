using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PmCurrencyReader : IPmCurrencyReader
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PmCurrencyReader(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<string> GetCurrencyCodeAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // 1) Explicit default from the accounting connection, when synced.
        //    Column is char(3) fixed-length — trim padding.
        var fromDefaults = await db.PmAccountingDefaults.AsNoTracking()
            .Where(d => d.PropertyManagerId == propertyManagerId)
            .Select(d => d.DefaultCurrency)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(fromDefaults))
        {
            return fromDefaults.Trim().ToUpperInvariant();
        }

        // 2) Majority vote across the PM's synced invoices. Handles books
        //    where the defaults row was never populated but invoices carry
        //    their currency (the common case today).
        var majority = await db.RentInvoices.AsNoTracking()
            .Where(i => i.PropertyManagerId == propertyManagerId
                     && i.Currency != null && i.Currency != "")
            .GroupBy(i => i.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(majority))
        {
            return majority.Trim().ToUpperInvariant();
        }

        // 3) Nothing synced yet — the launch market is US.
        return "USD";
    }
}
