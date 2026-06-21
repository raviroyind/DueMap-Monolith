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
    IReadOnlyList<DailyCloseFeeRow> Fees,
    // P1-6: the "What needs you" section — operator-actionable items with
    // deep links. Empty when the day was clean.
    IReadOnlyList<DailyCloseAttentionItem> NeedsYou);

/// <summary>
/// One "What needs you" item: something the PM must act on, with a deep link.
/// <paramref name="Severity"/> is "crit" (red) or "warn" (amber).
/// </summary>
public sealed record DailyCloseAttentionItem(
    string Severity,
    string Title,
    string Detail,
    string LinkLabel,
    string LinkUrl);

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
