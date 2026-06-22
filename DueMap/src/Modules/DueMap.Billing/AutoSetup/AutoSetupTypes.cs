namespace DueMap.Billing.AutoSetup;

// =============================================================================
// AutoSetup shared types (P1-3).
// One file so the surface stays browsable; each record stays small.
// =============================================================================

/// <summary>
/// One actionable issue surfaced by <see cref="IComplianceScanService"/>.
/// Renders directly to the review screen's "What needs you" list.
/// </summary>
/// <param name="LeaseId">Which lease the finding applies to.</param>
/// <param name="Kind">Stable code so the UI can render an icon / link.</param>
/// <param name="Message">Human-readable, plain English. NO legal advice.</param>
/// <param name="CurrentValue">What AutoSetup wrote (or NULL if it skipped).</param>
/// <param name="CompliantFix">What the PM should review/confirm.</param>
public sealed record ComplianceFinding(
    int LeaseId,
    ComplianceFindingKind Kind,
    string Message,
    string? CurrentValue,
    string? CompliantFix);

public enum ComplianceFindingKind
{
    /// <summary>The lease has no state set + no inferred_state — safe default used.</summary>
    AmbiguousState              = 1,
    /// <summary>State is a "reasonableness" jurisdiction (<c>standard_kind = 2</c>) — safe default used.</summary>
    ReasonablenessJurisdiction  = 2,
    /// <summary>Defensive belt-and-braces: a fee landed above the cap (shouldn't happen post-clamp).</summary>
    FeeExceedsCap               = 3,
    /// <summary>No <see cref="ResolvedRule"/> found for the lease's state — nothing was written.</summary>
    NoStateRuleFound            = 4
}

/// <summary>
/// The serialised result of one AutoSetup run, stored on
/// <c>property_managers.auto_setup_summary</c>. Drives the review screen
/// (P1-4) — keep stable across versions.
/// </summary>
public sealed record AutoSetupSummary(
    int LeasesProcessed,
    int LeasesSkipped,
    DateTime RanAtUtc,
    IReadOnlyList<ComplianceFinding> Findings);
