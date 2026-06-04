using Microsoft.Extensions.Logging;

namespace DueMap.Integrations.Accounting.Sync;

internal sealed partial class AccountingTimezoneSuggester : IAccountingTimezoneSuggester
{
    private readonly IAccountingConnectionService _connections;
    private readonly IEnumerable<IAccountingDataClient> _clients;
    private readonly ILogger<AccountingTimezoneSuggester> _logger;

    public AccountingTimezoneSuggester(
        IAccountingConnectionService connections,
        IEnumerable<IAccountingDataClient> clients,
        ILogger<AccountingTimezoneSuggester> logger)
    {
        _connections = connections;
        _clients = clients;
        _logger = logger;
    }

    public async Task<TimezoneSuggestion?> SuggestForPmAsync(int pmId, CancellationToken ct)
    {
        try
        {
            var conn = await _connections.GetActiveAsync(pmId, ct);
            if (conn is null) return null;

            var token = await _connections.GetAccessTokenAsync(pmId, ct);
            if (token is null) return null;

            var client = _clients.FirstOrDefault(c => c.Provider == conn.Provider);
            if (client is null) return null;

            var info = await client.GetCompanyInfoAsync(token, conn.RealmId, ct);

            // Prefer the raw tz string when present (Xero) — it's specific.
            // Otherwise use country + region (QBO).
            var iana = MapXeroTimezone(info.RawTimeZoneId)
                    ?? MapByCountryRegion(info.CountryCode, info.RegionCode);
            if (iana is null) return null;

            var source = conn.Provider switch
            {
                AccountingProvider.QuickBooks => "QuickBooks",
                AccountingProvider.Xero       => "Xero",
                _                             => conn.Provider.ToString()
            };
            return new TimezoneSuggestion(iana, source);
        }
        catch (Exception ex)
        {
            // Suggester must never break onboarding — log and fall through to
            // "no suggestion", which makes the form default to UTC.
            LogSuggestFailed(_logger, ex, pmId);
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Xero Windows-tz string → IANA. Only the zones our dropdown supports are
    // mapped; anything else returns null and we fall back to country/region.
    // -------------------------------------------------------------------------
    private static string? MapXeroTimezone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.ToUpperInvariant() switch
        {
            "EASTERNSTANDARDTIME"      => "America/New_York",
            "CENTRALSTANDARDTIME"      => "America/Chicago",
            "MOUNTAINSTANDARDTIME"     => "America/Denver",
            "PACIFICSTANDARDTIME"      => "America/Los_Angeles",
            "USMOUNTAINSTANDARDTIME"   => "America/Phoenix",
            "ALASKANSTANDARDTIME"      => "America/Anchorage",
            "HAWAIIANSTANDARDTIME"     => "Pacific/Honolulu",
            "GMTSTANDARDTIME"          => "Europe/London",
            "WEUROPESTANDARDTIME"      => "Europe/Berlin",
            "INDIASTANDARDTIME"        => "Asia/Kolkata",
            "AUSEASTERNSTANDARDTIME"   => "Australia/Sydney",
            "UTC"                      => "UTC",
            _                          => null
        };
    }

    // -------------------------------------------------------------------------
    // Country / sub-division → IANA. Coverage matches the curated dropdown in
    // CloseSettings.razor. For US states we pick the dominant tz in that state
    // (e.g. all of CA is Pacific). PMs in split-zone states still get a
    // reasonable default and can override.
    // -------------------------------------------------------------------------
    private static string? MapByCountryRegion(string? country, string? region)
    {
        country = country?.ToUpperInvariant();
        region  = region?.ToUpperInvariant();

        if (country == "US" && !string.IsNullOrEmpty(region))
        {
            return region switch
            {
                "WA" or "OR" or "CA" or "NV"                            => "America/Los_Angeles",
                "AZ"                                                     => "America/Phoenix",
                "MT" or "ID" or "WY" or "UT" or "CO" or "NM"             => "America/Denver",
                "ND" or "SD" or "NE" or "KS" or "OK" or "TX"
                or "MN" or "IA" or "MO" or "AR" or "LA"
                or "WI" or "IL" or "MS" or "AL" or "TN"                 => "America/Chicago",
                "MI" or "IN" or "OH" or "KY" or "GA" or "FL"
                or "SC" or "NC" or "VA" or "WV" or "PA" or "NY"
                or "VT" or "NH" or "ME" or "MA" or "CT" or "RI"
                or "NJ" or "DE" or "MD" or "DC"                         => "America/New_York",
                "AK"                                                     => "America/Anchorage",
                "HI"                                                     => "Pacific/Honolulu",
                _                                                        => "America/New_York" // US default
            };
        }

        return country switch
        {
            "US" => "America/New_York",
            "GB" or "UK" => "Europe/London",
            "DE" => "Europe/Berlin",
            "IN" => "Asia/Kolkata",
            "AU" => "Australia/Sydney",
            "CA" => "America/Toronto",   // not in our curated list yet; suggester
                                         // returns it and the form falls back
                                         // to UTC if absent (see CloseSettings).
            _    => null
        };
    }

    [LoggerMessage(EventId = 7501, Level = LogLevel.Warning,
        Message = "Timezone suggester failed for pm={PropertyManagerId}; falling back to UTC default")]
    static partial void LogSuggestFailed(ILogger logger, Exception ex, int propertyManagerId);
}
