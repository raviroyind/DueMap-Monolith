namespace DueMap.Rules.Domain;

public sealed class StateRuleVersion
{
    public int Id { get; set; }
    public int StateId { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpiresDate { get; set; }

    public int GracePeriodDays { get; set; }
    public LateFeeType LateFeeType { get; set; }

    public decimal? FlatAmount { get; set; }
    public decimal? PercentOfRent { get; set; }
    public decimal? HardCapAmount { get; set; }

    public bool NoticeRequiredBeforeFee { get; set; }
    public int? NoticeAdvanceDays { get; set; }

    public string SourceCitation { get; set; } = default!;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    // ---- §6.1 guardrail surface (v19, P1-2) -----------------------------
    // Distinct from the "recommended fee" fields above. These are absolute
    // ceilings the engine enforces against PM/lease overrides — the state
    // wins, always.

    /// <summary>Absolute ceiling for percent-of-rent fees. NULL = no % cap.</summary>
    public decimal? MaxPercent { get; set; }

    /// <summary>Absolute ceiling for flat-dollar fees. NULL = no flat cap.</summary>
    public decimal? MaxFlatAmount { get; set; }

    /// <summary>Legal floor for grace days; PM cannot configure below this.</summary>
    public byte MinGraceDays { get; set; }

    /// <summary>Whether the state allows daily-accrual late fees.</summary>
    public bool DailyAccrualOk { get; set; }

    /// <summary>Whether a written late-fee disclosure is required before assessment.</summary>
    public bool RequiresWrittenDisclosure { get; set; } = true;

    /// <summary>1 = hard-cap statute, 2 = "reasonableness" jurisdiction.</summary>
    public byte StandardKind { get; set; } = 1;

    /// <summary>Used by AutoSetup when <see cref="StandardKind"/> = 2.</summary>
    public decimal? SafeDefaultPct { get; set; }

    /// <summary>Plain-English summary shown to the PM in the rule editor.</summary>
    public string? PlainSummary { get; set; }

    /// <summary>Link to the statute / official source.</summary>
    public string? SourceUrl { get; set; }
}
