namespace DueMap.Tenancy.Domain;

public sealed class Lease
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int StateId { get; set; }
    public int? JurisdictionId { get; set; }
    public int? CustomerId { get; set; }
    public decimal MonthlyRent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// PM-supplied unit identifier — e.g. "Apt 3B", "Unit 12". Free-form for v1.
    /// Imported from the PM's rent-roll spreadsheet; normalised properties +
    /// property_units table arrives in a future schema migration.
    /// </summary>
    public string? UnitLabel { get; set; }

    public DateTime CreatedAt { get; set; }

    // ---- Auto-discovery (P1-1) ------------------------------------------
    // Inference engine writes these after sync; the onboarding review screen
    // shows them as one-tap suggestions. NULL on these fields means the
    // engine couldn't decide (no invoices yet, ambiguous, etc.).
    public decimal? InferredRentAmount { get; set; }
    public byte? InferredDueDay { get; set; }
    public string? InferredState { get; set; }
    public DateTime? InferredAt { get; set; }
    public DateTime? DiscoveryConfirmedAt { get; set; }

    // ---- AutoSetup-written late-fee profile (P1-3, v20) -----------------
    // Persisted but NOT assessable while <see cref="FeesStaged"/> = true.
    // Filled by AutoSetupService from the state's safe defaults, clamped to
    // the state ceiling via ResolvedRule.ClampToStateMax.

    public byte? LateFeeType { get; set; }            // 1=Flat, 2=Percent, 3=GreaterOf, 4=LesserOf
    public decimal? LateFeePercent { get; set; }      // whole-number percent (e.g. 5.00 = 5%)
    public decimal? LateFeeFlatAmount { get; set; }
    public byte? LateFeeGraceDays { get; set; }
    public bool? LateFeeDailyAccrual { get; set; }

    /// <summary>
    /// When true, the late-fee profile above is recorded for review but the
    /// assessment planner will <strong>skip</strong> this lease entirely —
    /// nothing reminds, nothing assesses. Flipped to false by the explicit
    /// "Go live" action (P1-4). Default false for back-compat with leases
    /// created before AutoSetup landed.
    /// </summary>
    public bool FeesStaged { get; set; }
}
