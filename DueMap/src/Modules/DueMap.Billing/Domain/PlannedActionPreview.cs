namespace DueMap.Billing.Domain;

/// <summary>
/// One row in a <see cref="DayPlanPreview"/> — exactly what would have
/// happened for one lease for one planned action under <see cref="ExecutionMode.DryRun"/>.
///
/// The shape is deliberately self-contained so the admin endpoint can
/// serialise it directly to JSON and a human reviewer (you, the PM, or QA)
/// can read the day's intent without joining anything.
/// </summary>
public sealed record PlannedActionPreview(
    int LeaseId,
    string? TenantDisplayName,
    /// <summary>e.g. "pre_due_reminder", "due_date_reminder", "grace_period_reminder", "late_fee_notice", "assess_late_fee".</summary>
    string ActionKindCode,
    /// <summary>"Email", "Sms", or "" for fee-assessment actions that don't dispatch anything.</summary>
    string Channel,
    /// <summary>Where the dispatch would have gone (email address or phone). Null for fee assessments.</summary>
    string? ToAddress,
    /// <summary>Fee amount for AssessLateFee. Null for send actions.</summary>
    decimal? Amount,
    /// <summary>The due date the planner attached the action to.</summary>
    DateOnly DueDate,
    /// <summary>Short string the reviewer can trace back: "template v=42" or "state rule v=7".</summary>
    string Provenance,
    /// <summary>The rendered email/SMS subject. Null for fee assessments.</summary>
    string? RenderedSubject,
    /// <summary>First ~140 chars of the rendered plain-text body. Null for fee assessments.</summary>
    string? RenderedFirstLine);

/// <summary>
/// Aggregate result of an <see cref="ExecutionMode.DryRun"/> for one PM
/// on one business date. Returned by the orchestrator and served as JSON
/// by the admin endpoint.
/// </summary>
public sealed record DayPlanPreview(
    int PropertyManagerId,
    DateOnly BusinessDate,
    int LeasesPlanned,
    int ActionsPreviewed,
    IReadOnlyList<PlannedActionPreview> Actions);
