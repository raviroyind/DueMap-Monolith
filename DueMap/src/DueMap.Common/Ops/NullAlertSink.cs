using Microsoft.Extensions.Logging;

namespace DueMap.Common.Ops;

/// <summary>
/// Default sink registered when no channel is configured. Logs the would-be
/// alert at Warning so it still appears in <c>logs.events</c> + the worker-log
/// viewer — operators see what would have alerted them even before they wire
/// up email or a webhook.
/// </summary>
internal sealed partial class NullAlertSink : IAlertSink
{
    private readonly ILogger<NullAlertSink> _logger;

    public NullAlertSink(ILogger<NullAlertSink> logger) => _logger = logger;

    public Task SendAsync(string subject, string body, CancellationToken ct)
    {
        LogWouldHaveAlerted(_logger, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 9301, Level = LogLevel.Warning,
        Message = "Alert (no sink configured) — subject: {Subject} body: {Body}")]
    static partial void LogWouldHaveAlerted(ILogger logger, string subject, string body);
}
