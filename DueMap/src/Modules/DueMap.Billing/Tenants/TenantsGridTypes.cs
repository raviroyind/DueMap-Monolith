namespace DueMap.Billing.Tenants;

/// <summary>
/// Per-row compliance signal for the unified Tenants screen (P1-5). Aligned
/// with <c>ComplianceScanService</c> semantics but reduced to a single badge.
/// </summary>
public enum FeeRisk
{
    /// <summary>No lease yet, or no fee configured — nothing to assess.</summary>
    Unknown = 0,
    /// <summary>Fee is within the state's legal ceiling (or the state has no % cap).</summary>
    Ok      = 1,
    /// <summary>Can't verify — lease has no state, or the state has no rule on file.</summary>
    Review  = 2,
    /// <summary>Configured fee exceeds the state cap. Must be fixed before go-live.</summary>
    OverCap = 3
}

/// <summary>
/// One row in the unified Tenants grid: a synced customer joined to its lease
/// (if any) plus the derived compliance + autopay signals. A customer with no
/// lease shows <see cref="LeaseId"/> = null and is a candidate for "Create
/// lease" (the old Link-Customers flow).
/// </summary>
public sealed record TenantRow(
    int CustomerId,
    string CustomerName,
    string? Email,
    string? BillingState,
    int? LeaseId,
    decimal? MonthlyRent,
    int? StateId,
    string? StateCode,
    decimal? LateFeePercent,
    bool FeesStaged,
    bool RemindersEnabled,
    FeeRisk FeeRisk,
    string AutopayStatus);   // placeholder until P2 (e.g. "—" / "Not enrolled")
