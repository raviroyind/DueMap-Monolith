namespace DueMap.Tenancy;

/// <summary>
/// Manual link between a <c>tenancy.leases</c> row and a
/// <c>tenancy.customers</c> row. Used by the mapping UI when auto-link
/// couldn't pick exactly one active lease for the customer.
/// </summary>
public interface ILeaseLinkService
{
    Task LinkCustomerAsync(int leaseId, int? customerId, CancellationToken ct);
}
