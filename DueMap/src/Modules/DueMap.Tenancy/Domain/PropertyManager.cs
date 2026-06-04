namespace DueMap.Tenancy.Domain;

public sealed class PropertyManager
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Linear onboarding state — only the Active value gates processing.</summary>
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.Registered;

    /// <summary>
    /// IANA timezone id (e.g. "America/Los_Angeles"). The worker uses this to
    /// determine the PM's local midnight for the daily sweep. Defaults to "UTC".
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    public DateTime CreatedAt { get; set; }

    // ---- Multi-step onboarding completion timestamps ------------------------
    // Set the first time the PM finishes each step. Cleared = step pending.
    // The kiosk router uses these to resume the PM at their first incomplete
    // step. See db/duemap_schema_v12_onboarding_multistep.sql.

    /// <summary>Set when the PM completes step 1 — Connect Accounting System.</summary>
    public DateTime? StepConnectDoneAt { get; set; }

    /// <summary>Set when the PM completes step 2 — Daily Close Settings.</summary>
    public DateTime? StepCloseDoneAt { get; set; }

    /// <summary>Set when the PM completes step 3 — Notice Preferences (v13).</summary>
    public DateTime? StepNoticePrefsDoneAt { get; set; }

    /// <summary>Set when the PM completes step 4 — Preflight Tenant Notifications.</summary>
    public DateTime? StepPreflightDoneAt { get; set; }

    /// <summary>True when all four onboarding steps have been completed.</summary>
    public bool OnboardingComplete =>
        StepConnectDoneAt     is not null &&
        StepCloseDoneAt       is not null &&
        StepNoticePrefsDoneAt is not null &&
        StepPreflightDoneAt   is not null;
}
