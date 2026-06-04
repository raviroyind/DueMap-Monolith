using DueMap.Integrations.Notices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DueMap.Integrations.Services;

internal sealed partial class SendGridEmailSender : IEmailSender
{
    private const string ProviderMessageIdHeader = "X-Message-Id";

    private readonly ISendGridClient _client;
    private readonly IntegrationsOptions.SendGridOptions _opts;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        ISendGridClient client,
        IOptions<IntegrationsOptions> options,
        ILogger<SendGridEmailSender> logger)
    {
        _client = client;
        _opts = options.Value.SendGrid;
        _logger = logger;
    }

    public async Task<DispatchResult> SendAsync(DispatchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Dev fallback: when SendGrid isn't configured we don't fail — we log
        // the email body at Information level so the dev can see what would
        // have gone out (and pluck the magic-link URL from the log to
        // continue testing the tenant portal without setting up SendGrid).
        // Detect by missing FromEmail; ApiKey is a placeholder in the same
        // case (see IntegrationsModule.ISendGridClient factory).
        if (string.IsNullOrWhiteSpace(_opts.FromEmail) || string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            LogDevFallback(_logger, request.To, request.Subject, request.BodyText);
            return new DispatchResult(DispatchStatus.Queued, ProviderMessageId: $"dev-{Guid.NewGuid():N}", FailureReason: null);
        }

        var msg = MailHelper.CreateSingleEmail(
            from: new EmailAddress(_opts.FromEmail, _opts.FromName),
            to: new EmailAddress(request.To, request.ToDisplayName),
            subject: request.Subject,
            plainTextContent: request.BodyText,
            htmlContent: request.BodyHtml);

        // Attach any files supplied by the caller. SendGrid expects base64-encoded
        // content; we wrap each DispatchAttachment's bytes here so callers can
        // keep working with raw byte arrays.
        if (request.Attachments is { Count: > 0 })
        {
            foreach (var a in request.Attachments)
            {
                msg.AddAttachment(
                    filename: a.FileName,
                    base64Content: Convert.ToBase64String(a.Content.Span),
                    type: a.ContentType,
                    disposition: "attachment");
            }
        }

        var response = await _client.SendEmailAsync(msg, ct);
        var status = (int)response.StatusCode;

        if (status is < 200 or >= 300)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            LogSendFailed(_logger, status, body);
            return new DispatchResult(DispatchStatus.Failed, null, $"SendGrid HTTP {status}: {body}");
        }

        string? providerMsgId = null;
        if (response.Headers.TryGetValues(ProviderMessageIdHeader, out var values))
        {
            providerMsgId = values.FirstOrDefault();
        }

        return new DispatchResult(DispatchStatus.Queued, providerMsgId, null);
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "SendGrid rejected message (HTTP {Status}): {Body}")]
    static partial void LogSendFailed(ILogger logger, int status, string body);

    // High-visibility log line so devs can spot the magic-link URL when
    // running without SendGrid creds. Multiline body intentionally — makes
    // the link easy to copy out of a terminal.
    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "[dev fallback] Would send email\n  To:      {To}\n  Subject: {Subject}\n  Body:\n{Body}")]
    static partial void LogDevFallback(ILogger logger, string to, string subject, string body);
}
