using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

public interface IPmAccountingDefaultsService
{
    /// <summary>
    /// Returns the PM's defaults, materializing a defaults row on first call so
    /// callers always have something to read against.
    /// </summary>
    Task<PmAccountingDefaults> GetOrCreateAsync(int propertyManagerId, CancellationToken ct);

    Task<PmAccountingDefaults> UpsertAsync(PmAccountingDefaults row, CancellationToken ct);
}
