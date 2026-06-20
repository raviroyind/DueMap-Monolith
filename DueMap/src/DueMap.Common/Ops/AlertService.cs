using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DueMap.Common.Ops;

/// <summary>
/// Default <see cref="IAlertService"/>. Owns:
/// <list type="bullet">
///   <item>The dedupe table: <c>alertKey → lastFiredUtc</c>. Same key inside
///   <see cref="AlertsOptions.DedupeWindow"/> swallowed silently.</item>
///   <item>The fan-out to <see cref="IAlertSink"/> (configured in
///   <c>IntegrationsModule</c>).</item>
///   <item>The fail-soft wrapper so a misbehaving sink never crashes a
///   caller.</item>
/// </list>
///
/// Singleton-friendly: the dedupe table is in-process so per-instance
/// scheduling matters only at the boundary between Web and Worker. Both
/// processes alert independently — that's intentional. The fire-from-Worker
/// path is by far the loudest, and a single Web restart shouldn't suddenly
/// page anyone twice.
/// </summary>
internal sealed partial class AlertService : IAlertService
{
    private readonly IAlertSink _sink;
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _dedupeWindow;
    private readonly ILogger<AlertService> _logger;

    // Concurrent because RaiseOperatorAsync can be called from parallel
    // Hangfire workers / parallel HTTP requests on the Web process.
    private readonly ConcurrentDictionary<string, DateTime> _lastFiredUtc = new(StringComparer.Ordinal);

    public AlertService(
        IAlertSink sink,
        IOptions<AlertsOptions> options,
        ILogger<AlertService> logger)
        : this(sink, options, logger, utcNow: () => DateTime.UtcNow) { }

    /// <summary>
    /// Test seam: lets unit tests inject a controllable clock to fast-forward
    /// across the dedupe window without sleeping. Internal so only the
    /// tests project (via InternalsVisibleTo) can reach it.
    /// </summary>
    internal AlertService(
        IAlertSink sink,
        IOptions<AlertsOptions> options,
        ILogger<AlertService> logger,
        Func<DateTime> utcNow)
    {
        _sink = sink;
        _logger = logger;
        _utcNow = utcNow;
        _dedupeWindow = options.Value.DedupeWindow;
    }

    public async Task RaiseOperatorAsync(
        string alertKey,
        AlertSeverity severity,
        string message,
        IReadOnlyDictionary<string, string>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alertKey) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Dedupe — atomic upsert. AddOrUpdate's updateValueFactory sees the
        // previous value, so we can decide once whether to fire AND record
        // the new timestamp in the same call.
        var now = _utcNow();
        var fire = true;
        _lastFiredUtc.AddOrUpdate(
            alertKey,
            addValueFactory: _ => { fire = true; return now; },
            updateValueFactory: (_, previous) =>
            {
                if (now - previous < _dedupeWindow)
                {
                    fire = false;
                    return previous;     // keep old timestamp so the window doesn't slide on every suppressed call
                }
                fire = true;
                return now;
            });

        if (!fire)
        {
            LogSuppressedDedupe(_logger, alertKey);
            return;
        }

        try
        {
            var subject = BuildSubject(severity, alertKey);
            var body = BuildBody(message, context);
            await _sink.SendAsync(subject, body, ct);
            LogRaised(_logger, alertKey, severity);
        }
        catch (Exception ex)
        {
            // Fail-soft. The fact we even reached here means the sink itself
            // failed — log loudly so we know alerting is broken, but don't
            // surface to the caller (would block the worker on a stuck SMTP
            // server, etc).
            LogSinkFailed(_logger, ex, alertKey);
        }
    }

    private static string BuildSubject(AlertSeverity severity, string alertKey) => severity switch
    {
        AlertSeverity.Critical => $"[CRIT] DueMap — {alertKey}",
        AlertSeverity.Warning  => $"[WARN] DueMap — {alertKey}",
        _                      => $"[INFO] DueMap — {alertKey}"
    };

    private static string BuildBody(string message, IReadOnlyDictionary<string, string>? context)
    {
        if (context is null || context.Count == 0) return message;

        var sb = new StringBuilder(message.Length + context.Count * 32);
        sb.AppendLine(message);
        sb.AppendLine();
        sb.AppendLine("Context:");
        foreach (var (k, v) in context)
        {
            sb.Append("  ").Append(k).Append(" = ").AppendLine(v);
        }
        return sb.ToString();
    }

    [LoggerMessage(EventId = 9311, Level = LogLevel.Information,
        Message = "Alert suppressed (dedupe window) — key={AlertKey}")]
    static partial void LogSuppressedDedupe(ILogger logger, string alertKey);

    [LoggerMessage(EventId = 9312, Level = LogLevel.Information,
        Message = "Alert raised key={AlertKey} severity={Severity}")]
    static partial void LogRaised(ILogger logger, string alertKey, AlertSeverity severity);

    [LoggerMessage(EventId = 9313, Level = LogLevel.Error,
        Message = "Alert sink failed for key={AlertKey} — alerting is broken until the sink recovers.")]
    static partial void LogSinkFailed(ILogger logger, Exception ex, string alertKey);
}
