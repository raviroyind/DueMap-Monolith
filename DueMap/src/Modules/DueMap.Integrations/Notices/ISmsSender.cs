namespace DueMap.Integrations.Notices;

/// <summary>
/// SMS provider abstraction. The default implementation is Twilio.
/// </summary>
public interface ISmsSender
{
    Task<DispatchResult> SendAsync(DispatchRequest request, CancellationToken ct);
}
