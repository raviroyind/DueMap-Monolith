using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Per-lease notice configuration. Designed to back a multi-select grid in the
/// PM admin UI: <see cref="ListForPropertyManagerAsync"/> hydrates the grid;
/// <see cref="BulkUpsertAsync"/> applies the same change across many leases in
/// one round-trip when the PM selects rows and clicks "apply".
/// </summary>
public interface ILeaseNoticeSettingsService
{
    Task<LeaseNoticeSettings?> GetAsync(int leaseId, CancellationToken ct);

    Task<IReadOnlyList<LeaseNoticeSettings>> ListForPropertyManagerAsync(
        int propertyManagerId,
        CancellationToken ct);

    Task<LeaseNoticeSettings> UpsertAsync(LeaseNoticeSettings settings, CancellationToken ct);

    /// <summary>
    /// Apply the supplied <paramref name="patch"/> to every lease in
    /// <paramref name="leaseIds"/>. Only fields set on the patch are written;
    /// see <see cref="LeaseNoticeSettingsPatch"/>.
    /// </summary>
    Task<int> BulkApplyAsync(
        IReadOnlyCollection<int> leaseIds,
        LeaseNoticeSettingsPatch patch,
        CancellationToken ct);
}

/// <summary>
/// Partial update for bulk-apply. Any property left at its <c>null</c> default
/// is "no change". Use the explicit <c>Clear*</c> flags to write a NULL into a
/// nullable column (i.e. "go back to the PM default for this field").
/// </summary>
public sealed record LeaseNoticeSettingsPatch
{
    public bool? PreDueEnabled { get; init; }
    public int? PreDueDaysBefore { get; init; }
    public bool ClearPreDueDaysBefore { get; init; }

    public bool? DueDateEnabled { get; init; }

    public bool? PostDueEnabled { get; init; }
    public PostDueMode? PostDueMode { get; init; }
    public bool ClearPostDueMode { get; init; }

    public int? PostDueGraceDays { get; init; }
    public bool ClearPostDueGraceDays { get; init; }
}
