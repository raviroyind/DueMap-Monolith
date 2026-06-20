using DueMap.Rules.Domain;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P1-2 §6.1 guardrail enforcement. <c>ClampToStateMax</c> + <c>ClampGrace</c>
/// are the chokepoints AutoSetup (P1-3) and any future per-lease fee override
/// must go through. The whole product promise — "the PM cannot configure their
/// way into an illegal fee" — lives in these two methods.
///
/// All tests are pure: no DB, no DI, no fixture beyond the local helper.
/// </summary>
public sealed class ResolvedRuleGuardrailTests
{
    /// <summary>
    /// Build a rule with only the guardrail fields populated — the
    /// "recommended fee" math (flat / pct / hard-cap) is irrelevant to
    /// the guardrail tests so we leave them null.
    /// </summary>
    private static ResolvedRule Guardrail(
        decimal? maxPercent = null,
        decimal? maxFlat = null,
        byte minGrace = 0) =>
        new(
            StateCode: "CA",
            StateRuleVersionId: 1,
            LocalRuleOverrideId: null,
            AssessmentDate: new DateOnly(2026, 1, 1),
            GracePeriodDays: 0,
            LateFeeType: LateFeeType.Percent,
            FlatAmount: null,
            PercentOfRent: null,
            HardCapAmount: null,
            NoticeRequiredBeforeFee: false,
            NoticeAdvanceDays: null,
            SourceCitation: "test",
            StateMaxPercent:   maxPercent,
            StateMaxFlatAmount: maxFlat,
            StateMinGraceDays: minGrace);

    // ----------------------------------------------------------------------
    // ClampToStateMax — % ceiling.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clamp_pulls_a_too_high_proposed_fee_down_to_state_max_percent()
    {
        // CA caps at 5% of monthly rent. Rent = $1,000 → ceiling = $50.
        var rule = Guardrail(maxPercent: 5m);

        var clamped = rule.ClampToStateMax(proposedFee: 120m, monthlyRent: 1000m);

        Assert.Equal(50m, clamped);
    }

    [Fact]
    public void Clamp_returns_proposed_when_below_state_max_percent()
    {
        var rule = Guardrail(maxPercent: 10m);  // 10% of $1,000 = $100 ceiling

        var clamped = rule.ClampToStateMax(proposedFee: 35m, monthlyRent: 1000m);

        Assert.Equal(35m, clamped);
    }

    // ----------------------------------------------------------------------
    // ClampToStateMax — flat ceiling.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clamp_pulls_a_too_high_proposed_fee_down_to_state_max_flat_amount()
    {
        // Some states cap flat at $50 regardless of rent.
        var rule = Guardrail(maxFlat: 50m);

        var clamped = rule.ClampToStateMax(proposedFee: 75m, monthlyRent: 1500m);

        Assert.Equal(50m, clamped);
    }

    // ----------------------------------------------------------------------
    // ClampToStateMax — both ceilings present → stricter wins.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clamp_uses_the_stricter_ceiling_when_both_caps_are_present()
    {
        // 5% of $1,000 = $50; flat cap = $40 → flat is stricter.
        var rule = Guardrail(maxPercent: 5m, maxFlat: 40m);

        var clamped = rule.ClampToStateMax(proposedFee: 200m, monthlyRent: 1000m);

        Assert.Equal(40m, clamped);
    }

    // ----------------------------------------------------------------------
    // Back-compat: existing seeds carry NULL on both ceilings.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clamp_passes_through_unchanged_when_neither_cap_is_set()
    {
        var rule = Guardrail();  // both nulls

        var clamped = rule.ClampToStateMax(proposedFee: 999m, monthlyRent: 1000m);

        Assert.Equal(999m, clamped);
    }

    // ----------------------------------------------------------------------
    // ClampGrace — floor.
    // ----------------------------------------------------------------------

    [Fact]
    public void Clamp_grace_floors_requested_days_to_state_minimum()
    {
        // NY mandates 5-day grace; PM requested 2 → floor at 5.
        var rule = Guardrail(minGrace: 5);

        Assert.Equal(5, rule.ClampGrace(requestedDays: 2));
    }

    [Fact]
    public void Clamp_grace_passes_through_when_requested_is_above_floor()
    {
        var rule = Guardrail(minGrace: 3);

        Assert.Equal(7, rule.ClampGrace(requestedDays: 7));
    }

    [Fact]
    public void Clamp_grace_is_zero_when_no_floor_and_no_request()
    {
        var rule = Guardrail();  // minGrace = 0

        Assert.Equal(0, rule.ClampGrace(requestedDays: 0));
    }
}
