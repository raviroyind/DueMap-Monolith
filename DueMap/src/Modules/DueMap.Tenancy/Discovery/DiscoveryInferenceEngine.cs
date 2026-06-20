namespace DueMap.Tenancy.Discovery;

/// <summary>
/// Pure inference for one lease — no I/O, no clock, no DI. Takes in the
/// raw signals (invoice amounts + due dates + billing state) and returns
/// the best guess for each of <c>rent</c>, <c>due_day</c>, <c>state</c>.
///
/// <para><strong>Mode-based with strict tie-breaking.</strong>
/// We return NULL for any field where two or more values share the top
/// count — better to show "we don't know" than to coin-flip a wrong
/// answer that's then auto-confirmed in the review flow.</para>
///
/// <para><strong>Why mode and not mean?</strong> A tenant who pays one
/// off-cycle catch-up invoice (e.g. $750 vs the usual $1,200) would skew
/// the mean. Mode tolerates that exact noise pattern, which is the most
/// common one in real rent-roll data.</para>
///
/// <para><strong>Rent rounding.</strong> Invoice totals can carry penny
/// tax/fee variation between months ($1,200.00 vs $1,200.05). We round
/// to whole dollars before counting so those land in the same bucket.</para>
/// </summary>
public static class DiscoveryInferenceEngine
{
    /// <summary>
    /// Run the inference. <paramref name="invoiceAmounts"/> and
    /// <paramref name="invoiceDueDays"/> may be any length (including 0) —
    /// no signal in → no signal out. <paramref name="billingState"/> may
    /// be NULL or non-2-letter (treated as ambiguous).
    /// </summary>
    public static InferenceOutcome Infer(
        IReadOnlyCollection<decimal> invoiceAmounts,
        IReadOnlyCollection<int> invoiceDueDays,
        string? billingState)
    {
        return new InferenceOutcome(
            Rent:    InferRent(invoiceAmounts),
            DueDay:  InferDueDay(invoiceDueDays),
            State:   NormaliseState(billingState));
    }

    // ----------------------------------------------------------------------
    // Rent — round to whole dollars, take mode, return NULL on tie.
    // ----------------------------------------------------------------------
    private static decimal? InferRent(IReadOnlyCollection<decimal> amounts)
    {
        if (amounts is null || amounts.Count == 0) return null;

        // Round half-away-from-zero so $1,234.50 → $1,235 (matches the
        // "natural" PM expectation; banker's rounding here would surprise).
        static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

        var groups = amounts
            .Select(Round)
            .GroupBy(v => v)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(2)
            .ToArray();

        // No data, or exactly one row → mode is undefined for our purposes
        // (a single observation isn't evidence of a recurring amount).
        if (groups.Length == 0) return null;
        if (groups.Length == 1) return groups[0].Count >= 2 ? groups[0].Value : null;
        // Tie at the top → ambiguous.
        return groups[0].Count > groups[1].Count ? groups[0].Value : (decimal?)null;
    }

    // ----------------------------------------------------------------------
    // Due day — 1..28 only. Values >28 collapse to 28 (handles end-of-month
    // shenanigans where invoices were stamped on the 30th of a 31-day
    // month). Mode, NULL on tie.
    // ----------------------------------------------------------------------
    private static byte? InferDueDay(IReadOnlyCollection<int> dueDays)
    {
        if (dueDays is null || dueDays.Count == 0) return null;

        static int Clamp(int d) => d < 1 ? 1 : d > 28 ? 28 : d;

        var groups = dueDays
            .Select(Clamp)
            .GroupBy(d => d)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(2)
            .ToArray();

        if (groups.Length == 0) return null;
        if (groups.Length == 1) return groups[0].Count >= 2 ? (byte)groups[0].Day : (byte?)null;
        return groups[0].Count > groups[1].Count ? (byte)groups[0].Day : (byte?)null;
    }

    // ----------------------------------------------------------------------
    // State — accept anything that uppercases to exactly 2 alpha chars.
    // Anything else (NULL, "California", "ca!") → NULL.
    // ----------------------------------------------------------------------
    internal static string? NormaliseState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length != 2) return null;
        var upper = trimmed.ToUpperInvariant();
        for (var i = 0; i < 2; i++)
        {
            if (upper[i] < 'A' || upper[i] > 'Z') return null;
        }
        return upper;
    }
}

/// <summary>Triple of inferred values; any field may be NULL.</summary>
public readonly record struct InferenceOutcome(decimal? Rent, byte? DueDay, string? State);
