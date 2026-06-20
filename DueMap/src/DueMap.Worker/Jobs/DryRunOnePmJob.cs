using System.Text.Json;
using DueMap.Billing;
using DueMap.Billing.Domain;
using DueMap.Common.FeatureFlags;
using Microsoft.Extensions.Logging;

namespace DueMap.Worker.Jobs;

/// <summary>
/// Hangfire-triggerable wrapper around <see cref="IPmDailyOrchestrator.ProcessAsync"/>
/// in <see cref="ExecutionMode.DryRun"/>. Operators can enqueue this from the
/// Hangfire dashboard ("Enqueue → DueMap.Worker.Jobs.DryRunOnePmJob.RunAsync(pmId, …)")
/// to inspect what would happen for one PM without going through the Web admin
/// surface.
///
/// The job writes the resulting <see cref="DayPlanPreview"/> as a single
/// JSON log line so the preview appears in <c>logs.events</c> and the
/// <c>/admin/worker-logs</c> viewer, queryable by <c>EventId = 4121</c>.
/// </summary>
public interface IDryRunOnePmJob
{
    Task RunAsync(int propertyManagerId, string? businessDateIso, CancellationToken ct);
}

internal sealed partial class DryRunOnePmJob : IDryRunOnePmJob
{
    private readonly IFeatureFlags _flags;
    private readonly IPmDailyOrchestrator _orchestrator;
    private readonly ILogger<DryRunOnePmJob> _logger;

    public DryRunOnePmJob(
        IFeatureFlags flags,
        IPmDailyOrchestrator orchestrator,
        ILogger<DryRunOnePmJob> logger)
    {
        _flags = flags;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task RunAsync(int propertyManagerId, string? businessDateIso, CancellationToken ct)
    {
        if (!await _flags.IsEnabledAsync("ops.dry_run", propertyManagerId, ct))
        {
            LogDisabled(_logger, propertyManagerId);
            return;
        }

        var businessDate = string.IsNullOrWhiteSpace(businessDateIso)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : (DateOnly.TryParse(businessDateIso, System.Globalization.CultureInfo.InvariantCulture,
                                 System.Globalization.DateTimeStyles.None, out var parsed)
                ? parsed
                : DateOnly.FromDateTime(DateTime.UtcNow));

        var outcome = await _orchestrator.ProcessAsync(
            propertyManagerId, businessDate, ExecutionMode.DryRun, ct);

        // Serialise the preview to a single log line. JSON is the most
        // queryable form for a structured-log sink, and the line stays
        // under 32 KB for typical PM portfolios.
        var json = JsonSerializer.Serialize(outcome.DryRunPreview, JsonOptions);
        LogPreview(_logger, propertyManagerId, businessDate, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [LoggerMessage(EventId = 4120, Level = LogLevel.Information,
        Message = "DryRunOnePmJob: feature flag 'ops.dry_run' is off for PM {PropertyManagerId}; nothing to do.")]
    static partial void LogDisabled(ILogger logger, int propertyManagerId);

    [LoggerMessage(EventId = 4121, Level = LogLevel.Information,
        Message = "DryRun preview for PM {PropertyManagerId} on {BusinessDate}: {PreviewJson}")]
    static partial void LogPreview(ILogger logger, int propertyManagerId, DateOnly businessDate, string previewJson);
}
