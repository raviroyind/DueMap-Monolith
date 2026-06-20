namespace DueMap.Tenancy.Discovery;

/// <summary>
/// Auto-discovery (P1-1). Runs after the initial accounting sync and
/// infers per-lease rent / due-day / state from invoice patterns +
/// customer billing address. Persists results to
/// <c>tenancy.leases.inferred_*</c>; the onboarding review screen reads
/// them via <see cref="GetForPmAsync"/> and one-taps a confirm via
/// <see cref="ConfirmAsync"/>.
///
/// Gated by feature flag <c>tenancy.autodiscovery</c> — when off, all
/// methods are no-ops (counts/empty lists, never throwing) so the
/// surface is safe to wire into onboarding before the flag flips.
///
/// Idempotent: <see cref="RunForPmAsync"/> overwrites prior inferred
/// values without resetting <c>DiscoveryConfirmedAt</c>. A re-sync
/// won't undo a PM's confirmation.
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Re-infer every lease for one PM. Returns the count of leases
    /// whose inferred values were written (0 when flag is off).
    /// </summary>
    Task<int> RunForPmAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Stamp <c>DiscoveryConfirmedAt = UtcNow</c>. Idempotent — already-
    /// confirmed leases are a no-op (don't overwrite the prior stamp).
    /// </summary>
    Task<bool> ConfirmAsync(int leaseId, CancellationToken ct);

    /// <summary>
    /// One row per lease for the review UI. Returns an empty list when
    /// the flag is off so callers can render a "discovery disabled"
    /// banner without branching.
    /// </summary>
    Task<IReadOnlyList<DiscoveryResult>> GetForPmAsync(int propertyManagerId, CancellationToken ct);
}
