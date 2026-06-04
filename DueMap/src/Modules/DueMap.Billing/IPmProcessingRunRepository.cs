using DueMap.Billing.Domain;

namespace DueMap.Billing;

/// <summary>
/// Day-grain idempotency at the PM level. <see cref="TryStartAsync"/> tries to
/// claim (PM, business_date) — null return means another worker already owns
/// today's run. <see cref="MarkCompletedAsync"/> and <see cref="MarkFailedAsync"/>
/// close the row.
/// </summary>
public interface IPmProcessingRunRepository
{
    Task<PmProcessingRun?> TryStartAsync(int propertyManagerId, DateOnly businessDate, CancellationToken ct);

    Task MarkCompletedAsync(long runId, int leasesPlanned, int actionsExecuted, CancellationToken ct);

    Task MarkFailedAsync(long runId, string reason, CancellationToken ct);

    Task<IReadOnlyList<int>> ListPropertyManagersAlreadyProcessedAsync(
        DateOnly businessDate,
        CancellationToken ct);

    /// <summary>Most recent runs for a PM, newest first.</summary>
    Task<IReadOnlyList<PmProcessingRun>> ListRecentAsync(int propertyManagerId, int take, CancellationToken ct);
}
