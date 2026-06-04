namespace DueMap.Integrations.Notices;

/// <summary>
/// Single dispatch surface used by the Billing orchestrator. Routes to the
/// underlying email or SMS adapter based on <see cref="DispatchRequest.Channel"/>.
/// </summary>
public interface INoticeDispatcher
{
    Task<DispatchResult> DispatchAsync(DispatchRequest request, CancellationToken ct);
}
