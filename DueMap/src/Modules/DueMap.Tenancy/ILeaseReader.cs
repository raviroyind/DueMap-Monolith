using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Read-only queries against the lease set. The orchestrator uses this to
/// drive the daily per-PM loop without taking a dependency on the DbContext.
/// </summary>
public interface ILeaseReader
{
    /// <summary>
    /// Active leases for a PM as of the supplied date: started on or before it,
    /// not ended (or ending on/after it).
    /// </summary>
    Task<IReadOnlyList<Lease>> ListActiveAsync(int propertyManagerId, DateOnly asOf, CancellationToken ct);

    /// <summary>
    /// PMs with at least one active lease on the supplied date. The sweep job
    /// uses this to decide whose processing to enqueue.
    /// </summary>
    Task<IReadOnlyList<int>> ListPropertyManagerIdsWithActiveLeasesAsync(DateOnly asOf, CancellationToken ct);

    /// <summary>
    /// EVERY lease for the PM regardless of date — including ones that haven't
    /// started yet and ones that have ended. The engine only ever assesses
    /// ACTIVE leases (<see cref="ListActiveAsync"/>), but the Leases screen has
    /// to show the whole portfolio: a PM who created a future-dated lease and
    /// then saw an empty grid reasonably concluded the app had lost it.
    /// </summary>
    Task<IReadOnlyList<Lease>> ListAllAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Lease projections for the mapping UI: each lease with its currently
    /// linked customer's display name (or null if unmapped).
    /// </summary>
    Task<IReadOnlyList<LeaseWithCustomer>> ListWithCustomerAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Single lease by id, scoped to the PM. Returns null if the lease doesn't
    /// exist OR belongs to a different PM — the scoping prevents data leakage
    /// across PMs even if the caller forgot to validate ownership separately.
    /// </summary>
    Task<Lease?> GetByIdAsync(int leaseId, int propertyManagerId, CancellationToken ct);
}
