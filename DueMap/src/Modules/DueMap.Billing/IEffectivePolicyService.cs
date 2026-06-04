using DueMap.Billing.Domain;
using DueMap.Tenancy.Domain;

namespace DueMap.Billing;

/// <summary>
/// Computes the <see cref="EffectiveLeasePolicy"/> for a lease on a given date
/// by merging PM-level preferences, per-lease overrides, and the state-law
/// grace floor returned by the Rules module.
/// </summary>
public interface IEffectivePolicyService
{
    Task<EffectiveLeasePolicy> BuildAsync(Lease lease, DateOnly assessmentDate, CancellationToken ct);
}
