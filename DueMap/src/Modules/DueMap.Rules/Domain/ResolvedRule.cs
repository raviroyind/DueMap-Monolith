namespace DueMap.Rules.Domain;

/// <summary>
/// The merged rule that applies to a specific lease on a specific assessment date.
/// Returned by <see cref="IRulesService.ResolveRuleAsync"/>. Contains both the
/// final effective values and provenance pointers so the caller (and any audit
/// surface) can prove which rule rows fed the result.
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
    string SourceCitation)
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
}
