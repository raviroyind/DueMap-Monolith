using System.Globalization;
using DueMap.Tenancy;

namespace DueMap.Web.Services;

/// <summary>
/// Per-circuit money formatting for the signed-in PM. Resolves the PM's
/// display currency once (cached for the circuit) and hands out a CultureInfo
/// whose "C" format renders that currency. Pages must format amounts with
/// this culture — a bare <c>ToString("C")</c> uses the HOST machine's locale,
/// which painted every amount ₹ on an en-IN server for a USD book (QA P2).
/// </summary>
public sealed class PmMoney
{
    private readonly PmContext _pm;
    private readonly IPmCurrencyReader _currency;
    private CultureInfo? _cached;

    public PmMoney(PmContext pm, IPmCurrencyReader currency)
    {
        _pm = pm;
        _currency = currency;
    }

    public async Task<CultureInfo> GetCultureAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;
        try
        {
            var pmId = await _pm.GetPmIdAsync();
            var code = await _currency.GetCurrencyCodeAsync(pmId, ct);
            _cached = MoneyCultures.For(code);
        }
        catch
        {
            // Fail-safe: a broken lookup must never fall through to the host
            // culture — USD is the launch-market default.
            _cached = MoneyCultures.Usd;
        }
        return _cached;
    }
}

/// <summary>
/// Maps ISO-4217 currency codes to render cultures. Codes without a natural
/// English culture render US-shaped numbers prefixed with the code
/// ("SEK 1,234.56") — unambiguous beats pretty.
/// </summary>
public static class MoneyCultures
{
    public static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");

    public static CultureInfo For(string? code) => (code ?? "USD").Trim().ToUpperInvariant() switch
    {
        "USD" or "" => Usd,
        "GBP" => CultureInfo.GetCultureInfo("en-GB"),
        "EUR" => CultureInfo.GetCultureInfo("en-IE"),
        "CAD" => CultureInfo.GetCultureInfo("en-CA"),
        "AUD" => CultureInfo.GetCultureInfo("en-AU"),
        "NZD" => CultureInfo.GetCultureInfo("en-NZ"),
        "INR" => CultureInfo.GetCultureInfo("en-IN"),
        var other => Generic(other)
    };

    private static CultureInfo Generic(string code)
    {
        var c = (CultureInfo)Usd.Clone();
        c.NumberFormat.CurrencySymbol = code + " ";   // nbsp keeps code+amount together
        return c;
    }
}
