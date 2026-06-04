using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Minimal write surface for the PM aggregate. Used by the registration flow
/// to materialize a PM org alongside the first user. A full admin UI for PM
/// org management is out of scope for v1.
/// </summary>
public interface IPropertyManagerWriter
{
    Task<PropertyManager> CreateAsync(string name, CancellationToken ct);

    /// <summary>
    /// Hard-delete a PM. Intended for compensating rollback right after a
    /// failed registration — once dependent rows exist (users, leases, etc.)
    /// the underlying FKs will reject this and throw. Callers should treat
    /// failure as best-effort: log it, don't propagate.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct);

    /// <summary>
    /// Advance the PM's <see cref="Domain.OnboardingStatus"/> to <paramref name="status"/>
    /// IFF it's strictly higher than the current value. Never regresses —
    /// callers can blindly call this at each milestone (Connected on OAuth,
    /// Synced + Active on first sync) without worrying about ordering.
    /// </summary>
    Task SetMinimumStatusAsync(int propertyManagerId, Domain.OnboardingStatus status, CancellationToken ct);
}
