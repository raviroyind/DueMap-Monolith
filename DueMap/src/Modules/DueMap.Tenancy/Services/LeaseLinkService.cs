using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class LeaseLinkService : ILeaseLinkService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public LeaseLinkService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task LinkCustomerAsync(int leaseId, int? customerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct)
            ?? throw new InvalidOperationException($"Lease {leaseId} not found.");

        if (customerId is int cid)
        {
            // Validate the customer belongs to the same PM — cross-PM linkage
            // would silently break tenant scoping.
            var customerPm = await db.Customers.AsNoTracking()
                .Where(c => c.Id == cid)
                .Select(c => (int?)c.PropertyManagerId)
                .FirstOrDefaultAsync(ct);
            if (customerPm is null)
            {
                throw new InvalidOperationException($"Customer {cid} not found.");
            }
            if (customerPm != lease.PropertyManagerId)
            {
                throw new InvalidOperationException(
                    $"Customer {cid} belongs to PM {customerPm}, not lease's PM {lease.PropertyManagerId}.");
            }
        }

        lease.CustomerId = customerId;
        await db.SaveChangesAsync(ct);
    }
}
