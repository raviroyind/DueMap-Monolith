using DueMap.Tenancy.Domain;

namespace DueMap.Billing.Domain;

/// <summary>
/// The fully-resolved policy that applies to one lease on one assessment date.
/// This is the single artifact that captures the four-way merge:
/// <list type="bullet">
///   <item>PM master toggles (kill switches)</item>
///   <item>PM defaults</item>
///   <item>Per-lease overrides</item>
///   <item>State-law grace floor (from the Rules module)</item>
/// </list>
/// All assessment-time decisions read from this record, never from the raw rows.
/// </summary>
public sealed record EffectiveLeasePolicy(
    int LeaseId,
    int PropertyManagerId,
    int StateId,
    int? JurisdictionId,
    bool PreDueEnabled,
    int PreDueDaysBefore,
    bool DueDateEnabled,
    bool PostDueEnabled,
    PostDueMode PostDueMode,
    int EffectiveGraceDays,
    int RequestedGraceDays,
    int StateMinimumGraceDays,
    decimal MonthlyRent)
{
    /// <summary>
    /// True when the PM asked for a grace period shorter than state law allows.
    /// The UI should surface this so the PM understands why <c>EffectiveGraceDays</c>
    /// is larger than what they entered.
    /// </summary>
    public bool GraceWasRaisedByLaw => EffectiveGraceDays > RequestedGraceDays;
}
