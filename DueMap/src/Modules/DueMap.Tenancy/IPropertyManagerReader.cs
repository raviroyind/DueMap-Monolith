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

    /// <summary>
    /// Raw <c>auto_setup_summary</c> JSON for the P1-4 review screen, or null
    /// if AutoSetup hasn't run for this PM. The caller deserializes to
    /// <c>DueMap.Billing.AutoSetup.AutoSetupSummary</c> (Tenancy can't
    /// reference Billing, so this stays a string here).
    /// </summary>
    Task<string?> GetAutoSetupSummaryJsonAsync(int propertyManagerId, CancellationToken ct);
}
