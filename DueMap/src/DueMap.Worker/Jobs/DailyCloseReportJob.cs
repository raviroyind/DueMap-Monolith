using DueMap.Billing.Reports;
using DueMap.Identity;
using Microsoft.Extensions.Logging;

namespace DueMap.Worker.Jobs;

/// <summary>
/// Per-PM daily close report sender. Chained as a Hangfire continuation from
/// the sweep right after the orchestrator finishes — so the email arrives in
/// the PM's inbox carrying the totals for the day they just closed.
///
/// Email lookup uses the Identity user directory (worker-side; Billing stays
/// independent of authentication). PMs without a primary email on file are
/// logged + skipped.
/// </summary>
internal sealed partial class DailyCloseReportJob : IDailyCloseReportJob
{
    private readonly IUserDirectory _users;
    private readonly IDailyCloseReportService _reports;
    private readonly ILogger<DailyCloseReportJob> _logger;

    public DailyCloseReportJob(
        IUserDirectory users,
        IDailyCloseReportService reports,
        ILogger<DailyCloseReportJob> logger)
    {
        _users = users;
        _reports = reports;
        _logger = logger;
    }

    public async Task RunForPmAsync(int propertyManagerId, DateOnly businessDate, CancellationToken ct)
    {
        using var _scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["PmId"] = propertyManagerId,
            ["BusinessDate"] = businessDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        });

        var email = await _users.GetPrimaryEmailForPmAsync(propertyManagerId, ct);
        if (string.IsNullOrWhiteSpace(email))
        {
            LogSkipNoEmail(_logger, propertyManagerId);
            return;
        }

        await _reports.SendForAsync(propertyManagerId, businessDate, email, ct);
    }

    [LoggerMessage(EventId = 5103, Level = LogLevel.Warning,
        Message = "DailyCloseReport: pm={PropertyManagerId} has no primary email — skipping")]
    static partial void LogSkipNoEmail(ILogger logger, int propertyManagerId);
}
