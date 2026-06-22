using DueMap.Common.Ops;
using DueMap.Integrations.Notices;
using Microsoft.Extensions.Options;

namespace DueMap.Integrations.Ops;

/// <summary>
/// Sends operator alerts via the existing <see cref="IEmailSender"/> (which
/// is SendGrid in production and the dev-fallback console logger when
/// SendGrid isn't configured — so this works out of the box in dev too).
///
/// Subject + body come from <c>AlertService</c>; we just wrap them in a
/// plain-text email to <c>Ops:Alerts:To</c>.
/// </summary>
internal sealed class EmailAlertSink : IAlertSink
{
    private readonly IEmailSender _email;
    private readonly string _to;

    public EmailAlertSink(IEmailSender email, IOptions<AlertsOptions> options)
    {
        _email = email;
        _to = options.Value.To ?? throw new InvalidOperationException(
            "EmailAlertSink configured but Ops:Alerts:To is empty.");
    }

    public Task SendAsync(string subject, string body, CancellationToken ct) =>
        _email.SendAsync(new DispatchRequest(
            Channel: DispatchChannel.Email,
            To: _to,
            ToDisplayName: null,
            Subject: subject,
            BodyHtml: $"<pre style=\"font-family:monospace;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(body)}</pre>",
            BodyText: body), ct);
}
