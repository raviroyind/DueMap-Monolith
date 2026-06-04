using DueMap.Integrations.Accounting;
using DueMap.Integrations.Accounting.Services;
using DueMap.Integrations.Accounting.Sync;
using DueMap.Integrations.Notices;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging;

namespace DueMap.Integrations.Services;

/// <summary>
/// Resolves the rent invoice for a (lease, due-date) and fetches its PDF
/// from the connected accounting provider. Used by the action executor when
/// a <c>late_fee_notice</c> is going out.
/// </summary>
internal sealed partial class LateFeeInvoiceAttachmentFetcher : ILateFeeInvoiceAttachmentFetcher
{
    private readonly IAccountingConnectionService _connections;
    private readonly IRentInvoiceRepository _invoices;
    private readonly IEnumerable<IAccountingDataClient> _clients;
    private readonly ILogger<LateFeeInvoiceAttachmentFetcher> _logger;

    public LateFeeInvoiceAttachmentFetcher(
        IAccountingConnectionService connections,
        IRentInvoiceRepository invoices,
        IEnumerable<IAccountingDataClient> clients,
        ILogger<LateFeeInvoiceAttachmentFetcher> logger)
    {
        _connections = connections;
        _invoices = invoices;
        _clients = clients;
        _logger = logger;
    }

    public async Task<DispatchAttachment?> TryFetchForCurrentPeriodAsync(
        int propertyManagerId,
        int leaseId,
        DateOnly currentDueDate,
        CancellationToken ct)
    {
        try
        {
            // 1. Find which provider invoice maps to this rent period.
            var invoice = await _invoices.GetCurrentExternalAsync(leaseId, currentDueDate, ct);
            if (invoice is null)
            {
                LogNoInvoice(_logger, leaseId, currentDueDate);
                return null;
            }

            // 2. Resolve the PM's active connection + token.
            var conn = await _connections.GetActiveAsync(propertyManagerId, ct);
            if (conn is null) return null;

            var token = await _connections.GetAccessTokenAsync(propertyManagerId, ct);
            if (token is null) return null;

            // 3. Make sure the invoice's provider matches the active connection.
            // If a PM reconnected to a different provider mid-cycle, the local
            // row's external_id won't resolve against the new provider — skip
            // attachment rather than 404.
            var expectedTenancyProvider = MapToTenancy(conn.Provider);
            if (invoice.Provider != expectedTenancyProvider)
            {
                LogProviderMismatch(_logger, leaseId, invoice.Provider.ToString(), conn.Provider.ToString());
                return null;
            }

            var client = _clients.FirstOrDefault(c => c.Provider == conn.Provider);
            if (client is null) return null;

            // 4. Pull the PDF. null => provider doesn't have it (draft, deleted, etc).
            var bytes = await client.GetInvoicePdfAsync(token, conn.RealmId, invoice.ExternalId, ct);
            if (bytes is null || bytes.Length == 0)
            {
                LogNoPdf(_logger, leaseId, invoice.ExternalId);
                return null;
            }

            // 5. Wrap. Filename uses the provider doc number when available so
            // the tenant sees something recognisable in their inbox.
            var name = !string.IsNullOrWhiteSpace(invoice.ExternalDocNumber)
                ? $"Invoice-{invoice.ExternalDocNumber}.pdf"
                : $"Invoice-{invoice.ExternalId}.pdf";

            return new DispatchAttachment(name, "application/pdf", bytes);
        }
        catch (Exception ex)
        {
            // Attachment failure must never block the email — log and return null
            // so the dispatcher falls through to a no-attachment send.
            LogFetchFailed(_logger, ex, leaseId);
            return null;
        }
    }

    private static ExternalAccountingProvider MapToTenancy(AccountingProvider p) => p switch
    {
        AccountingProvider.QuickBooks => ExternalAccountingProvider.QuickBooks,
        AccountingProvider.Xero       => ExternalAccountingProvider.Xero,
        _ => throw new ArgumentOutOfRangeException(nameof(p), p, null)
    };

    [LoggerMessage(EventId = 7601, Level = LogLevel.Information,
        Message = "Late-fee attachment: no invoice on file for lease={LeaseId} due={DueDate}")]
    static partial void LogNoInvoice(ILogger logger, int leaseId, DateOnly dueDate);

    [LoggerMessage(EventId = 7602, Level = LogLevel.Warning,
        Message = "Late-fee attachment: invoice provider ({InvoiceProvider}) != active connection ({ConnProvider}) for lease={LeaseId}; skipping PDF")]
    static partial void LogProviderMismatch(ILogger logger, int leaseId, string invoiceProvider, string connProvider);

    [LoggerMessage(EventId = 7603, Level = LogLevel.Information,
        Message = "Late-fee attachment: provider returned no PDF for lease={LeaseId} invoice={InvoiceExternalId}")]
    static partial void LogNoPdf(ILogger logger, int leaseId, string invoiceExternalId);

    [LoggerMessage(EventId = 7604, Level = LogLevel.Warning,
        Message = "Late-fee attachment: PDF fetch failed for lease={LeaseId}; email will send without attachment")]
    static partial void LogFetchFailed(ILogger logger, Exception ex, int leaseId);
}
