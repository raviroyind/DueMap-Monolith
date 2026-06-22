namespace DueMap.Integrations.Notices;

public enum DispatchStatus
{
    /// <summary>Provider accepted the message; ultimate delivery via webhook callback.</summary>
    Queued,

    /// <summary>Provider rejected the message synchronously.</summary>
    Failed,

    /// <summary>
    /// Intentionally not sent — the SMS channel is disabled (flag off) or the
    /// recipient opted out (P2-2). NOT a failure: callers skip quietly and
    /// must not count it toward provider-error alerting.
    /// </summary>
    Suppressed
}

public sealed record DispatchResult(
    DispatchStatus Status,
    string? ProviderMessageId,
    string? FailureReason);
