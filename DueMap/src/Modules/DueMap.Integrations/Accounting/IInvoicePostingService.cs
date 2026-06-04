namespace DueMap.Integrations.Accounting;

public sealed record LateFeePostingRequest(
    int PropertyManagerId,
    AccountingProvider Provider,
    string CustomerExternalId,
    DateOnly DueDate,
    decimal Amount,
    string Memo);

public sealed record InvoicePostingResult(
    bool Posted,
    string? ExternalInvoiceId,
    string? FailureReason);

/// <summary>
/// Post a late fee as a line item / invoice in the PM's accounting system.
/// Implementation is deferred — needs the OAuth-authenticated client and
/// provider-specific invoice payloads.
/// </summary>
public interface IInvoicePostingService
{
    Task<InvoicePostingResult> PostLateFeeAsync(LateFeePostingRequest request, CancellationToken ct);
}
