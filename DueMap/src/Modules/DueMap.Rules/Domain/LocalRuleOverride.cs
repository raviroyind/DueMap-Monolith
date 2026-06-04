namespace DueMap.Rules.Domain;

public sealed class LocalRuleOverride
{
    public int Id { get; set; }
    public int JurisdictionId { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpiresDate { get; set; }

    public int? GracePeriodDays { get; set; }
    public decimal? FlatAmount { get; set; }
    public decimal? PercentOfRent { get; set; }
    public decimal? HardCapAmount { get; set; }
    public int? NoticeAdvanceDays { get; set; }

    public string SourceCitation { get; set; } = default!;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}
