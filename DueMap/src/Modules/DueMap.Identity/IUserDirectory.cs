namespace DueMap.Identity;

/// <summary>
/// Read-only lookups across the Identity user set, scoped by property manager.
/// The Daily Close Report worker uses this to find the email address(es)
/// to send to — Tenancy doesn't store PM contact emails (intentionally, they
/// belong with the user identity), so this is the cross-context shim.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// The primary contact email for a PM. Returns the email of the first
    /// active user whose PmId claim matches — for single-user orgs this is
    /// trivially the only user; for multi-user orgs (future), a per-PM
    /// "primary contact" flag would replace this naive "first" pick.
    /// Returns null if no user is found for the PM.
    /// </summary>
    Task<string?> GetPrimaryEmailForPmAsync(int propertyManagerId, CancellationToken ct);
}
