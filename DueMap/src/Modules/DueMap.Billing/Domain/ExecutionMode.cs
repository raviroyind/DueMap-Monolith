namespace DueMap.Billing.Domain;

/// <summary>
/// Whether a daily run actually executes or just previews the plan.
///
/// <see cref="Live"/> is the nightly Hangfire path — claims slots, sends
/// emails, charges fees, records every row. This is the default for
/// every interface so existing callers don't have to change.
///
/// <see cref="DryRun"/> is the operator-facing preview. It plans
/// identically but writes nothing: no pm_processing_runs row, no
/// notices.deliveries, no late_fee_assessments, no assessment_runs, no
/// outbound dispatch, no QBO/Xero PDF fetch. The orchestrator returns a
/// <see cref="DayPlanPreview"/> with one <see cref="PlannedActionPreview"/>
/// per action that <em>would</em> have happened, including the rendered
/// subject and a short body excerpt.
///
/// "Zero writes" is the contract. P0-2 tests enforce it via NSubstitute
/// (the repos / dispatcher are asserted to never have been called).
/// </summary>
public enum ExecutionMode
{
    Live = 0,
    DryRun = 1
}
