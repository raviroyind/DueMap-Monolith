using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Lease creation surface. QB and Xero have no concept of a "lease" — that's
/// PM-domain knowledge — so leases are created either via the manual UI or
/// (future) the QB-invoice-pattern detector that proposes them.
/// </summary>
public interface ILeaseWriter
{
    Task<Lease> CreateAsync(NewLeaseInput input, CancellationToken ct);

    /// <summary>
    /// Persist the AutoSetup-derived late-fee profile on a lease (P1-3).
    /// Always sets <c>FeesStaged = true</c> as a side effect — assessment
    /// is gated until the PM clicks "Go live" (P1-4).
    /// </summary>
    Task UpdateLateFeeProfileAsync(int leaseId, LateFeeProfileInput profile, CancellationToken ct);

    /// <summary>
    /// Inline edit of a lease's core fields from the unified Tenants screen
    /// (P1-5): monthly rent and state. Does NOT touch the fee profile or the
    /// staging flag. Rent must be positive; state must exist.
    /// </summary>
    Task UpdateCoreFieldsAsync(int leaseId, decimal monthlyRent, int stateId, CancellationToken ct);
}

/// <summary>
/// Caller-supplied staged late-fee profile. All values come from
/// <c>AutoSetupService</c> after clamping through
/// <c>ResolvedRule.ClampToStateMax</c> and <c>ClampGrace</c>.
/// </summary>
public sealed record LateFeeProfileInput(
    byte    LateFeeType,        // 1=Flat, 2=Percent, 3=GreaterOf, 4=LesserOf
    decimal? LateFeePercent,
    decimal? LateFeeFlatAmount,
    byte    GraceDays,
    bool    DailyAccrual);

/// <summary>
/// Caller-supplied data for a brand-new lease row. The writer validates
/// (PM owns the customer; state exists; rent positive) and stamps CreatedAt.
/// </summary>
public sealed record NewLeaseInput(
    int PropertyManagerId,
    int StateId,
    int? CustomerId,
    decimal MonthlyRent,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? UnitLabel = null);
