namespace DueMap.Integrations.Accounting;

/// <summary>
/// Read-only view of a single PM's connection health, used by both the
/// orchestrator's pre-flight gate and the admin "Connection Health" board.
/// Returned by <see cref="IAccountingConnectionService.GetHealthAsync"/> and
/// <see cref="IAccountingConnectionService.ListUnhealthyAsync"/>.
/// </summary>
public sealed record ConnectionHealthSnapshot(
    int PropertyManagerId,
    AccountingProvider Provider,
    ConnectionHealthStatus HealthStatus,
    string? PausedReason,
    DateTime? LastHealthCheck);
