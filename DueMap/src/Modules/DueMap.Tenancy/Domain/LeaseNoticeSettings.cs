namespace DueMap.Tenancy.Domain;

/// <summary>
/// Per-lease overrides on top of the PM's defaults. Null value columns mean
/// "inherit the PM default". The state-law grace floor is applied at assessment
/// time in the Billing module — this row stores the PM's raw request.
/// </summary>
public sealed class LeaseNoticeSettings
{
    public int LeaseId { get; set; }

    public bool PreDueEnabled { get; set; } = true;
    public int? PreDueDaysBefore { get; set; }

    public bool DueDateEnabled { get; set; } = true;

    public bool PostDueEnabled { get; set; } = true;
    public PostDueMode? PostDueMode { get; set; }
    public int? PostDueGraceDays { get; set; }

    public DateTime UpdatedAt { get; set; }
}
