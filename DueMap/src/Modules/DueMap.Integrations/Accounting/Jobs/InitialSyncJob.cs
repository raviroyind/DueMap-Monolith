using Microsoft.Extensions.Logging;

namespace DueMap.Integrations.Accounting.Jobs;

/// <summary>
/// Background "first sync on connect" job. Enqueued by the Web OAuth callback
/// the moment a connection is persisted, so a PM's customers/invoices are
/// pulled server-side regardless of whether the user completes the
/// <c>/onboarding/syncing</c> wizard page. The Worker's Hangfire server runs
/// it (the Web host only enqueues).
///
/// <para>Lives in Integrations — not the Worker's Jobs folder — because the
/// Web host needs the interface at compile time to enqueue it, and Web does
/// not reference the Worker project. The class has NO Hangfire dependency;
/// Hangfire invokes <see cref="RunAsync"/> by reflection.</para>
///
/// <para>Idempotent: <see cref="IPmAccountingSync.SyncForAsync"/> upserts by
/// external id and uses <c>LastSyncAt</c> as the delta cursor, so a re-run
/// (or an overlap with the wizard page's own sync) is cheap and safe.</para>
/// </summary>
public interface IInitialSyncJob
{
    Task RunAsync(int propertyManagerId, CancellationToken ct);
}

internal sealed partial class InitialSyncJob : IInitialSyncJob
{
    private readonly IPmAccountingSync _sync;
    private readonly ILogger<InitialSyncJob> _logger;

    public InitialSyncJob(IPmAccountingSync sync, ILogger<InitialSyncJob> logger)
    {
        _sync = sync;
        _logger = logger;
    }

    public async Task RunAsync(int propertyManagerId, CancellationToken ct)
    {
        LogStarting(_logger, propertyManagerId);
        var result = await _sync.SyncForAsync(propertyManagerId, ct);
        if (result.Success)
        {
            LogSucceeded(_logger, propertyManagerId, result.CustomersSynced, result.InvoicesSynced);
        }
        else
        {
            // SyncForAsync already records LastSyncError on the connection; we
            // log here too so the failure shows on the activity timeline. The
            // PM can retry from the /connections "Sync now" button.
            LogFailed(_logger, propertyManagerId, result.FailureReason ?? "unknown");
        }
    }

    [LoggerMessage(EventId = 6010, Level = LogLevel.Information,
        Message = "InitialSyncJob: starting first sync for PM {PropertyManagerId}")]
    static partial void LogStarting(ILogger logger, int propertyManagerId);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Information,
        Message = "InitialSyncJob: PM {PropertyManagerId} synced {CustomersSynced} customer(s), {InvoicesSynced} invoice(s)")]
    static partial void LogSucceeded(ILogger logger, int propertyManagerId, int customersSynced, int invoicesSynced);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Warning,
        Message = "InitialSyncJob: first sync failed for PM {PropertyManagerId} — {Reason}")]
    static partial void LogFailed(ILogger logger, int propertyManagerId, string reason);
}
