using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Resolves the deliverable contact details for a lease. Currently a stub
/// (returns null) because the customers table is populated by the QB/Xero
/// sync, which lands in a later turn. Once it's wired, this becomes a join
/// from <c>tenancy.leases</c> through the customer mapping to <c>tenancy.customers</c>.
/// </summary>
public interface ITenantContactResolver
{
    Task<TenantContact?> ResolveAsync(int leaseId, CancellationToken ct);
}
