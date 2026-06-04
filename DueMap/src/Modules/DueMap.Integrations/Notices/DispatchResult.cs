namespace DueMap.Integrations.Notices;

public enum DispatchStatus
{
    /// <summary>Provider accepted the message; ultimate delivery via webhook callback.</summary>
    Queued,

    /// <summary>Provider rejected the message synchronously.</summary>
    Failed
}

public sealed record DispatchResult(
    DispatchStatus Status,
    string? ProviderMessageId,
    string? FailureReason);
