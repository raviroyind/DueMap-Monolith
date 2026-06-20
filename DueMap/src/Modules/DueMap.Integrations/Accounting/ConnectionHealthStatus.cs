namespace DueMap.Integrations.Accounting;

/// <summary>
/// Operational liveness of a PM's accounting OAuth connection. Distinct from
/// <see cref="ConnectionStatus"/> (which captures the admin/legal state —
/// Connected / Disconnected).
///
/// Numeric values match the v17 schema's <c>health_status</c> column so the
/// EF int conversion is the identity.
/// </summary>
public enum ConnectionHealthStatus
{
    /// <summary>Default. Token refreshes work; nightly sync is processing this PM.</summary>
    Healthy  = 1,

    /// <summary>Transient HTTP errors observed but auth still works. Not currently used; reserved for future use.</summary>
    Degraded = 2,

    /// <summary>
    /// Auth has failed in a way that won't self-recover (refresh token revoked,
    /// app uninstalled, scopes downgraded). The orchestrator SKIPS this PM
    /// until the operator/PM reconnects. The Connection Health admin board
    /// surfaces every PM in this state with a Reconnect link.
    /// </summary>
    Broken   = 3
}
