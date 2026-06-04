using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Read-only PM lookup. The dev-mode UI uses this to populate a PM picker;
/// once Identity is wired, callers identify the PM via claims and most callers
/// won't need this anymore.
/// </summary>
public interface IPropertyManagerReader
{
    Task<IReadOnlyList<PropertyManager>> ListAllAsync(CancellationToken ct);

    Task<PropertyManager?> GetAsync(int id, CancellationToken ct);
}
