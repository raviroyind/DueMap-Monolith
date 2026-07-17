using System.Globalization;

namespace DueMap.Web.Services;

/// <summary>
/// Human-readable UTC timestamps for the PM UI. The old "u" format
/// ("2026-07-17 06:39:28Z") read like a server log; PMs asked for normal
/// dates and 12-hour clocks. Invariant culture keeps the output stable
/// regardless of the host locale (same rationale as PmMoney).
/// </summary>
public static class FriendlyTime
{
    /// <summary>"Jul 17, 2026 · 6:39 AM UTC"</summary>
    public static string Utc(DateTime utc) =>
        utc.ToString("MMM d, yyyy · h:mm tt 'UTC'", CultureInfo.InvariantCulture);

    public static string Utc(DateTime? utc, string whenNull = "never") =>
        utc is DateTime dt ? Utc(dt) : whenNull;

    /// <summary>Date-only flavour: "Jul 17, 2026".</summary>
    public static string Date(DateTime utc) =>
        utc.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
