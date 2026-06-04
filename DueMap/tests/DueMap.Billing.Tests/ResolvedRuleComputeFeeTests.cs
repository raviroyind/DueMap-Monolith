using DueMap.Rules.Domain;
using Xunit;

namespace DueMap.Billing.Tests;

public sealed class ResolvedRuleComputeFeeTests
{
    private static ResolvedRule Rule(
        LateFeeType type,
        decimal? flat = null,
        decimal? percent = null,
        decimal? hardCap = null) =>
        new(
            StateCode: "CA",
            StateRuleVersionId: 1,
            LocalRuleOverrideId: null,
            AssessmentDate: new DateOnly(2026, 1, 1),
            GracePeriodDays: 5,
            LateFeeType: type,
            FlatAmount: flat,
            PercentOfRent: percent,
            HardCapAmount: hardCap,
            NoticeRequiredBeforeFee: false,
            NoticeAdvanceDays: null,
            SourceCitation: "test");

    [Fact]
    public void Flat_returns_flat_amount()
    {
        var r = Rule(LateFeeType.Flat, flat: 50m);
        Assert.Equal(50m, r.ComputeFee(2000m));
    }

    [Fact]
    public void Flat_with_null_returns_zero()
    {
        var r = Rule(LateFeeType.Flat);
        Assert.Equal(0m, r.ComputeFee(2000m));
    }

    [Fact]
    public void Percent_returns_percent_of_rent()
    {
        var r = Rule(LateFeeType.Percent, percent: 0.05m);
        Assert.Equal(100m, r.ComputeFee(2000m));
    }

    [Fact]
    public void GreaterOf_returns_higher_of_flat_and_percent()
    {
        // 5% of 2000 = 100 > $50 → 100 wins
        var a = Rule(LateFeeType.GreaterOf, flat: 50m, percent: 0.05m);
        Assert.Equal(100m, a.ComputeFee(2000m));

        // 1% of 2000 = 20 < $50 → 50 wins
        var b = Rule(LateFeeType.GreaterOf, flat: 50m, percent: 0.01m);
        Assert.Equal(50m, b.ComputeFee(2000m));
    }

    [Fact]
    public void LesserOf_returns_lower_of_flat_and_percent()
    {
        // 5% of 2000 = 100 vs $50 → 50 wins
        var a = Rule(LateFeeType.LesserOf, flat: 50m, percent: 0.05m);
        Assert.Equal(50m, a.ComputeFee(2000m));

        // 5% of 200 = 10 vs $50 → 10 wins
        var b = Rule(LateFeeType.LesserOf, flat: 50m, percent: 0.05m);
        Assert.Equal(10m, b.ComputeFee(200m));
    }

    [Fact]
    public void HardCap_clamps_a_percent_fee_above_the_cap()
    {
        // 10% of 2000 = 200, capped at 75
        var r = Rule(LateFeeType.Percent, percent: 0.10m, hardCap: 75m);
        Assert.Equal(75m, r.ComputeFee(2000m));
    }

    [Fact]
    public void HardCap_leaves_fees_below_the_cap_unchanged()
    {
        var r = Rule(LateFeeType.Percent, percent: 0.05m, hardCap: 200m);
        Assert.Equal(100m, r.ComputeFee(2000m));
    }

    [Fact]
    public void Negative_rent_throws()
    {
        var r = Rule(LateFeeType.Flat, flat: 50m);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.ComputeFee(-1m));
    }

    [Fact]
    public void Zero_rent_yields_zero_for_percent()
    {
        var r = Rule(LateFeeType.Percent, percent: 0.05m);
        Assert.Equal(0m, r.ComputeFee(0m));
    }
}
