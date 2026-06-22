namespace DueMap.Rules.Domain;

/// <summary>
/// The merged rule that applies to a specific lease on a specific assessment date.
/// Returned by <see cref="IRulesService.ResolveRuleAsync"/>. Contains both the
/// final effective values and provenance pointers so the caller (and any audit
/// surface) can prove which rule rows fed the result.
///
/// <para><strong>§6.1 guardrails (v19, P1-2).</strong> The
/// <c>StateMax*</c> and <c>StateMinGraceDays</c> fields are absolute
/// ceilings/floors enforced regardless of PM or lease overrides — the
/// state always wins. AutoSetup and any future PM-supplied fee override
/// MUST go through <see cref="ClampToStateMax"/> and
/// <see cref="ClampGrace"/> before persisting.</para>
/// </summary>
public sealed record ResolvedRule(
    string StateCode,
    int StateRuleVersionId,
    int? LocalRuleOverrideId,
    DateOnly AssessmentDate,
    int GracePeriodDays,
    LateFeeType LateFeeType,
    decimal? FlatAmount,
    decimal? PercentOfRent,
    decimal? HardCapAmount,
    bool NoticeRequiredBeforeFee,
    int? NoticeAdvanceDays,
    string SourceCitation,
    // ---- §6.1 guardrail surface ----
    decimal? StateMaxPercent          = null,
    decimal? StateMaxFlatAmount       = null,
    byte     StateMinGraceDays        = 0,
    bool     DailyAccrualOk           = false,
    bool     RequiresWrittenDisclosure = true,
    byte     StandardKind             = 1,
    decimal? SafeDefaultPct           = null,
    string?  PlainSummary             = null,
    string?  SourceUrl                = null)
{
    /// <summary>
    /// Apply this rule to a rent amount and return the late fee that should
    /// be assessed (already clamped to the hard cap if present).
    /// </summary>
    public decimal ComputeFee(decimal monthlyRent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyRent);

        var raw = LateFeeType switch
        {
            LateFeeType.Flat       => FlatAmount    ?? 0m,
            LateFeeType.Percent    => (PercentOfRent ?? 0m) * monthlyRent,
            LateFeeType.GreaterOf  => Math.Max(FlatAmount ?? 0m, (PercentOfRent ?? 0m) * monthlyRent),
            LateFeeType.LesserOf   => Math.Min(FlatAmount ?? decimal.MaxValue, (PercentOfRent ?? 0m) * monthlyRent),
            _ => 0m
        };

        return HardCapAmount.HasValue ? Math.Min(raw, HardCapAmount.Value) : raw;
    }

    /// <summary>
    /// Clamp a PM-supplied or AutoSetup-supplied late fee to the state's
    /// absolute ceiling. Used by P1-3 AutoSetup (and any future per-lease
    /// fee override) so a PM <strong>cannot</strong> configure their way
    /// into an illegal fee.
    ///
    /// <para>Both <see cref="StateMaxPercent"/> and
    /// <see cref="StateMaxFlatAmount"/> are evaluated; whichever produces
    /// the LOWER ceiling wins. <c>NULL</c> means "no cap from that
    /// dimension" — when both are null the proposed fee is returned
    /// unchanged (back-compat: existing seeds don't carry the new
    /// guardrail data yet).</para>
    /// </summary>
    /// <param name="proposedFee">The fee the caller wants to assess.</param>
    /// <param name="monthlyRent">Used to convert <see cref="StateMaxPercent"/> to a dollar ceiling.</param>
    public decimal ClampToStateMax(decimal proposedFee, decimal monthlyRent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(proposedFee);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyRent);

        var ceiling = decimal.MaxValue;
        if (StateMaxPercent is decimal pct)
        {
            // max_percent is stored as a whole number (e.g. 5.00 = 5%).
            ceiling = Math.Min(ceiling, pct / 100m * monthlyRent);
        }
        if (StateMaxFlatAmount is decimal flat)
        {
            ceiling = Math.Min(ceiling, flat);
        }
        return ceiling == decimal.MaxValue ? proposedFee : Math.Min(proposedFee, ceiling);
    }

    /// <summary>
    /// Floor a PM-supplied or AutoSetup-supplied grace value to the
    /// state's <see cref="StateMinGraceDays"/>. Mirrors the existing
    /// <c>EffectivePolicyService</c> logic — exposed here as a static
    /// chokepoint so the AutoSetup engine can reuse it without
    /// instantiating EffectivePolicyService.
    /// </summary>
    public int ClampGrace(int requestedDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestedDays);
        return Math.Max(requestedDays, StateMinGraceDays);
    }
}
