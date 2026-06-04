namespace DueMap.Tenancy.Domain;

/// <summary>
/// Per-PM configuration for the Daily Close report email — captured in step 2
/// of onboarding and editable later from workspace settings.
///
/// The worker reads this row at the end of each per-PM nightly chain to
/// decide (a) whether to send at all, (b) at what local hour, and (c) where.
/// </summary>
public sealed class PmDailyCloseSettings
{
    public int PropertyManagerId { get; set; }

    /// <summary>Master switch — uncheck to stop the daily close email entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hour-of-day (0–23) at which the close goes out, in the PM's own
    /// timezone. Defaults to 07:00 so it lands in the inbox first thing.
    /// </summary>
    public int SendHourLocal { get; set; } = 7;

    /// <summary>
    /// IANA tz id (e.g. "America/Los_Angeles"). The onboarding form writes
    /// the same value to <see cref="PropertyManager.TimeZoneId"/> so the
    /// sweep job and the close email stay in sync.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Override the PM's primary email when set. Useful for sending the
    /// close to a bookkeeper rather than the PM personally.
    /// </summary>
    public string? RecipientOverride { get; set; }

    /// <summary>
    /// Comma-separated CC list. Validated + split by the service layer; this
    /// is the raw on-disk form for simplicity.
    /// </summary>
    public string? CcList { get; set; }

    public DateTime UpdatedAt { get; set; }
}
