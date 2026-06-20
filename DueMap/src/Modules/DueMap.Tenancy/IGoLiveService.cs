namespace DueMap.Tenancy;

/// <summary>
/// The explicit "Go live" transition (P1-4). This is the single moment the
/// PM's staged late-fee profiles become assessable:
/// <list type="number">
///   <item>every staged lease (<c>FeesStaged = true</c>) is flipped to
///   <c>false</c> — the assessment planner stops skipping it;</item>
///   <item>the PM's <see cref="Domain.OnboardingStatus"/> is advanced to
///   <c>Active</c> (never regressed).</item>
/// </list>
///
/// <para><strong>Idempotent.</strong> Re-running after the PM is already live
/// is a no-op (0 leases activated, <c>WasAlreadyActive = true</c>).</para>
///
/// <para>Deliberately separate from <c>MarkPreflightDoneAsync</c>: in the v2
/// review flow the intro email and "Go live" are distinct steps, and fees
/// must NOT become live until the PM clicks Go live — even though the PM may
/// already be <c>Active</c>, the staged gate keeps every lease silent until
/// this call.</para>
/// </summary>
public interface IGoLiveService
{
    Task<GoLiveResult> GoLiveAsync(int propertyManagerId, CancellationToken ct);
}

/// <param name="LeasesActivated">How many staged leases were flipped live by this call.</param>
/// <param name="WasAlreadyActive">True if the PM was already at Active status before this call.</param>
public sealed record GoLiveResult(int LeasesActivated, bool WasAlreadyActive);
