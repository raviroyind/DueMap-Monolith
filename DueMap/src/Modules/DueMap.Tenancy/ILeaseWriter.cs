using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Lease creation surface. QB and Xero have no concept of a "lease" — that's
/// PM-domain knowledge — so leases are created either via the manual UI or
/// (future) the QB-invoice-pattern detector that proposes them.
/// </summary>
public interface ILeaseWriter
{
    Task<Lease> CreateAsync(NewLeaseInput input, CancellationToken ct);
}

/// <summary>
/// Caller-supplied data for a brand-new lease row. The writer validates
/// (PM owns the customer; state exists; rent positive) and stamps CreatedAt.
/// </summary>
public sealed record NewLeaseInput(
    int PropertyManagerId,
    int StateId,
    int? CustomerId,
    decimal MonthlyRent,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? UnitLabel = null);
