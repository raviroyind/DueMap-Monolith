namespace DueMap.Tenancy.Domain;

/// <summary>
/// PM-level master toggles and defaults for the three product-facing notice
/// kinds. A master toggle of <c>false</c> is a kill switch — no notice of that
/// kind goes out for any of the PM's leases, regardless of per-lease overrides.
/// </summary>
public sealed class PmNoticePreferences
{
    public int PropertyManagerId { get; set; }

    public bool PreDueMasterEnabled { get; set; } = true;
    public int PreDueDefaultDaysBefore { get; set; } = 3;

    public bool DueDateMasterEnabled { get; set; } = true;

    public bool PostDueMasterEnabled { get; set; } = true;
    public PostDueMode PostDueDefaultMode { get; set; } = PostDueMode.GracePeriod;
    public int PostDueDefaultGraceDays { get; set; } = 5;

    public DateTime UpdatedAt { get; set; }
}
