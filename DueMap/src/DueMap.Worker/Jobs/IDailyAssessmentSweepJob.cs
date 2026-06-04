namespace DueMap.Worker.Jobs;

/// <summary>
/// Public so Hangfire's reflection-based job invoker can resolve it from the
/// container. Fires every 15 minutes; each tick selects PMs that have at least
/// one active lease and no completed processing row for today, then enqueues a
/// one-shot <see cref="DueMap.Billing.IPmDailyOrchestrator"/> job per PM.
/// </summary>
public interface IDailyAssessmentSweepJob
{
    Task RunAsync(CancellationToken ct);
}
