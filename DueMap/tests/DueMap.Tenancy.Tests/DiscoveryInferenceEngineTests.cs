using DueMap.Tenancy.Discovery;
using Xunit;

namespace DueMap.Tenancy.Tests;

/// <summary>
/// Pure-logic tests for the inference engine. No DI, no DB, no clock —
/// every test is &lt; 1ms and deterministic. The fixtures cover the
/// scenarios the roadmap prompt called out: clear majority, tied
/// majority (= ambiguous), ambiguous state.
/// </summary>
public sealed class DiscoveryInferenceEngineTests
{
    // ----------------------------------------------------------------------
    // Clear majority on all three signals → all three populated.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clear_majority_infers_rent_due_day_and_state()
    {
        var amounts = new decimal[] { 1200m, 1200m, 1200m, 1200m, 1200m, 750m };  // mode = 1200
        var dueDays = new[] { 1, 1, 1, 1, 1, 15 };                                 // mode = 1
        var state = "CA";

        var outcome = DiscoveryInferenceEngine.Infer(amounts, dueDays, state);

        Assert.Equal(1200m, outcome.Rent);
        Assert.Equal((byte)1, outcome.DueDay);
        Assert.Equal("CA", outcome.State);
    }

    // ----------------------------------------------------------------------
    // Penny-level variation in rent collapses to whole-dollar buckets.
    // ----------------------------------------------------------------------

    [Fact]
    public void Penny_variation_rounds_to_same_dollar_bucket()
    {
        var amounts = new decimal[] { 1200.00m, 1200.05m, 1199.98m, 1200.10m };

        var outcome = DiscoveryInferenceEngine.Infer(amounts, Array.Empty<int>(), null);

        Assert.Equal(1200m, outcome.Rent);
    }

    // ----------------------------------------------------------------------
    // Tied rent → ambiguous (= NULL). Better to ask than to coin-flip.
    // ----------------------------------------------------------------------

    [Fact]
    public void Tied_rent_returns_null()
    {
        var amounts = new decimal[] { 1200m, 1200m, 950m, 950m };

        var outcome = DiscoveryInferenceEngine.Infer(amounts, Array.Empty<int>(), null);

        Assert.Null(outcome.Rent);
    }

    // ----------------------------------------------------------------------
    // Single invoice ≠ "recurring" → rent NULL.
    // ----------------------------------------------------------------------

    [Fact]
    public void Single_invoice_is_not_enough_evidence_for_rent()
    {
        var amounts = new decimal[] { 1200m };

        var outcome = DiscoveryInferenceEngine.Infer(amounts, Array.Empty<int>(), null);

        Assert.Null(outcome.Rent);
    }

    // ----------------------------------------------------------------------
    // Tied due day → ambiguous.
    // ----------------------------------------------------------------------

    [Fact]
    public void Tied_due_day_returns_null()
    {
        var dueDays = new[] { 1, 1, 15, 15 };

        var outcome = DiscoveryInferenceEngine.Infer(Array.Empty<decimal>(), dueDays, null);

        Assert.Null(outcome.DueDay);
    }

    // ----------------------------------------------------------------------
    // The ambiguous-state cases the roadmap called out explicitly:
    //   - null state
    //   - non-2-letter ("California", "ca!")
    //   - mixed-case / padded input gets normalised
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("California")]
    [InlineData("c")]
    [InlineData("ca!")]
    [InlineData("12")]
    public void Ambiguous_state_returns_null(string? raw)
    {
        var outcome = DiscoveryInferenceEngine.Infer(
            Array.Empty<decimal>(), Array.Empty<int>(), raw);

        Assert.Null(outcome.State);
    }

    [Theory]
    [InlineData("ca", "CA")]
    [InlineData(" CA ", "CA")]
    [InlineData("ny", "NY")]
    public void Mixed_case_state_gets_normalised(string raw, string expected)
    {
        var outcome = DiscoveryInferenceEngine.Infer(
            Array.Empty<decimal>(), Array.Empty<int>(), raw);

        Assert.Equal(expected, outcome.State);
    }

    // ----------------------------------------------------------------------
    // Day-of-month clamp — a "30th of a 31-day month" stamp lands in
    // the same bucket as "30 in a 30-day month."
    // ----------------------------------------------------------------------

    [Fact]
    public void Due_day_clamps_into_one_to_twenty_eight_window()
    {
        // 30 and 31 both clamp to 28; both 28s also stay 28 — mode = 28.
        var dueDays = new[] { 30, 31, 28, 28 };

        var outcome = DiscoveryInferenceEngine.Infer(
            Array.Empty<decimal>(), dueDays, null);

        Assert.Equal((byte)28, outcome.DueDay);
    }

    // ----------------------------------------------------------------------
    // No data at all → all NULLs.
    // ----------------------------------------------------------------------

    [Fact]
    public void Empty_input_returns_all_nulls()
    {
        var outcome = DiscoveryInferenceEngine.Infer(
            Array.Empty<decimal>(), Array.Empty<int>(), null);

        Assert.Null(outcome.Rent);
        Assert.Null(outcome.DueDay);
        Assert.Null(outcome.State);
    }
}
