namespace DueMap.Integrations.Accounting.Sync;

/// <summary>
/// One implementation per accounting provider. Holds the per-call auth + realm
/// concerns inside the implementation; the sync orchestrator selects the right
/// client based on <see cref="Provider"/>.
/// </summary>
public interface IAccountingDataClient
{
    AccountingProvider Provider { get; }

    Task<IReadOnlyList<SyncedCustomer>> ListCustomersAsync(
        string accessToken,
        string realmId,
        DateTime? modifiedSinceUtc,
        CancellationToken ct);

    Task<IReadOnlyList<SyncedInvoice>> ListInvoicesAsync(
        string accessToken,
        string realmId,
        DateTime? modifiedSinceUtc,
        CancellationToken ct);

    /// <summary>
    /// One-shot company metadata fetch — name, country, region, raw tz hint.
    /// Used during onboarding to pre-fill the Daily Close timezone, and later
    /// to display the company name on the dashboard (task #61). Implementations
    /// should not throw on missing fields; return what's available.
    /// </summary>
    Task<AccountingCompanyInfo> GetCompanyInfoAsync(
        string accessToken,
        string realmId,
        CancellationToken ct);

    /// <summary>
    /// Fetches the provider-rendered invoice PDF for the given external invoice id.
    /// Returns null when the provider doesn't have a PDF for that invoice
    /// (e.g. draft status, deleted, online-invoicing disabled). Throws for
    /// transient HTTP failures so the caller can decide to retry or fall back.
    /// </summary>
    Task<byte[]?> GetInvoicePdfAsync(
        string accessToken,
        string realmId,
        string invoiceExternalId,
        CancellationToken ct);
}
