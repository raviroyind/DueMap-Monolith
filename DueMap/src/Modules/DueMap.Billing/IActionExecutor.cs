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

/// <summary>
/// Result of executing one planned action.
/// <list type="bullet">
///   <item><see cref="Outcome"/> is the live-mode classification used by the
///   orchestrator counters. In <see cref="ExecutionMode.DryRun"/> it's set to
///   <see cref="ActionOutcome.Executed"/> when a preview was produced (the
///   action "would have run"), and to <see cref="ActionOutcome.SkippedNoContact"/>
///   / <see cref="ActionOutcome.Failed"/> when the preview itself was blocked.</item>
///   <item><see cref="Detail"/> is a human-readable trailing message — kept
///   short, used by the worker log + Daily Close summary.</item>
///   <item><see cref="Preview"/> is populated only in DryRun and carries the
///   row the admin endpoint serialises.</item>
/// </list>
/// </summary>
public sealed record ActionExecutionResult(
    ActionOutcome Outcome,
    string? Detail,
    PlannedActionPreview? Preview = null);

/// <summary>
/// Executes one <see cref="PlannedAction"/> for one lease atomically:
/// idempotency check → resolve template → render → dispatch (if a send) or
/// record fee (if an assess) → write the assessment_run row.
///
/// <para>
/// <see cref="ExecutionMode.DryRun"/> performs the planning + rendering
/// reads only and returns a <see cref="PlannedActionPreview"/> on the result
/// instead of writing or dispatching anything. The default is
/// <see cref="ExecutionMode.Live"/> so existing call sites stay unchanged.
/// </para>
/// </summary>
public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(
        Lease lease,
        DateOnly currentDueDate,
        DateOnly businessDate,
        PlannedAction action,
        ExecutionMode mode = ExecutionMode.Live,
        CancellationToken ct = default);
}
