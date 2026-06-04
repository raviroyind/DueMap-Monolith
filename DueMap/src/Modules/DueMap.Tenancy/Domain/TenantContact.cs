namespace DueMap.Tenancy.Domain;

/// <summary>
/// The deliverable contact details for a tenant on a lease. Sourced from the
/// synced customer record (QB/Xero) once accounting sync is wired up.
/// </summary>
public sealed record TenantContact(
    int LeaseId,
    string? Email,
    string? Phone,
    string? DisplayName);
