namespace DueMap.Billing.Reports;

/// <summary>
/// Snapshot of one PM's activity for a single reporting day. Built by
/// <see cref="DailyCloseReportService"/> and handed to the HTML renderer.
/// All dollar amounts are in the PM's working currency (USD assumed for v1).
/// </summary>
public sealed record DailyCloseReportData(
    int PropertyManagerId,
    DateOnly BusinessDate,
    string PmDisplayName,
    DailyCloseSummary Summary,
    IReadOnlyList<DailyCloseReminderRow> Reminders,
    IReadOnlyList<DailyCloseFeeRow> Fees);

public sealed record DailyCloseSummary(
    int UnpaidInvoices,
    decimal UnpaidAmount,
    int PastDueInvoices,
    decimal PastDueAmount,
    int RemindersSent,
    int FeesAssessed,
    decimal FeesTotal);

public sealed record DailyCloseReminderRow(
    string CustomerName,
    string InvoiceNumber,
    DateOnly DueDate,
    decimal OriginalAmount,
    decimal CurrentAmount,
    string Channel);

public sealed record DailyCloseFeeRow(
    string CustomerName,
    int LeaseId,
    DateOnly DueDate,
    decimal FeeAmount);
