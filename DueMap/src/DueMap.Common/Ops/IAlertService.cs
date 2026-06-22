namespace DueMap.Common.Ops;

/// <summary>
/// Operator alerting. Fires <em>only</em> for events that need a human:
/// sweep stalled, sync failing across many PMs, dispatch provider down,
/// etc. Per-tenant failures (one bounced email, one missing template) are
/// log lines, never alerts.
///
/// <para>
/// Dedupes by <c>alertKey</c> within <see cref="AlertsOptions.DedupeWindow"/>.
/// Repeats with the same key inside the window are silently swallowed. This
/// is the "threshold" guard — callers decide WHEN to alert (e.g. "if &gt; 5
/// PMs failed sync"), the service guarantees once-per-window emission.
/// </para>
///
/// <para>Fail-soft: any sink exception is caught + logged. Never throws.</para>
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Page the operator. <paramref name="alertKey"/> identifies the alert
    /// class (e.g. <c>"sweep_stalled"</c>) for dedupe. <paramref name="message"/>
    /// is the body shown in email / chat. <paramref name="context"/> is
    /// rendered as a small key:value table appended to the body.
    /// </summary>
    Task RaiseOperatorAsync(
        string alertKey,
        AlertSeverity severity,
        string message,
        IReadOnlyDictionary<string, string>? context = null,
        CancellationToken ct = default);
}
