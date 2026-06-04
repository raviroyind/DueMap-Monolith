using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

public interface ICustomerRepository
{
    Task<Customer?> GetByExternalAsync(
        int propertyManagerId,
        ExternalAccountingProvider provider,
        string externalId,
        CancellationToken ct);

    Task<IReadOnlyList<Customer>> ListByPropertyManagerAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Single customer by primary key. Returns null if not found. Callers are
    /// expected to validate ownership (e.g. by checking PropertyManagerId on
    /// the returned row) before showing data — this method itself doesn't
    /// scope by PM because some flows (e.g. dashboard joins) need the raw row.
    /// </summary>
    Task<Customer?> GetByIdAsync(int customerId, CancellationToken ct);

    /// <summary>
    /// Insert if no row exists for the (PM, provider, external_id) tuple, else
    /// update mutable fields. Returns the persisted entity.
    /// </summary>
    Task<Customer> UpsertAsync(Customer customer, CancellationToken ct);

    /// <summary>
    /// Stamps <see cref="Customer.PreflightNotifiedAt"/> with the current UTC
    /// time for the given customer ids that belong to <paramref name="propertyManagerId"/>.
    /// Other PMs' ids in the list are silently ignored (defense-in-depth — a
    /// crafted POST cannot stamp another tenant's customers).
    /// Returns the count of rows actually stamped (skips ones already stamped).
    /// </summary>
    Task<int> MarkPreflightSentAsync(
        int propertyManagerId,
        IReadOnlyCollection<int> customerIds,
        CancellationToken ct);
}
