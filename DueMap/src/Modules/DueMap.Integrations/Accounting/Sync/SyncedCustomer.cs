namespace DueMap.Integrations.Accounting.Sync;

/// <summary>Provider-agnostic snapshot of a customer returned by an accounting client.</summary>
public sealed record SyncedCustomer(
    string ExternalId,
    string DisplayName,
    string? Email,
    string? Phone,
    bool IsActive);
