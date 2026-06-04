namespace DueMap.Worker.Jobs;

public interface IDailyCloseReportJob
{
    /// <summary>
    /// Per-PM variant — called as a Hangfire continuation from the sweep
    /// after the orchestrator finishes. <paramref name="businessDate"/> is
    /// the PM's local date that just closed.
    /// </summary>
    Task RunForPmAsync(int propertyManagerId, DateOnly businessDate, CancellationToken ct);
}
