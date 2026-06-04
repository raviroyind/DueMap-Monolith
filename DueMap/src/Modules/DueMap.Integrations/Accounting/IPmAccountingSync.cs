namespace DueMap.Integrations.Accounting;

public sealed record PmSyncResult(
    bool Success,
    int CustomersSynced,
    int InvoicesSynced,
    string? FailureReason);

/// <summary>
/// Called by the Billing orchestrator just before per-PM daily processing so
/// the planner reads against fresh customer/invoice data. Implementations live
/// here; consumers (Billing) hold the abstraction.
/// </summary>
public interface IPmAccountingSync
{
    Task<PmSyncResult> SyncForAsync(int propertyManagerId, CancellationToken ct);
}
