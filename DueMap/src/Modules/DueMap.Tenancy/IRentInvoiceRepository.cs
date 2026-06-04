using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Identity of a rent invoice in the connected accounting system —
/// the pair needed to fetch the invoice PDF via the provider's API.
/// </summary>
public sealed record RentInvoiceExternalRef(
    ExternalAccountingProvider Provider,
    string ExternalId,
    string? ExternalDocNumber);

public interface IRentInvoiceRepository
{
    Task<RentInvoice?> GetByExternalAsync(
        int propertyManagerId,
        ExternalAccountingProvider provider,
        string externalId,
        CancellationToken ct);

    /// <summary>
    /// The most relevant due date for a lease as of an assessment date:
    /// the latest open invoice whose due_date is &lt;= asOf, or the earliest
    /// open invoice with a future due_date if none have arrived yet. Returns
    /// <c>null</c> if no invoice is linked to the lease.
    /// </summary>
    Task<DateOnly?> GetCurrentDueDateAsync(int leaseId, DateOnly asOf, CancellationToken ct);

    /// <summary>
    /// Public pay-by-link URL on the same "current invoice" the planner uses.
    /// Selection rule mirrors <see cref="GetCurrentDueDateAsync"/>. Returns
    /// <c>null</c> if no invoice is linked, the URL wasn't captured during
    /// sync, or the PM hasn't enabled online payments on their provider.
    /// </summary>
    Task<string?> GetCurrentPayUrlAsync(int leaseId, DateOnly asOf, CancellationToken ct);

    /// <summary>
    /// Provider + external id of the "current" invoice for the lease, using
    /// the same selection rule as <see cref="GetCurrentDueDateAsync"/>. Used
    /// by the late-fee notice flow to fetch the invoice PDF directly from
    /// the connected accounting system. Returns null when no invoice is
    /// linked to the lease yet.
    /// </summary>
    Task<RentInvoiceExternalRef?> GetCurrentExternalAsync(int leaseId, DateOnly asOf, CancellationToken ct);

    /// <summary>
    /// Single invoice by primary key. Caller validates tenant scoping (e.g.
    /// the tenant portal checks that <see cref="RentInvoice.CustomerId"/>
    /// matches the signed-in tenant).
    /// </summary>
    Task<RentInvoice?> GetByIdAsync(int invoiceId, CancellationToken ct);

    Task<RentInvoice> UpsertAsync(RentInvoice invoice, CancellationToken ct);

    /// <summary>
    /// All invoices for a PM, newest due-date first. Used by the dashboard
    /// counters and the /invoices drill-down grid. Cheap as long as a PM has
    /// thousands not millions of rows; if that changes, paginate.
    /// </summary>
    Task<IReadOnlyList<RentInvoice>> ListByPropertyManagerAsync(int propertyManagerId, CancellationToken ct);
}
