using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Notices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace DueMap.Integrations.Services;

internal sealed partial class TwilioSmsSender : ISmsSender
{
    private const string SmsFlag = "notices.sms";

    private readonly ITwilioRestClient _client;
    private readonly IntegrationsOptions.TwilioOptions _opts;
    private readonly IFeatureFlags _flags;
    private readonly ISmsSuppressionStore _suppressions;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        ITwilioRestClient client,
        IOptions<IntegrationsOptions> options,
        IFeatureFlags flags,
        ISmsSuppressionStore suppressions,
        ILogger<TwilioSmsSender> logger)
    {
        _client = client;
        _opts = options.Value.Twilio;
        _flags = flags;
        _suppressions = suppressions;
        _logger = logger;
    }

    public async Task<DispatchResult> SendAsync(DispatchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // P2-2 gate #1: SMS channel must be enabled (global flag). Off => the
        // channel is dark; report Suppressed (not Failed) so the caller skips
        // quietly and it isn't counted as a provider error.
        if (!await _flags.IsEnabledAsync(SmsFlag, propertyManagerId: null, ct))
        {
            return new DispatchResult(DispatchStatus.Suppressed, null, "SMS channel disabled (notices.sms off).");
        }

        if (string.IsNullOrWhiteSpace(_opts.FromNumber))
        {
            return new DispatchResult(DispatchStatus.Failed, null, "Integrations:Twilio:FromNumber is not configured.");
        }

        // P2-2 gate #2: never text an opted-out number (legal requirement).
        if (await _suppressions.IsSuppressedAsync(request.To, ct))
        {
            LogSuppressed(_logger, request.To);
            return new DispatchResult(DispatchStatus.Suppressed, null, "Recipient opted out of SMS.");
        }

        try
        {
            var resource = await MessageResource.CreateAsync(
                to: new PhoneNumber(request.To),
                from: new PhoneNumber(_opts.FromNumber),
                body: request.BodyText,
                client: _client);

            return new DispatchResult(DispatchStatus.Queued, resource.Sid, null);
        }
        catch (ApiException ex)
        {
            LogSendFailed(_logger, ex.Code, ex.Message);
            return new DispatchResult(DispatchStatus.Failed, null, $"Twilio {ex.Code}: {ex.Message}");
        }
    }

    [LoggerMessage(EventId = 3101, Level = LogLevel.Warning,
        Message = "Twilio rejected message (code {Code}): {Reason}")]
    static partial void LogSendFailed(ILogger logger, int code, string reason);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information,
        Message = "SMS suppressed — recipient {To} has opted out; not sending.")]
    static partial void LogSuppressed(ILogger logger, string to);
}
