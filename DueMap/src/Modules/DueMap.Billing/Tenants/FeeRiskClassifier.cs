namespace DueMap.Billing.Tenants;

/// <summary>
/// Pure fee-risk classification for the Tenants grid. Same guardrail logic as
/// <c>ResolvedRule.ClampToStateMax</c> / <c>ComplianceScanService</c>, reduced
/// to a single per-row badge. No I/O — fully unit-testable.
/// </summary>
public static class FeeRiskClassifier
{
    /// <param name="leaseFeePercent">The lease's configured late-fee percent (whole number, e.g. 5.00 = 5%). Null = none set.</param>
    /// <param name="stateMaxPercent">The state's absolute % ceiling. Null = state has no % cap on file.</param>
    /// <param name="stateKnown">False when the lease has no resolvable state (can't verify compliance).</param>
    public static FeeRisk Classify(decimal? leaseFeePercent, decimal? stateMaxPercent, bool stateKnown)
    {
        // Can't verify without a state — always surfaces for review.
        if (!stateKnown) return FeeRisk.Review;

        // No fee configured yet → nothing to assess.
        if (leaseFeePercent is null) return FeeRisk.Unknown;

        // State known but carries no % cap (flat-only or reasonableness) →
        // we can't prove an overage, so it's acceptable as configured.
        if (stateMaxPercent is null) return FeeRisk.Ok;

        return leaseFeePercent > stateMaxPercent ? FeeRisk.OverCap : FeeRisk.Ok;
    }
}
