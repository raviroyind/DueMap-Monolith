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
    private readonly ITwilioRestClient _client;
    private readonly IntegrationsOptions.TwilioOptions _opts;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        ITwilioRestClient client,
        IOptions<IntegrationsOptions> options,
        ILogger<TwilioSmsSender> logger)
    {
        _client = client;
        _opts = options.Value.Twilio;
        _logger = logger;
    }

    public async Task<DispatchResult> SendAsync(DispatchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_opts.FromNumber))
        {
            return new DispatchResult(DispatchStatus.Failed, null, "Integrations:Twilio:FromNumber is not configured.");
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
}
