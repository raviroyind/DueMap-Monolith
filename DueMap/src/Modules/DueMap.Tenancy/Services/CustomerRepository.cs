using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public CustomerRepository(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<Customer?> GetByExternalAsync(
        int propertyManagerId,
        ExternalAccountingProvider provider,
        string externalId,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PropertyManagerId == propertyManagerId
                                   && c.ExternalProvider == provider
                                   && c.ExternalId == externalId, ct);
    }

    public async Task<Customer?> GetByIdAsync(int customerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
    }

    public async Task<IReadOnlyList<Customer>> ListByPropertyManagerAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Customers.AsNoTracking()
            .Where(c => c.PropertyManagerId == propertyManagerId)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<Customer> UpsertAsync(Customer customer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.Customers.FirstOrDefaultAsync(
            c => c.PropertyManagerId == customer.PropertyManagerId
              && c.ExternalProvider == customer.ExternalProvider
              && c.ExternalId == customer.ExternalId, ct);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            customer.CreatedAt = now;
            customer.UpdatedAt = now;
            if (customer.LastSyncedAt == default) customer.LastSyncedAt = now;
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            return customer;
        }

        existing.DisplayName  = customer.DisplayName;
        existing.Email        = customer.Email;
        existing.Phone        = customer.Phone;
        existing.IsActive     = customer.IsActive;
        existing.LastSyncedAt = customer.LastSyncedAt == default ? now : customer.LastSyncedAt;
        existing.UpdatedAt    = now;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<int> MarkPreflightSentAsync(
        int propertyManagerId,
        IReadOnlyCollection<int> customerIds,
        CancellationToken ct)
    {
        if (customerIds.Count == 0) return 0;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Filter by both id AND PropertyManagerId so a forged id from another
        // tenant simply doesn't match — the query is the security boundary.
        var rows = await db.Customers
            .Where(c => c.PropertyManagerId == propertyManagerId
                     && customerIds.Contains(c.Id)
                     && c.PreflightNotifiedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var c in rows)
        {
            c.PreflightNotifiedAt = now;
            c.UpdatedAt = now;
        }
        if (rows.Count > 0)
            await db.SaveChangesAsync(ct);

        return rows.Count;
    }
}
