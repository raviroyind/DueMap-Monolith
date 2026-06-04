namespace DueMap.Integrations.Notices;

/// <summary>
/// Resolves the rent invoice for a given (lease, due-date), then pulls its
/// PDF from the connected accounting provider (QBO or Xero) so the late-fee
/// notice email can attach it. Returns null when:
///   * the PM has no active accounting connection
///   * the rent invoice for that period isn't on file locally
///   * the provider doesn't have a PDF for the invoice
///
/// All failures are non-fatal — the email still goes out without an
/// attachment if the fetch fails. Errors are logged inside.
/// </summary>
public interface ILateFeeInvoiceAttachmentFetcher
{
    Task<DispatchAttachment?> TryFetchForCurrentPeriodAsync(
        int propertyManagerId,
        int leaseId,
        DateOnly currentDueDate,
        CancellationToken ct);
}
