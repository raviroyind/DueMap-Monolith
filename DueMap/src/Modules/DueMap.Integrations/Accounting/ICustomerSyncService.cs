namespace DueMap.Integrations.Accounting;

public sealed record AccountingCustomer(
    string ExternalId,
    string DisplayName,
    string? Email,
    string? Phone);

public enum AccountingProvider
{
    QuickBooks,
    Xero
}

/// <summary>
/// Pull customers (== tenants) from the PM's accounting system. Implementation
/// is deferred — needs the OAuth flow and provider-specific paging. The
/// contract is here so the rest of the system can be wired to it.
/// </summary>
public interface ICustomerSyncService
{
    Task<IReadOnlyList<AccountingCustomer>> ListCustomersAsync(
        int propertyManagerId,
        AccountingProvider provider,
        CancellationToken ct);
}
