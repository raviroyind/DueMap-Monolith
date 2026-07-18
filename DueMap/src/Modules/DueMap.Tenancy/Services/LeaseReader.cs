using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class LeaseReader : ILeaseReader
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public LeaseReader(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Lease>> ListActiveAsync(
        int propertyManagerId,
        DateOnly asOf,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Leases.AsNoTracking()
            .Where(l => l.PropertyManagerId == propertyManagerId
                     && l.StartDate <= asOf
                     && (l.EndDate == null || l.EndDate >= asOf))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Lease>> ListAllAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Leases.AsNoTracking()
            .Where(l => l.PropertyManagerId == propertyManagerId)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> ListPropertyManagerIdsWithActiveLeasesAsync(
        DateOnly asOf,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Leases.AsNoTracking()
            .Where(l => l.StartDate <= asOf && (l.EndDate == null || l.EndDate >= asOf))
            .Select(l => l.PropertyManagerId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<Lease?> GetByIdAsync(int leaseId, int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Leases.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leaseId && l.PropertyManagerId == propertyManagerId, ct);
    }

    public async Task<IReadOnlyList<LeaseWithCustomer>> ListWithCustomerAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await (
            from l in db.Leases.AsNoTracking()
            where l.PropertyManagerId == propertyManagerId
            join c in db.Customers.AsNoTracking() on l.CustomerId equals c.Id into cj
            from c in cj.DefaultIfEmpty()
            orderby l.Id
            select new LeaseWithCustomer(
                l.Id,
                l.MonthlyRent,
                l.StartDate,
                l.CustomerId,
                c != null ? c.DisplayName : null)
        ).ToListAsync(ct);
    }
}
