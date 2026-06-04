namespace DueMap.Integrations.Accounting.Sync;

/// <summary>
/// Lightweight company-level metadata fetched from the accounting provider —
/// just enough to (a) personalise the dashboard (task #61) and (b) suggest a
/// sensible default IANA timezone during onboarding (no need for the PM to
/// hunt for their own zone in a dropdown when QBO/Xero already know it).
///
/// Neither provider returns IANA tz ids directly:
///   * QuickBooks exposes Country + LegalAddr.CountrySubDivisionCode
///     (US state) and no tz field. We map state-or-country → IANA.
///   * Xero returns Timezone as a Windows tz string ("EASTERNSTANDARDTIME").
///     We map that → IANA.
/// </summary>
public sealed record AccountingCompanyInfo(
    string  CompanyName,
    string? CountryCode,        // ISO 3166-1 alpha-2 (e.g. "US", "GB", "AU")
    string? RegionCode,         // sub-division (e.g. US state "CA"); null when not available
    string? RawTimeZoneId,      // Xero Windows tz name; null for QBO
    string? PrimaryEmail);      // Contact email from QBO CompanyInfo.Email.Address; null when absent or for Xero
