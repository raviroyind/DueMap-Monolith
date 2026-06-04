namespace DueMap.Billing.Domain;

/// <summary>
/// One assessed late fee against one rent period for one lease. The monthly
/// rent and rule provenance are snapshotted at assessment time — later edits to
/// the lease or the rule do not retroactively change the historical record.
/// At most one row per (lease, due_date); enforced by a UNIQUE constraint.
/// </summary>
public sealed class LateFeeAssessment
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly AssessmentDate { get; set; }

    public decimal FeeAmount { get; set; }
    public decimal MonthlyRentSnapshot { get; set; }

    public int StateRuleVersionId { get; set; }
    public int? LocalRuleOverrideId { get; set; }

    public LateFeeAssessmentStatus Status { get; set; } = LateFeeAssessmentStatus.Assessed;
    public string? ReversalReason { get; set; }
    public DateTime? ReversedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
