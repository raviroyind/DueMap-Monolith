using System.Globalization;
using System.Text;

namespace DueMap.Billing.Reports;

/// <summary>
/// Builds the HTML body for the daily close email. Inline styles only —
/// every major mail client strips &lt;style&gt; tags and external CSS,
/// so each element carries its own visual treatment.
///
/// Layout mirrors the spec screenshot: header strip, summary table, reminders
/// table, fees table, plain footer.
/// </summary>
internal static class DailyCloseReportHtmlBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Build(DailyCloseReportData d)
    {
        var sb = new StringBuilder(capacity: 8 * 1024);
        sb.Append(@"<!DOCTYPE html><html><head><meta charset=""utf-8""></head><body style=""margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;color:#27272a;"">");
        sb.Append(@"<div style=""max-width:760px;margin:0 auto;padding:24px 0;"">");

        // -------- Header strip --------
        sb.Append(@"<div style=""background:#334155;color:#fff;padding:22px 28px;border-radius:12px 12px 0 0;display:flex;align-items:center;justify-content:space-between;"">
            <div>
                <div style=""font-size:11px;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;opacity:0.85;"">DueMap</div>
                <div style=""font-size:19px;font-weight:600;margin-top:6px;line-height:1.3;"">Daily close — ").Append(d.BusinessDate.ToString("MMMM d, yyyy", Culture)).Append(@"</div>
            </div>
            <a href=""https://app.duemap.com/"" style=""color:#fff;font-size:12px;text-decoration:none;opacity:0.9;"">Open dashboard →</a>
        </div>");

        // -------- Greeting --------
        sb.Append(@"<div style=""background:#fff;padding:22px 28px;"">
            <div style=""font-size:16px;font-weight:600;"">Hi ").Append(HtmlEncode(d.PmDisplayName)).Append(@",</div>
            <div style=""font-size:14px;color:#52525b;margin-top:4px;line-height:1.55;"">Here is your daily close report covering activity on ")
          .Append(d.BusinessDate.ToString("MMMM d, yyyy", Culture)).Append(@".</div>
        </div>");

        // -------- What needs you (P1-6) --------
        sb.Append(@"<div style=""background:#fff;padding:18px 28px 6px;"">
            <div style=""font-size:13px;font-weight:600;color:#9f1239;text-transform:uppercase;letter-spacing:0.06em;margin-bottom:10px;"">What needs you</div>");
        if (d.NeedsYou.Count == 0)
        {
            sb.Append(@"<div style=""font-size:13px;color:#16a34a;padding:4px 0 8px;"">Nothing needs your attention — all clear.</div>");
        }
        else
        {
            foreach (var item in d.NeedsYou)
            {
                var accent = item.Severity == "crit" ? "#be123c" : "#d97706";
                var bg     = item.Severity == "crit" ? "#fef2f2" : "#fffbeb";
                sb.Append(@"<div style=""border-left:3px solid ").Append(accent)
                  .Append(@";background:").Append(bg)
                  .Append(@";padding:10px 14px;margin-bottom:8px;border-radius:0 6px 6px 0;"">
                    <div style=""font-size:13px;font-weight:600;color:#27272a;"">").Append(HtmlEncode(item.Title)).Append(@"</div>
                    <div style=""font-size:12px;color:#52525b;margin-top:2px;line-height:1.5;"">").Append(HtmlEncode(item.Detail)).Append(@"</div>
                    <a href=""").Append(HtmlEncode(item.LinkUrl)).Append(@""" style=""display:inline-block;margin-top:6px;font-size:12px;font-weight:600;color:").Append(accent).Append(@";text-decoration:none;"">")
                  .Append(HtmlEncode(item.LinkLabel)).Append(" →</a>\n                </div>");
            }
        }
        sb.Append("</div>");

        // -------- What happened (P1-6 section header) --------
        sb.Append(@"<div style=""background:#fff;padding:18px 28px 0;"">
            <div style=""font-size:13px;font-weight:600;color:#3730a3;text-transform:uppercase;letter-spacing:0.06em;border-top:1px solid #e4e4e7;padding-top:16px;"">What happened</div>
        </div>");

        // -------- Summary table --------
        sb.Append(@"<div style=""background:#fff;padding:12px 28px 6px;"">
            <div style=""font-size:12px;font-weight:600;color:#71717a;text-transform:uppercase;letter-spacing:0.06em;margin-bottom:10px;"">Summary</div>
            <table style=""width:100%;border-collapse:collapse;font-size:13px;"">
                <thead>
                    <tr style=""background:#f4f4f5;text-align:left;"">
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Unpaid invoices</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;text-align:right;"">Unpaid amount</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Past-due invoices</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;text-align:right;"">Past-due amount</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Reminders sent</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Fees assessed</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;text-align:right;"">Fees total</th>
                    </tr>
                </thead>
                <tbody>
                    <tr style=""border-top:1px solid #e4e4e7;"">
                        <td style=""padding:12px 10px;font-weight:600;"">").Append(d.Summary.UnpaidInvoices).Append(@"</td>
                        <td style=""padding:12px 10px;font-weight:600;text-align:right;"">").Append(Money(d.Summary.UnpaidAmount)).Append(@"</td>
                        <td style=""padding:12px 10px;font-weight:600;color:#be123c;"">").Append(d.Summary.PastDueInvoices).Append(@"</td>
                        <td style=""padding:12px 10px;font-weight:600;text-align:right;color:#be123c;"">").Append(Money(d.Summary.PastDueAmount)).Append(@"</td>
                        <td style=""padding:12px 10px;font-weight:600;"">").Append(d.Summary.RemindersSent).Append(@"</td>
                        <td style=""padding:12px 10px;font-weight:600;"">").Append(d.Summary.FeesAssessed).Append(@"</td>
                        <td style=""padding:12px 10px;font-weight:600;text-align:right;"">").Append(Money(d.Summary.FeesTotal)).Append(@"</td>
                    </tr>
                </tbody>
            </table>
        </div>");

        // -------- Reminders section --------
        sb.Append(@"<div style=""background:#fff;padding:20px 28px 6px;"">
            <div style=""font-size:13px;font-weight:600;color:#3730a3;text-transform:uppercase;letter-spacing:0.06em;margin-bottom:10px;"">Payment reminders sent</div>");
        if (d.Reminders.Count == 0)
        {
            sb.Append(@"<div style=""font-size:13px;color:#71717a;padding:10px 0;"">No reminders sent on this day.</div>");
        }
        else
        {
            sb.Append(@"<table style=""width:100%;border-collapse:collapse;font-size:13px;"">
                <thead>
                    <tr style=""background:#f4f4f5;text-align:left;"">
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Customer</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Invoice</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Due date</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;text-align:right;"">Original</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;text-align:right;"">Current</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Channel</th>
                    </tr>
                </thead><tbody>");
            foreach (var r in d.Reminders)
            {
                sb.Append(@"<tr style=""border-top:1px solid #e4e4e7;"">
                    <td style=""padding:10px;"">").Append(HtmlEncode(r.CustomerName)).Append(@"</td>
                    <td style=""padding:10px;font-family:monospace;font-size:12px;color:#52525b;"">").Append(HtmlEncode(r.InvoiceNumber)).Append(@"</td>
                    <td style=""padding:10px;color:#52525b;"">").Append(r.DueDate.ToString("yyyy-MM-dd", Culture)).Append(@"</td>
                    <td style=""padding:10px;text-align:right;"">").Append(Money(r.OriginalAmount)).Append(@"</td>
                    <td style=""padding:10px;text-align:right;font-weight:600;"">").Append(Money(r.CurrentAmount)).Append(@"</td>
                    <td style=""padding:10px;color:#52525b;"">").Append(HtmlEncode(r.Channel)).Append(@"</td>
                </tr>");
            }
            sb.Append("</tbody></table>");
        }
        sb.Append("</div>");

        // -------- Fees section --------
        sb.Append(@"<div style=""background:#fff;padding:20px 28px 24px;border-radius:0 0 12px 12px;"">
            <div style=""font-size:13px;font-weight:600;color:#3730a3;text-transform:uppercase;letter-spacing:0.06em;margin-bottom:10px;"">Late fees auto-generated</div>");
        if (d.Fees.Count == 0)
        {
            sb.Append(@"<div style=""font-size:13px;color:#71717a;padding:10px 0;"">No fees assessed on this day.</div>");
        }
        else
        {
            sb.Append(@"<table style=""width:100%;border-collapse:collapse;font-size:13px;"">
                <thead>
                    <tr style=""background:#f4f4f5;text-align:left;"">
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Customer</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">Lease</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;"">For period</th>
                        <th style=""padding:10px;color:#52525b;font-weight:600;text-align:right;"">Fee amount</th>
                    </tr>
                </thead><tbody>");
            foreach (var f in d.Fees)
            {
                sb.Append(@"<tr style=""border-top:1px solid #e4e4e7;"">
                    <td style=""padding:10px;"">").Append(HtmlEncode(f.CustomerName)).Append(@"</td>
                    <td style=""padding:10px;font-family:monospace;font-size:12px;color:#52525b;"">L-").Append(f.LeaseId).Append(@"</td>
                    <td style=""padding:10px;color:#52525b;"">").Append(f.DueDate.ToString("yyyy-MM-dd", Culture)).Append(@"</td>
                    <td style=""padding:10px;text-align:right;font-weight:600;color:#be123c;"">").Append(Money(f.FeeAmount)).Append(@"</td>
                </tr>");
            }
            sb.Append("</tbody></table>");
        }
        sb.Append("</div>");

        // -------- Footer --------
        sb.Append(@"<div style=""text-align:center;font-size:11px;color:#a1a1aa;padding:18px;"">
            Sent by DueMap. This is an automated daily close report.
        </div></div></body></html>");

        return sb.ToString();
    }

    /// <summary>Plain-text fallback for mail clients that won't render HTML.</summary>
    public static string BuildPlainText(DailyCloseReportData d)
    {
        // Use IFormatProvider on every interpolated AppendLine — CA1305 enforces
        // explicit culture so report bytes are identical regardless of which
        // server's locale the Worker happens to run on.
        var ic = Culture;
        var sb = new StringBuilder(2048);
        sb.AppendLine(ic, $"DueMap — Daily close report — {d.BusinessDate:yyyy-MM-dd}");
        sb.AppendLine(ic, $"Hi {d.PmDisplayName},").AppendLine();

        sb.AppendLine(ic, $"WHAT NEEDS YOU ({d.NeedsYou.Count})");
        if (d.NeedsYou.Count == 0)
        {
            sb.AppendLine("  Nothing needs your attention — all clear.");
        }
        else
        {
            foreach (var item in d.NeedsYou)
            {
                sb.AppendLine(ic, $"  [{item.Severity.ToUpperInvariant()}] {item.Title}");
                sb.AppendLine(ic, $"        {item.Detail}");
                sb.AppendLine(ic, $"        {item.LinkLabel}: {item.LinkUrl}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("WHAT HAPPENED");
        sb.AppendLine("SUMMARY");
        sb.AppendLine(ic, $"  Unpaid invoices: {d.Summary.UnpaidInvoices}  ({Money(d.Summary.UnpaidAmount)})");
        sb.AppendLine(ic, $"  Past-due: {d.Summary.PastDueInvoices}  ({Money(d.Summary.PastDueAmount)})");
        sb.AppendLine(ic, $"  Reminders sent: {d.Summary.RemindersSent}");
        sb.AppendLine(ic, $"  Fees assessed: {d.Summary.FeesAssessed}  ({Money(d.Summary.FeesTotal)})");
        sb.AppendLine();

        sb.AppendLine(ic, $"REMINDERS ({d.Reminders.Count})");
        foreach (var r in d.Reminders)
        {
            sb.AppendLine(ic, $"  · {r.CustomerName} — invoice {r.InvoiceNumber} due {r.DueDate:yyyy-MM-dd} — {Money(r.CurrentAmount)} via {r.Channel}");
        }
        sb.AppendLine();

        sb.AppendLine(ic, $"LATE FEES ({d.Fees.Count})");
        foreach (var f in d.Fees)
        {
            sb.AppendLine(ic, $"  · {f.CustomerName} — L-{f.LeaseId} period {f.DueDate:yyyy-MM-dd} — {Money(f.FeeAmount)}");
        }
        sb.AppendLine();
        sb.AppendLine("— DueMap (automated daily close)");
        return sb.ToString();
    }

    private static string Money(decimal amount) =>
        amount.ToString("C", new CultureInfo("en-US"));

    private static string HtmlEncode(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
}
