namespace DueMap.Billing.Reports;

/// <summary>
/// Builds the per-PM end-of-day close report and emails it. Driven by a
/// recurring Hangfire job in DueMap.Worker. Idempotent on a given (pmId,
/// businessDate) — re-running won't double-send (we skip when no email is
/// on file and SendGrid de-dupes inside the day at the provider level).
/// </summary>
public interface IDailyCloseReportService
{
    /// <summary>
    /// Generate + send the report for one PM. <paramref name="businessDate"/>
    /// is the day being closed (typically "yesterday" when the job fires at
    /// 00:05 UTC). <paramref name="toEmail"/> is the destination — the caller
    /// (Worker job) resolves it via the Identity user directory so Billing
    /// stays independent of authentication. Returns true if an email was
    /// dispatched, false when the PM record itself is missing.
    /// </summary>
    Task<bool> SendForAsync(int propertyManagerId, DateOnly businessDate, string toEmail, CancellationToken ct);
}
