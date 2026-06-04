namespace DueMap.Tenancy.Domain;

/// <summary>
/// Read-side projection for the mapping UI: a lease plus its currently-linked
/// customer (if any). When <see cref="CustomerId"/> is null the lease has not
/// been linked yet — usually because auto-link couldn't pick exactly one
/// active lease for the customer.
/// </summary>
public sealed record LeaseWithCustomer(
    int LeaseId,
    decimal MonthlyRent,
    DateOnly StartDate,
    int? CustomerId,
    string? CustomerDisplayName);
