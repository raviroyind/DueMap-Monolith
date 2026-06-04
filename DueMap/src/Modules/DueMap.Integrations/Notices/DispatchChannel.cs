namespace DueMap.Integrations.Notices;

/// <summary>
/// Provider-agnostic notice channel. Mirrors <c>notices.notice_deliveries.channel</c>
/// but lives in Integrations so this module has no compile-time dependency on Notices.
/// The caller (Billing dispatcher) converts between the two.
/// </summary>
public enum DispatchChannel
{
    Email,
    Sms
}
