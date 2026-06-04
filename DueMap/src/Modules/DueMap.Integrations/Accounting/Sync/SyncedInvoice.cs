namespace DueMap.Integrations.Accounting.Sync;

public enum SyncedInvoiceStatus { Open, Paid, Voided }

public sealed record SyncedInvoice(
    string ExternalId,
    string CustomerExternalId,
    string? ExternalDocNumber,
    DateOnly? IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal Balance,
    string Currency,
    SyncedInvoiceStatus Status,
    string? PublicPaymentUrl);   // QBO InvoiceLink / Xero OnlineInvoiceUrl; null when payments not enabled
