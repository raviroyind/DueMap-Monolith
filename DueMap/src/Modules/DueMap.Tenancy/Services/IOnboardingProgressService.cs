using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy.Services;

/// <summary>
/// The four onboarding steps in the order a PM walks through them.
/// Numeric values double as the position used by the stepper UI.
///
/// NOTE: Preflight is intentionally numbered 4, not 3, so the values match
/// the visual step index. If you add a new step, renumber and add a
/// schema migration that back-fills the new timestamp column.
/// </summary>
public enum OnboardingStep
{
    Connect          = 1,
    CloseSettings    = 2,
    NoticePreferences = 3,
    Preflight        = 4
}

/// <summary>
/// Snapshot of a single PM's onboarding progress, ready to drive both the
/// kiosk router and the stepper UI without further DB hits.
/// </summary>
public sealed record OnboardingProgress(
    DateTime? ConnectDoneAt,
    DateTime? CloseDoneAt,
    DateTime? NoticePrefsDoneAt,
    DateTime? PreflightDoneAt)
{
    public bool IsComplete =>
        ConnectDoneAt     is not null &&
        CloseDoneAt       is not null &&
        NoticePrefsDoneAt is not null &&
        PreflightDoneAt   is not null;

    /// <summary>
    /// The first step that still needs to happen, or null when onboarding is
    /// complete. The kiosk router redirects to this step.
    /// </summary>
    public OnboardingStep? NextStep =>
        ConnectDoneAt     is null ? OnboardingStep.Connect
      : CloseDoneAt       is null ? OnboardingStep.CloseSettings
      : NoticePrefsDoneAt is null ? OnboardingStep.NoticePreferences
      : PreflightDoneAt   is null ? OnboardingStep.Preflight
      : null;

    public bool IsDone(OnboardingStep step) => step switch
    {
        OnboardingStep.Connect           => ConnectDoneAt     is not null,
        OnboardingStep.CloseSettings     => CloseDoneAt       is not null,
        OnboardingStep.NoticePreferences => NoticePrefsDoneAt is not null,
        OnboardingStep.Preflight         => PreflightDoneAt   is not null,
        _ => false
    };
}

/// <summary>
/// Reads and advances a PM's multi-step onboarding state. The router asks
/// this on every navigation; the step pages call MarkXDoneAsync once the
/// step's local form has been persisted.
/// </summary>
public interface IOnboardingProgressService
{
    Task<OnboardingProgress> GetAsync(int propertyManagerId, CancellationToken ct);

    Task MarkConnectDoneAsync       (int propertyManagerId, CancellationToken ct);
    Task MarkCloseDoneAsync         (int propertyManagerId, CancellationToken ct);
    Task MarkNoticePrefsDoneAsync   (int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Marks the preflight step done AND advances <see cref="OnboardingStatus"/>
    /// to <c>Active</c> — this is the moment the PM officially goes live.
    /// </summary>
    Task MarkPreflightDoneAsync(int propertyManagerId, CancellationToken ct);
}
