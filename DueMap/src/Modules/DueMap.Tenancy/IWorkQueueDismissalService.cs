namespace DueMap.Tenancy;

/// <summary>Persistence for Today-queue dismissals (see WorkItemDismissal).</summary>
public interface IWorkQueueDismissalService
{
    /// <summary>Idempotent — dismissing an already-dismissed key is a no-op.</summary>
    Task DismissAsync(int propertyManagerId, string itemKey, CancellationToken ct);

    Task<IReadOnlySet<string>> ListKeysAsync(int propertyManagerId, CancellationToken ct);
}
