namespace DueMap.Billing;

public sealed record PmProcessingOutcome(
    bool Started,
    int LeasesPlanned,
    int ActionsExecuted,
    int ActionsSkipped,
    int ActionsFailed,
    string? FailureReason);

/// <summary>
/// Drives one PM's daily processing: claims the (PM, date) slot, runs the
/// accounting sync hook, iterates active leases through the planner, and
/// hands each <see cref="Domain.PlannedAction"/> to the <see cref="IActionExecutor"/>.
///
/// Hangfire enqueues one call to <see cref="ProcessAsync"/> per ready PM per
/// day. The method is safe to retry — the (PM, date) slot is the idempotency
/// guard at the top, and each action's run row is the guard at the bottom.
/// </summary>
public interface IPmDailyOrchestrator
{
    Task<PmProcessingOutcome> ProcessAsync(int propertyManagerId, DateOnly businessDate, CancellationToken ct);
}
