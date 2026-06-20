using System.Net.Http.Json;
using DueMap.Common.Ops;
using Microsoft.Extensions.Options;

namespace DueMap.Integrations.Ops;

/// <summary>
/// POSTs operator alerts as JSON to a generic webhook URL. Body shape is
/// kept Slack-compatible (text field) so a Slack incoming webhook works
/// without translation; richer integrations (Teams, PagerDuty) can be
/// added in a follow-up.
/// </summary>
internal sealed class WebhookAlertSink : IAlertSink
{
    private readonly HttpClient _http;
    private readonly string _url;

    public WebhookAlertSink(HttpClient http, IOptions<AlertsOptions> options)
    {
        _http = http;
        _url = options.Value.WebhookUrl ?? throw new InvalidOperationException(
            "WebhookAlertSink configured but Ops:Alerts:WebhookUrl is empty.");
    }

    public async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        // Slack-compatible payload: { "text": "..." }. Recipients that need
        // a different schema can be added by branching here on the URL host.
        var payload = new { text = $"*{subject}*\n```\n{body}\n```" };
        using var response = await _http.PostAsJsonAsync(_url, payload, ct);
        // Don't throw on non-success — AlertService catches anyway, but a
        // clean response is preferred. EnsureSuccessStatusCode here would
        // turn a Slack 429 into a stack trace; the caller's log line is
        // enough signal.
        _ = response.StatusCode;
    }
}
