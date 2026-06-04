namespace DueMap.Billing.Domain;

public enum PmProcessingStatus
{
    Started,
    Completed,
    Failed
}

internal static class PmProcessingStatusMapping
{
    public static string ToWire(this PmProcessingStatus value) => value switch
    {
        PmProcessingStatus.Started   => "started",
        PmProcessingStatus.Completed => "completed",
        PmProcessingStatus.Failed    => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static PmProcessingStatus FromWire(string value) => value switch
    {
        "started"   => PmProcessingStatus.Started,
        "completed" => PmProcessingStatus.Completed,
        "failed"    => PmProcessingStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown status")
    };
}

/// <summary>
/// One row per (property_manager_id, business_date). The UNIQUE constraint is
/// the daily idempotency guard — only one worker performs today's processing
/// for a given PM, regardless of how many sweep ticks fire.
/// </summary>
public sealed class PmProcessingRun
{
    public long Id { get; set; }
    public int PropertyManagerId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public PmProcessingStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public int LeasesPlanned { get; set; }
    public int ActionsExecuted { get; set; }
}
