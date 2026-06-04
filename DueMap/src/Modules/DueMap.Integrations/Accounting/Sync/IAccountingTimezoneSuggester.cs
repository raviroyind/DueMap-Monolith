namespace DueMap.Integrations.Accounting.Sync;

/// <summary>
/// Best-effort "what timezone should this PM use?" probe — calls the
/// connected accounting provider's company-info endpoint and maps the result
/// to an IANA tz id. Used by the onboarding Daily Close form to pre-fill
/// the timezone dropdown without making the PM hunt for their own zone.
///
/// The PM can always override the suggestion in the UI; this is purely a
/// default. Returns null when no useful tz can be inferred (e.g. country
/// missing, unmapped Windows tz string) — caller should fall back to "UTC".
/// </summary>
public interface IAccountingTimezoneSuggester
{
    /// <summary>
    /// Returns an IANA tz id from <see cref="System.TimeZoneInfo"/> or null.
    /// Network errors are caught and logged inside — they don't propagate
    /// (suggester failure must never block onboarding).
    /// </summary>
    Task<TimezoneSuggestion?> SuggestForPmAsync(int propertyManagerId, CancellationToken ct);
}

/// <summary>
/// A successful suggestion. <see cref="Source"/> is shown in the UI as a
/// small "auto-detected from QuickBooks" / "auto-detected from Xero" badge
/// so the PM understands where the default came from.
/// </summary>
public sealed record TimezoneSuggestion(string IanaTimeZoneId, string Source);
