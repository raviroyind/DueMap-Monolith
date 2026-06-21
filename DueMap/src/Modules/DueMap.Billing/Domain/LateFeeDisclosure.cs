using System.Text.Json;
using DueMap.Rules.Domain;

namespace DueMap.Billing.Domain;

/// <summary>
/// The evidence snapshot persisted on a <see cref="LateFeeAssessment"/>
/// (P1-6, §6.2). Captures the exact rule terms that produced a fee at the
/// moment it was assessed, so the record remains provable after the rule is
/// later versioned. Serialised to JSON into
/// <c>late_fee_assessments.disclosure_snapshot</c>.
/// </summary>
public sealed record LateFeeDisclosure(
    string StateCode,
    int StateRuleVersionId,
    int? LocalRuleOverrideId,
    string LateFeeType,
    decimal? PercentOfRent,
    decimal? FlatAmount,
    decimal? HardCapAmount,
    int GracePeriodDays,
    string SourceCitation,
    string? SourceUrl,
    string? PlainSummary,
    decimal MonthlyRent,
    decimal FeeAssessed,
    DateOnly AssessmentDate)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Build the snapshot from the resolved rule + the assessed fee.</summary>
    public static LateFeeDisclosure From(ResolvedRule rule, decimal monthlyRent, decimal feeAssessed) =>
        new(
            StateCode:           rule.StateCode,
            StateRuleVersionId:  rule.StateRuleVersionId,
            LocalRuleOverrideId: rule.LocalRuleOverrideId,
            LateFeeType:         rule.LateFeeType.ToString(),
            PercentOfRent:       rule.PercentOfRent,
            FlatAmount:          rule.FlatAmount,
            HardCapAmount:       rule.HardCapAmount,
            GracePeriodDays:     rule.GracePeriodDays,
            SourceCitation:      rule.SourceCitation,
            SourceUrl:           rule.SourceUrl,
            PlainSummary:        rule.PlainSummary,
            MonthlyRent:         monthlyRent,
            FeeAssessed:         feeAssessed,
            AssessmentDate:      rule.AssessmentDate);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
