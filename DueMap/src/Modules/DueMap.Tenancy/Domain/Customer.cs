namespace DueMap.Tenancy.Domain;

/// <summary>
/// A tenant, synced from the PM's QB Customer or Xero Contact record.
/// Provenance lives in the <see cref="ExternalProvider"/> + <see cref="ExternalId"/>
/// pair, which is UNIQUE per PM.
/// </summary>
public sealed class Customer
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public ExternalAccountingProvider ExternalProvider { get; set; }
    public string ExternalId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Timestamp at which the PM sent this customer the one-time preflight
    /// "we use DueMap to remind you of upcoming rent" email during onboarding
    /// step 3. NULL = not yet notified. The grid on /onboarding/preflight
    /// uses this to filter "already done" vs "still to send."
    /// </summary>
    public DateTime? PreflightNotifiedAt { get; set; }
}
