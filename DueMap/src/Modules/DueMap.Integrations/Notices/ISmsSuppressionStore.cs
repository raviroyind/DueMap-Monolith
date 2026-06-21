namespace DueMap.Integrations.Notices;

/// <summary>
/// SMS opt-out list (P2-2). A phone that texts STOP is suppressed here and
/// never texted again until it texts START. <see cref="TwilioSmsSender"/>
/// checks <see cref="IsSuppressedAsync"/> before every send; the Twilio
/// inbound webhook calls <see cref="SuppressAsync"/> / <see cref="UnsuppressAsync"/>.
///
/// Phone matching is on normalized digits (last-10 for US), so the free-form
/// numbers from accounting sync match Twilio's E.164 <c>From</c>.
/// </summary>
public interface ISmsSuppressionStore
{
    Task<bool> IsSuppressedAsync(string phone, CancellationToken ct);
    Task SuppressAsync(string phone, string reason, int? propertyManagerId, CancellationToken ct);
    Task UnsuppressAsync(string phone, CancellationToken ct);
}

/// <summary>Phone normalization shared by the store + sender. Pure + testable.</summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Reduce a phone to comparable digits: strip everything non-numeric, then
    /// keep the last 10 (US local) so "+1 (415) 555-1234", "14155551234", and
    /// "415-555-1234" all collapse to "4155551234". Returns "" if no digits.
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        Span<char> digits = stackalloc char[phone.Length];
        var n = 0;
        foreach (var c in phone)
        {
            if (c is >= '0' and <= '9') digits[n++] = c;
        }
        if (n == 0) return string.Empty;
        var s = new string(digits[..n]);
        return s.Length > 10 ? s[^10..] : s;
    }
}
