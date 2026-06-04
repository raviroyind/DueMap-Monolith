namespace DueMap.Tenancy.Domain;

/// <summary>
/// Linear onboarding flag for a PM. The worker sweep only processes PMs at
/// <see cref="Active"/>; earlier states surface in the UI as the next step
/// the PM needs to complete. Status only advances — see
/// <c>IPropertyManagerWriter.SetMinimumStatusAsync</c>.
/// </summary>
public enum OnboardingStatus
{
    Registered = 1,    // PM record exists; no accounting connection yet
    Connected  = 2,    // OAuth tokens persisted
    Synced     = 3,    // First successful customer/invoice pull
    Active     = 4     // Ready to process — sweep job runs notices + late fees
}
