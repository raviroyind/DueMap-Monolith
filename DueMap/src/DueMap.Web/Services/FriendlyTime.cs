using System.Globalization;

namespace DueMap.Web.Services;

/// <summary>
/// Human-readable timestamps for the PM UI, rendered in the WORKSPACE's time
/// zone (captured from the PM's QuickBooks/Xero organisation — see PmClock).
/// Two problems this solves: the old "u" format read like a server log
/// ("2026-07-17 06:39:28Z"), and everything was shown in UTC, so a PM in
/// Denver saw times six hours off their own clock.
///
/// Invariant culture keeps output stable regardless of the host locale (same
/// rationale as PmMoney). Values are stored and passed around as UTC; the
/// conversion happens here, at the edge.
/// </summary>
public static class FriendlyTime
{
    /// <summary>"Jul 17, 2026 · 6:39 AM MDT" — converted into <paramref name="zone"/>.</summary>
    public static string Local(DateTime utc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        return local.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture)
             + " " + Abbreviation(zone, local);
    }

    public static string Local(DateTime? utc, TimeZoneInfo zone, string whenNull = "never") =>
        utc is DateTime dt ? Local(dt, zone) : whenNull;

    /// <summary>Date-only in the workspace zone: "Jul 17, 2026".</summary>
    public static string LocalDate(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
                    .ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Short zone label. Curated for the zones our QBO/Xero suggester can
    /// produce, because .NET exposes only long names ("Mountain Daylight
    /// Time") and naive initialisms get it wrong for some zones (GMT/BST).
    /// Anything unmapped falls back to an explicit UTC offset, which is never
    /// ambiguous.
    /// </summary>
    private static string Abbreviation(TimeZoneInfo zone, DateTime localTime)
    {
        var dst = zone.IsDaylightSavingTime(localTime);
        return zone.Id switch
        {
            "UTC" or "Etc/UTC"      => "UTC",
            "America/New_York"      => dst ? "EDT"  : "EST",
            "America/Toronto"       => dst ? "EDT"  : "EST",
            "America/Chicago"       => dst ? "CDT"  : "CST",
            "America/Denver"        => dst ? "MDT"  : "MST",
            "America/Phoenix"       => "MST",              // no DST
            "America/Los_Angeles"   => dst ? "PDT"  : "PST",
            "America/Anchorage"     => dst ? "AKDT" : "AKST",
            "Pacific/Honolulu"      => "HST",              // no DST
            "Europe/London"         => dst ? "BST"  : "GMT",
            "Europe/Berlin"         => dst ? "CEST" : "CET",
            "Asia/Kolkata"          => "IST",
            "Australia/Sydney"      => dst ? "AEDT" : "AEST",
            _                       => Offset(zone, localTime)
        };
    }

    private static string Offset(TimeZoneInfo zone, DateTime localTime)
    {
        var off = zone.GetUtcOffset(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified));
        if (off == TimeSpan.Zero) return "UTC";
        var sign = off < TimeSpan.Zero ? "-" : "+";
        var abs = off.Duration();
        return abs.Minutes == 0
            ? string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{abs.Hours}")
            : string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{abs.Hours}:{abs.Minutes:D2}");
    }

    // ---- Legacy UTC-labelled helpers -------------------------------------
    // Kept for surfaces with no PM context (e.g. admin tooling), where the
    // workspace zone genuinely isn't known.

    public static string Utc(DateTime utc) =>
        utc.ToString("MMM d, yyyy · h:mm tt 'UTC'", CultureInfo.InvariantCulture);

    public static string Utc(DateTime? utc, string whenNull = "never") =>
        utc is DateTime dt ? Utc(dt) : whenNull;

    public static string Date(DateTime utc) =>
        utc.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
