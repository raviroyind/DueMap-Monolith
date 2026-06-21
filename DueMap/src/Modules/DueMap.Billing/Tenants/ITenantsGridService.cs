namespace DueMap.Billing.Tenants;

/// <summary>
/// Read + write backbone for the unified Tenants screen (P1-5). Joins synced
/// customers (Tenancy) with their lease + the state's fee cap (Rules) to build
/// the grid, and routes inline edits / lease creation / linking back to the
/// Tenancy writers. Lives in Billing because it spans Tenancy + Rules — the
/// same tier as <c>EffectivePolicyService</c> and AutoSetup.
///
/// Flag-gated by <c>tenancy.tenants_screen</c> at the UI layer; the service
/// itself is a plain query/command surface.
/// </summary>
public interface ITenantsGridService
{
    /// <summary>One row per synced customer, newest-customer-first not guaranteed — sort in the UI.</summary>
    Task<IReadOnlyList<TenantRow>> GetRowsAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>Inline edit of a linked lease's rent + state.</summary>
    Task UpdateLeaseAsync(int leaseId, decimal monthlyRent, int stateId, CancellationToken ct);

    /// <summary>
    /// Create a lease for a customer that doesn't have one yet (replaces the
    /// old Link-Customers create step). Returns the new lease id.
    /// </summary>
    Task<int> CreateLeaseForCustomerAsync(int propertyManagerId, int customerId, int stateId, decimal monthlyRent, CancellationToken ct);

    /// <summary>The available states for the row's state dropdown (id + 2-letter code).</summary>
    Task<IReadOnlyList<(int Id, string Code, string Name)>> GetStatesAsync(CancellationToken ct);
}
