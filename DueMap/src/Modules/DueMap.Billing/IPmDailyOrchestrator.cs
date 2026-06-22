using DueMap.Billing.Domain;

namespace DueMap.Billing;

public sealed record PmProcessingOutcome(
    bool Started,
    int LeasesPlanned,
    int ActionsExecuted,
    int ActionsSkipped,
    int ActionsFailed,
    string? FailureReason,
    /// <summary>
    /// Populated only when <see cref="IPmDailyOrchestrator.ProcessAsync"/>
    /// was called with <see cref="ExecutionMode.DryRun"/>. Always null in
    /// Live mode.
    /// </summary>
    DayPlanPreview? DryRunPreview = null);

/// <summary>
/// Drives one PM's daily processing: claims the (PM, date) slot, runs the
/// accounting sync hook, iterates active leases through the planner, and
/// hands each <see cref="Domain.PlannedAction"/> to the <see cref="IActionExecutor"/>.
///
/// Hangfire enqueues one call to <see cref="ProcessAsync"/> per ready PM per
/// day. The method is safe to retry — the (PM, date) slot is the idempotency
/// guard at the top, and each action's run row is the guard at the bottom.
///
/// <para>
/// <see cref="ExecutionMode.DryRun"/> short-circuits every write (no slot
/// claim, no sync, no dispatch, no fee ledger, no assessment_runs) and
/// returns the planned actions as a <see cref="DayPlanPreview"/> on the
/// outcome. The default value is <see cref="ExecutionMode.Live"/> so
/// existing callers (Hangfire sweep) don't need to change.
/// </para>
/// </summary>
public interface IPmDailyOrchestrator
{
    Task<PmProcessingOutcome> ProcessAsync(
        int propertyManagerId,
        DateOnly businessDate,
        ExecutionMode mode = ExecutionMode.Live,
        CancellationToken ct = default);
}
