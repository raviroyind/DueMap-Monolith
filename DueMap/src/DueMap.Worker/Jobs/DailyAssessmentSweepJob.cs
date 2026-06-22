using DueMap.Billing;
using DueMap.Billing.Reports;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace DueMap.Worker.Jobs;

/// <summary>
/// Recurring sweep (cron <c>*/15 * * * *</c>). For each PM at status=Active:
///   1. Compute their local business date from <see cref="PropertyManager.TimeZoneId"/>.
///   2. Skip if today's run row already exists (PM already processed today).
///   3. Enqueue <see cref="IPmDailyOrchestrator.ProcessAsync"/> (reminders + late fees),
///      then chain <see cref="IDailyCloseReportJob.RunForPmAsync"/> as a Hangfire
///      continuation — the close report fires ONLY after a successful orchestrator
///      run, never on its own.
///
/// The 15-min cadence guarantees no PM's local midnight lags by more than 15
/// minutes before being picked up. Per-PM single enqueue per day is enforced
/// by the orchestrator's TryStartAsync — even if a race lets two sweep ticks
/// see the same PM as unprocessed, only one will actually run.
/// </summary>
internal sealed partial class DailyAssessmentSweepJob : IDailyAssessmentSweepJob
{
    private readonly IPropertyManagerReader _pms;
    private readonly IPmProcessingRunRepository _runs;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<DailyAssessmentSweepJob> _logger;

    public DailyAssessmentSweepJob(
        IPropertyManagerReader pms,
        IPmProcessingRunRepository runs,
        IBackgroundJobClient jobs,
        ILogger<DailyAssessmentSweepJob> logger)
    {
        _pms = pms;
        _runs = runs;
        _jobs = jobs;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var all = await _pms.ListAllAsync(ct);
        var active = all.Where(p => p.OnboardingStatus == OnboardingStatus.Active).ToList();

        // Compute each PM's local "today" once. The set of distinct business
        // dates is bounded by the number of distinct timezones in the portfolio
        // (~25 max worldwide); we call the already-processed query once per
        // distinct date and cache the result.
        var pmToBusinessDate = active.ToDictionary(p => p.Id, p => LocalToday(p, nowUtc));
        var distinctDates = pmToBusinessDate.Values.Distinct().ToList();

        var processedByDate = new Dictionary<DateOnly, HashSet<int>>();
        foreach (var date in distinctDates)
        {
            var ids = await _runs.ListPropertyManagersAlreadyProcessedAsync(date, ct);
            processedByDate[date] = ids.ToHashSet();
        }

        var pending = active
            .Where(p => !processedByDate[pmToBusinessDate[p.Id]].Contains(p.Id))
            .ToList();

        LogSweepStarting(_logger, nowUtc, active.Count, pending.Count);

        foreach (var pm in pending)
        {
            var businessDate = pmToBusinessDate[pm.Id];

            // Orchestrator first (reminders + late fees), then daily close
            // report as a Hangfire continuation. ContinueJobWith only runs the
            // child if the parent finishes successfully — so a failed orchestrator
            // doesn't send a stale report.
            // Pass ExecutionMode.Live explicitly — Hangfire's expression-tree
            // capture doesn't infer optional-parameter defaults (P0-2 added the
            // ExecutionMode parameter; this is the nightly Live path).
            var orchId = _jobs.Enqueue<IPmDailyOrchestrator>(
                o => o.ProcessAsync(pm.Id, businessDate, DueMap.Billing.Domain.ExecutionMode.Live, CancellationToken.None));

            _jobs.ContinueJobWith<IDailyCloseReportJob>(
                orchId,
                j => j.RunForPmAsync(pm.Id, businessDate, CancellationToken.None));
        }
    }

    // PM's local "today" — TimeZoneInfo accepts both Windows ids and (with .NET 8)
    // IANA ids. We store IANA in the DB; on Linux this is native, on Windows it's
    // translated via ICU. Unknown / malformed ids fall back to UTC so a bad row
    // doesn't kill the sweep.
    private static DateOnly LocalToday(PropertyManager pm, DateTime nowUtc)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(pm.TimeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(nowUtc); }
        catch (InvalidTimeZoneException)  { return DateOnly.FromDateTime(nowUtc); }
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "Sweep tick: nowUtc={NowUtc} activePMs={Active} pendingPMs={Pending}")]
    static partial void LogSweepStarting(ILogger logger, DateTime nowUtc, int active, int pending);
}
