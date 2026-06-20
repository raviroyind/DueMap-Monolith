using DueMap.Billing.Domain;
using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Accounting;
using DueMap.Tenancy;
using Microsoft.Extensions.Logging;

namespace DueMap.Billing.Services;

internal sealed partial class PmDailyOrchestrator : IPmDailyOrchestrator
{
    private readonly IPmProcessingRunRepository _runs;
    private readonly IPmAccountingSync _sync;
    private readonly IAccountingConnectionService _connections;
    private readonly ILeaseReader _leases;
    private readonly IAssessmentPlanner _planner;
    private readonly IActionExecutor _executor;
    private readonly IFeatureFlags _flags;
    private readonly ILogger<PmDailyOrchestrator> _logger;

    public PmDailyOrchestrator(
        IPmProcessingRunRepository runs,
        IPmAccountingSync sync,
        IAccountingConnectionService connections,
        ILeaseReader leases,
        IAssessmentPlanner planner,
        IActionExecutor executor,
        IFeatureFlags flags,
        ILogger<PmDailyOrchestrator> logger)
    {
        _runs = runs;
        _sync = sync;
        _connections = connections;
        _leases = leases;
        _planner = planner;
        _executor = executor;
        _flags = flags;
        _logger = logger;
    }

    public async Task<PmProcessingOutcome> ProcessAsync(
        int propertyManagerId,
        DateOnly businessDate,
        ExecutionMode mode = ExecutionMode.Live,
        CancellationToken ct = default)
    {
        // Every log line emitted from this scope carries PmId + BusinessDate
        // (and RunId once we have it) so the admin dashboard can pivot any
        // log line back to "which PM, which run." We use ILogger.BeginScope
        // (rather than Serilog.Context.LogContext directly) so Billing stays
        // free of a hard Serilog dependency — the Worker's Serilog M.E.L
        // bridge captures scope properties into Serilog log events.
        using var _pmScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["PmId"] = propertyManagerId,
            ["BusinessDate"] = businessDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["Mode"] = mode.ToString()
        });

        return mode == ExecutionMode.DryRun
            ? await ProcessDryRunAsync(propertyManagerId, businessDate, ct)
            : await ProcessLiveAsync(propertyManagerId, businessDate, ct);
    }

    // ------------------------------------------------------------------
    // LIVE — unchanged behaviour from the existing nightly pipeline
    // ------------------------------------------------------------------

    private async Task<PmProcessingOutcome> ProcessLiveAsync(
        int propertyManagerId, DateOnly businessDate, CancellationToken ct)
    {
        // P0-3 self-heal gate. Flag-controlled (default off) so existing
        // behaviour is preserved until ops.connection_health is enabled.
        // When on, a PM with a Broken connection short-circuits BEFORE any
        // slot claim or sync — pause-instead-of-retry-loop.
        if (await _flags.IsEnabledAsync("ops.connection_health", propertyManagerId, ct))
        {
            var health = await _connections.GetHealthAsync(propertyManagerId, ct);
            if (health is { HealthStatus: ConnectionHealthStatus.Broken })
            {
                LogPmPausedConnectionBroken(_logger, propertyManagerId, businessDate, health.PausedReason);
                return new PmProcessingOutcome(
                    Started: false,
                    LeasesPlanned: 0,
                    ActionsExecuted: 0,
                    ActionsSkipped: 0,
                    ActionsFailed: 0,
                    FailureReason: $"connection broken — paused: {health.PausedReason}");
            }
        }

        // 1. Claim the (PM, date) slot. Duplicate runs return null.
        var run = await _runs.TryStartAsync(propertyManagerId, businessDate, ct);
        if (run is null)
        {
            LogAlreadyProcessed(_logger, propertyManagerId, businessDate);
            return new PmProcessingOutcome(false, 0, 0, 0, 0, "already processed");
        }

        using var _runScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RunId"] = run.Id
        });

        try
        {
            // 2. Refresh accounting data before reading anything.
            var syncResult = await _sync.SyncForAsync(propertyManagerId, ct);
            if (!syncResult.Success)
            {
                var reason = $"Accounting sync failed: {syncResult.FailureReason}";
                await _runs.MarkFailedAsync(run.Id, reason, ct);
                return new PmProcessingOutcome(true, 0, 0, 0, 0, reason);
            }

            // 3. Plan + execute for each active lease.
            var leases = await _leases.ListActiveAsync(propertyManagerId, businessDate, ct);

            var executed = 0;
            var skipped  = 0;
            var failed   = 0;

            foreach (var lease in leases)
            {
                var plan = await _planner.PlanAsync(lease, businessDate, ct);
                if (plan.IsEmpty) continue;

                foreach (var action in plan.Actions)
                {
                    var result = await _executor.ExecuteAsync(
                        lease, plan.CurrentDueDate, businessDate, action, ExecutionMode.Live, ct);

                    switch (result.Outcome)
                    {
                        case ActionOutcome.Executed:          executed++; break;
                        case ActionOutcome.SkippedAlreadyDone:
                        case ActionOutcome.SkippedNoContact:  skipped++;  break;
                        case ActionOutcome.Failed:            failed++;
                                                              LogActionFailed(_logger, lease.Id, action.Kind.ToString(), result.Detail);
                                                              break;
                    }
                }
            }

            await _runs.MarkCompletedAsync(run.Id, leases.Count, executed, ct);
            return new PmProcessingOutcome(true, leases.Count, executed, skipped, failed, null);
        }
        catch (Exception ex)
        {
            LogProcessingCrashed(_logger, ex, propertyManagerId, businessDate);
            await _runs.MarkFailedAsync(run.Id, ex.Message, ct);
            return new PmProcessingOutcome(true, 0, 0, 0, 0, ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // DRY-RUN — plan + render only, zero writes, no dispatch
    // ------------------------------------------------------------------

    private async Task<PmProcessingOutcome> ProcessDryRunAsync(
        int propertyManagerId, DateOnly businessDate, CancellationToken ct)
    {
        // No slot claim. No sync. We're explicitly previewing against the
        // current local state — "what would happen if I ran this *right now*."
        // Stale data is fine; that's the whole point of a preview.
        var leases = await _leases.ListActiveAsync(propertyManagerId, businessDate, ct);

        var previews = new List<PlannedActionPreview>();
        var executed = 0;
        var skipped  = 0;
        var failed   = 0;

        foreach (var lease in leases)
        {
            var plan = await _planner.PlanAsync(lease, businessDate, ct);
            if (plan.IsEmpty) continue;

            foreach (var action in plan.Actions)
            {
                var result = await _executor.ExecuteAsync(
                    lease, plan.CurrentDueDate, businessDate, action, ExecutionMode.DryRun, ct);

                if (result.Preview is not null) previews.Add(result.Preview);

                switch (result.Outcome)
                {
                    case ActionOutcome.Executed:          executed++; break;
                    case ActionOutcome.SkippedAlreadyDone:
                    case ActionOutcome.SkippedNoContact:  skipped++;  break;
                    case ActionOutcome.Failed:            failed++;
                                                          LogDryRunActionFailed(_logger, lease.Id, action.Kind, result.Detail);
                                                          break;
                }
            }
        }

        var preview = new DayPlanPreview(
            PropertyManagerId: propertyManagerId,
            BusinessDate:      businessDate,
            LeasesPlanned:     leases.Count,
            ActionsPreviewed:  previews.Count,
            Actions:           previews);

        LogDryRunCompleted(_logger, propertyManagerId, businessDate, leases.Count, previews.Count);

        return new PmProcessingOutcome(
            Started:         true,
            LeasesPlanned:   leases.Count,
            ActionsExecuted: executed,
            ActionsSkipped:  skipped,
            ActionsFailed:   failed,
            FailureReason:   null,
            DryRunPreview:   preview);
    }

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information,
        Message = "PM {PropertyManagerId} already processed for {BusinessDate}")]
    static partial void LogAlreadyProcessed(ILogger logger, int propertyManagerId, DateOnly businessDate);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning,
        Message = "Action failed for lease {LeaseId} kind={Kind}: {Detail}")]
    static partial void LogActionFailed(ILogger logger, int leaseId, string kind, string? detail);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Error,
        Message = "Processing crashed for PM {PropertyManagerId} on {BusinessDate}")]
    static partial void LogProcessingCrashed(ILogger logger, Exception ex, int propertyManagerId, DateOnly businessDate);

    [LoggerMessage(EventId = 4111, Level = LogLevel.Information,
        Message = "DryRun completed for PM {PropertyManagerId} on {BusinessDate}: leases={LeasesPlanned} previews={ActionsPreviewed}")]
    static partial void LogDryRunCompleted(ILogger logger, int propertyManagerId, DateOnly businessDate, int leasesPlanned, int actionsPreviewed);

    [LoggerMessage(EventId = 4112, Level = LogLevel.Information,
        Message = "DryRun preview blocked for lease {LeaseId} kind={Kind}: {Detail}")]
    static partial void LogDryRunActionFailed(ILogger logger, int leaseId, ActionKind kind, string? detail);

    [LoggerMessage(EventId = 4113, Level = LogLevel.Information,
        Message = "PM {PropertyManagerId} skipped on {BusinessDate} — connection broken: {Reason}")]
    static partial void LogPmPausedConnectionBroken(ILogger logger, int propertyManagerId, DateOnly businessDate, string? reason);
}
