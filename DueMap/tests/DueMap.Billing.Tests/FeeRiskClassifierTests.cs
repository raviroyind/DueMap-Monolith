using DueMap.Billing.Tenants;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P1-5 fee-risk badge. Pure classification — mirrors the §6 guardrail
/// (fee vs state cap) for the unified Tenants grid.
/// </summary>
public sealed class FeeRiskClassifierTests
{
    [Fact]
    public void No_state_is_review()
    {
        Assert.Equal(FeeRisk.Review, FeeRiskClassifier.Classify(leaseFeePercent: 5m, stateMaxPercent: 5m, stateKnown: false));
    }

    [Fact]
    public void State_known_but_no_fee_is_unknown()
    {
        Assert.Equal(FeeRisk.Unknown, FeeRiskClassifier.Classify(leaseFeePercent: null, stateMaxPercent: 5m, stateKnown: true));
    }

    [Fact]
    public void Fee_within_cap_is_ok()
    {
        Assert.Equal(FeeRisk.Ok, FeeRiskClassifier.Classify(leaseFeePercent: 4m, stateMaxPercent: 5m, stateKnown: true));
    }

    [Fact]
    public void Fee_equal_to_cap_is_ok()
    {
        Assert.Equal(FeeRisk.Ok, FeeRiskClassifier.Classify(leaseFeePercent: 5m, stateMaxPercent: 5m, stateKnown: true));
    }

    [Fact]
    public void Fee_above_cap_is_over_cap()
    {
        Assert.Equal(FeeRisk.OverCap, FeeRiskClassifier.Classify(leaseFeePercent: 8m, stateMaxPercent: 5m, stateKnown: true));
    }

    [Fact]
    public void State_known_with_no_percent_cap_is_ok()
    {
        // Flat-only or reasonableness states carry no % ceiling — can't prove an overage.
        Assert.Equal(FeeRisk.Ok, FeeRiskClassifier.Classify(leaseFeePercent: 12m, stateMaxPercent: null, stateKnown: true));
    }
}
