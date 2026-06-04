namespace DueMap.Integrations.Notices;

/// <summary>
/// Email provider abstraction. The default implementation is SendGrid; tests
/// substitute a fake. Public so consumers can resolve it directly when SMS
/// fan-out isn't needed.
/// </summary>
public interface IEmailSender
{
    Task<DispatchResult> SendAsync(DispatchRequest request, CancellationToken ct);
}
