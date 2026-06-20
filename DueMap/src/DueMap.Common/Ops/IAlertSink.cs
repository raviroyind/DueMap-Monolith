namespace DueMap.Common.Ops;

/// <summary>
/// Where an operator alert actually goes (email, Slack webhook, PagerDuty, …).
/// Implementations live in Integrations (so they can reuse the existing
/// <c>IEmailSender</c> + <c>HttpClient</c> wiring); Common only ships
/// <see cref="NullAlertSink"/> for the "no channel configured" default.
///
/// Sinks MUST NOT throw — <see cref="IAlertService"/> already catches as
/// belt-and-braces, but the discipline keeps the worker safe from a
/// misbehaving notification provider.
/// </summary>
public interface IAlertSink
{
    Task SendAsync(string subject, string body, CancellationToken ct);
}
