using DueMap.Billing.Domain;

namespace DueMap.Billing;

/// <summary>
/// Idempotency ledger. Every action the orchestrator executes is journaled
/// here; the UNIQUE constraint on (lease, due_date, kind) is the actual safety
/// net — re-running today's job conflicts and rolls back rather than producing
/// a duplicate notice or duplicate late fee.
/// </summary>
public interface IAssessmentRunRepository
{
    Task<bool> HasRunAsync(int leaseId, DateOnly dueDate, ActionKind kind, CancellationToken ct);

    /// <summary>
    /// Insert a new run row. Returns the persisted entity, or <c>null</c> if a
    /// row for the same (lease, due_date, kind) already exists — i.e. another
    /// process / earlier retry has already performed this action.
    /// </summary>
    Task<AssessmentRun?> TryRecordAsync(AssessmentRun run, CancellationToken ct);
}
