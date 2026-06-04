using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

/// <summary>
/// Joins <c>tenancy.leases.customer_id</c> to <c>tenancy.customers</c> to
/// produce the deliverable contact details. Returns null when:
/// <list type="bullet">
///   <item>The lease has no <c>customer_id</c> set (auto-link failed; needs manual mapping)</item>
///   <item>The linked customer has no email AND no phone</item>
/// </list>
/// </summary>
internal sealed class TenantContactResolver : ITenantContactResolver
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public TenantContactResolver(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<TenantContact?> ResolveAsync(int leaseId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var row = await (
            from l in db.Leases.AsNoTracking()
            where l.Id == leaseId && l.CustomerId != null
            join c in db.Customers.AsNoTracking() on l.CustomerId equals c.Id
            select new { c.DisplayName, c.Email, c.Phone, c.IsActive }
        ).FirstOrDefaultAsync(ct);

        if (row is null || !row.IsActive) return null;
        if (string.IsNullOrWhiteSpace(row.Email) && string.IsNullOrWhiteSpace(row.Phone)) return null;

        return new TenantContact(leaseId, row.Email, row.Phone, row.DisplayName);
    }
}
