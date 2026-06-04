using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy.Services;

/// <summary>
/// Read/write surface for <see cref="PmDailyCloseSettings"/>. Captured during
/// onboarding step 2 and editable later from workspace settings.
/// </summary>
public interface IPmDailyCloseSettingsService
{
    /// <summary>
    /// Returns the PM's settings or a sensible default if no row exists yet
    /// (caller will then either show defaults in the form, or persist them
    /// via <see cref="UpsertAsync"/>).
    /// </summary>
    Task<PmDailyCloseSettings> GetOrDefaultAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Inserts or updates the PM's row AND mirrors the timezone onto
    /// <c>PropertyManager.TimeZoneId</c> so the worker's per-PM sweep
    /// scheduler stays in sync without a separate write.
    /// </summary>
    Task UpsertAsync(PmDailyCloseSettings settings, CancellationToken ct);
}
