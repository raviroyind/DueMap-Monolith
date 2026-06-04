using DueMap.Billing.Domain;
using DueMap.Tenancy.Domain;

namespace DueMap.Billing;

public enum ActionOutcome
{
    Executed,
    SkippedAlreadyDone,
    SkippedNoContact,
    Failed
}

public sealed record ActionExecutionResult(ActionOutcome Outcome, string? Detail);

/// <summary>
/// Executes one <see cref="PlannedAction"/> for one lease atomically:
/// idempotency check → resolve template → render → dispatch (if a send) or
/// record fee (if an assess) → write the assessment_run row.
/// </summary>
public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(
        Lease lease,
        DateOnly currentDueDate,
        DateOnly businessDate,
        PlannedAction action,
        CancellationToken ct);
}
