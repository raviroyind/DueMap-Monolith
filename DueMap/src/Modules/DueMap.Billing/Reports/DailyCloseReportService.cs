using DueMap.Integrations.Notices;
using DueMap.Notices;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging;

namespace DueMap.Billing.Reports;

internal sealed partial class DailyCloseReportService : IDailyCloseReportService
{
    private readonly IPropertyManagerReader _pms;
    private readonly ILeaseReader _leases;
    private readonly IRentInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly INoticeDeliveryRepository _deliveries;
    private readonly ILateFeeAssessmentRepository _fees;
    private readonly INoticeDispatcher _dispatcher;
    private readonly ILogger<DailyCloseReportService> _logger;

    public DailyCloseReportService(
        IPropertyManagerReader pms,
        ILeaseReader leases,
        IRentInvoiceRepository invoices,
        ICustomerRepository customers,
        INoticeDeliveryRepository deliveries,
        ILateFeeAssessmentRepository fees,
        INoticeDispatcher dispatcher,
        ILogger<DailyCloseReportService> logger)
    {
        _pms = pms;
        _leases = leases;
        _invoices = invoices;
        _customers = customers;
        _deliveries = deliveries;
        _fees = fees;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<bool> SendForAsync(int propertyManagerId, DateOnly businessDate, string toEmail, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);

        var pm = await _pms.GetAsync(propertyManagerId, ct);
        if (pm is null)
        {
            LogSkipPmMissing(_logger, propertyManagerId);
            return false;
        }

        var data = await BuildAsync(pm, businessDate, ct);
        var html = DailyCloseReportHtmlBuilder.Build(data);

        var request = new DispatchRequest(
            Channel: DispatchChannel.Email,
            To: toEmail,
            ToDisplayName: pm.Name,
            Subject: $"DueMap daily close — {businessDate:yyyy-MM-dd}",
            BodyHtml: html,
            BodyText: DailyCloseReportHtmlBuilder.BuildPlainText(data));

        var result = await _dispatcher.DispatchAsync(request, ct);
        if (result.Status == DispatchStatus.Failed)
        {
            LogSendFailed(_logger, propertyManagerId, result.FailureReason ?? "unknown");
            return false;
        }

        LogSent(_logger, propertyManagerId, toEmail, data.Summary.RemindersSent, data.Summary.FeesAssessed);
        return true;
    }

    private async Task<DailyCloseReportData> BuildAsync(PropertyManager pm, DateOnly businessDate, CancellationToken ct)
    {
        // Window: [00:00, 24:00) UTC of the business date. Cross-PM timezone
        // handling is a follow-up — every PM gets the same UTC-day window today.
        var fromUtc = businessDate.ToDateTime(TimeOnly.MinValue);
        var toUtc   = businessDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Sequential reads — the underlying repos all share the Tenancy DbContext;
        // running them concurrently would trip EF's "second operation" guard.
        var leases     = await _leases.ListActiveAsync(pm.Id, businessDate, ct);
        var invoices   = await _invoices.ListByPropertyManagerAsync(pm.Id, ct);
        var customers  = (await _customers.ListByPropertyManagerAsync(pm.Id, ct))
            .ToDictionary(c => c.Id, c => c.DisplayName);

        var deliveries = await _deliveries.ListSentInRangeForPmAsync(pm.Id, fromUtc, toUtc, ct);
        var fees       = await _fees.ListForLeasesInRangeAsync(
            leases.Select(l => l.Id).ToList(), fromUtc, toUtc, ct);

        // Map invoice -> lease customer for the reminder + fee row labels.
        var invoiceById = invoices.ToDictionary(i => i.Id);
        var leaseToCustomer = leases.ToDictionary(l => l.Id, l => l.CustomerId);

        // Summary numbers — "unpaid" means status=Open; "past due" filters that
        // set to those whose due_date < businessDate.
        var openInvoices = invoices.Where(i => i.Status == Tenancy.Domain.RentInvoiceStatus.Open).ToList();
        var pastDue      = openInvoices.Where(i => i.DueDate < businessDate).ToList();

        var summary = new DailyCloseSummary(
            UnpaidInvoices:  openInvoices.Count,
            UnpaidAmount:    openInvoices.Sum(i => i.Balance),
            PastDueInvoices: pastDue.Count,
            PastDueAmount:   pastDue.Sum(i => i.Balance),
            RemindersSent:   deliveries.Count,
            FeesAssessed:    fees.Count,
            FeesTotal:       fees.Sum(f => f.FeeAmount));

        var reminderRows = deliveries
            .Select(d =>
            {
                // Best-effort linkage: deliveries carry LeaseId; the lease's
                // customer id resolves the display name.
                string customer = leaseToCustomer.TryGetValue(d.LeaseId, out var cid) && cid is int realCid
                    ? customers.GetValueOrDefault(realCid) ?? $"Customer #{realCid}"
                    : $"Lease #{d.LeaseId}";

                // Pick the lease's most-relevant open invoice for the row labels.
                var inv = openInvoices
                    .Where(i => i.LeaseId == d.LeaseId)
                    .OrderBy(i => i.DueDate)
                    .FirstOrDefault();

                return new DailyCloseReminderRow(
                    CustomerName: customer,
                    InvoiceNumber: inv?.ExternalDocNumber ?? (inv is null ? "—" : $"#{inv.Id}"),
                    DueDate: inv?.DueDate ?? businessDate,
                    OriginalAmount: inv?.TotalAmount ?? 0m,
                    CurrentAmount: inv?.Balance ?? 0m,
                    Channel: d.Channel.ToString());
            })
            .ToList();

        var feeRows = fees
            .Select(f =>
            {
                string customer = leaseToCustomer.TryGetValue(f.LeaseId, out var cid) && cid is int realCid
                    ? customers.GetValueOrDefault(realCid) ?? $"Customer #{realCid}"
                    : $"Lease #{f.LeaseId}";
                return new DailyCloseFeeRow(
                    CustomerName: customer,
                    LeaseId: f.LeaseId,
                    DueDate: f.DueDate,
                    FeeAmount: f.FeeAmount);
            })
            .ToList();

        return new DailyCloseReportData(
            PropertyManagerId: pm.Id,
            BusinessDate: businessDate,
            PmDisplayName: pm.Name,
            Summary: summary,
            Reminders: reminderRows,
            Fees: feeRows);
    }

    [LoggerMessage(EventId = 7101, Level = LogLevel.Information,
        Message = "DailyCloseReport sent: pm={PropertyManagerId} email={Email} reminders={Reminders} fees={Fees}")]
    static partial void LogSent(ILogger logger, int propertyManagerId, string email, int reminders, int fees);

    [LoggerMessage(EventId = 7103, Level = LogLevel.Warning,
        Message = "DailyCloseReport skipped: pm={PropertyManagerId} not found")]
    static partial void LogSkipPmMissing(ILogger logger, int propertyManagerId);

    [LoggerMessage(EventId = 7104, Level = LogLevel.Error,
        Message = "DailyCloseReport send failed: pm={PropertyManagerId} reason={Reason}")]
    static partial void LogSendFailed(ILogger logger, int propertyManagerId, string reason);
}
