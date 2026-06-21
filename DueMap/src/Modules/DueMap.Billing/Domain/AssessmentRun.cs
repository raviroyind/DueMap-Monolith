namespace DueMap.Billing.Domain;

/// <summary>
/// The idempotency receipt for one action executed against one lease's rent
/// period. The (lease_id, due_date, action_kind) tuple is UNIQUE in the DB —
/// duplicate inserts bounce, which is what makes the assessment job safe to
/// retry. <see cref="NoticeDeliveryId"/> is set for every "send_*" action;
/// <see cref="LateFeeAssessmentId"/> is set for <see cref="ActionKind.AssessLateFee"/>.
/// </summary>
public sealed class AssessmentRun
{
    public long Id { get; set; }
    public int LeaseId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly AssessmentDate { get; set; }
    public ActionKind ActionKind { get; set; }

    public long? NoticeDeliveryId { get; set; }
    public int? LateFeeAssessmentId { get; set; }

    /// <summary>
    /// P2-3: discriminator for multi-touch reminder sequences. Null for the
    /// legacy one-per-kind cadence; set to the sequence step's key so the
    /// (lease, due_date, kind, step_key) unique constraint fires each step once.
    /// </summary>
    public string? StepKey { get; set; }

    public DateTime CreatedAt { get; set; }
}
