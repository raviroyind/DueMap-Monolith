using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Reads and writes the PM-level master toggles and defaults. Always returns
/// a row — if none exists, a defaults row is materialized and persisted on
/// first read so the orchestrator has stable values to merge with.
/// </summary>
public interface IPmNoticePreferencesService
{
    Task<PmNoticePreferences> GetOrCreateAsync(int propertyManagerId, CancellationToken ct);

    Task<PmNoticePreferences> UpdateAsync(PmNoticePreferences prefs, CancellationToken ct);
}
