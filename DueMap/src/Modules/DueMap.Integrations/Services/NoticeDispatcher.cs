using DueMap.Integrations.Notices;

namespace DueMap.Integrations.Services;

internal sealed class NoticeDispatcher : INoticeDispatcher
{
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;

    public NoticeDispatcher(IEmailSender email, ISmsSender sms)
    {
        _email = email;
        _sms = sms;
    }

    public Task<DispatchResult> DispatchAsync(DispatchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Channel switch
        {
            DispatchChannel.Email => _email.SendAsync(request, ct),
            DispatchChannel.Sms   => _sms.SendAsync(request, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(request),
                $"Unsupported dispatch channel: {request.Channel}.")
        };
    }
}
