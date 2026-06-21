using DueMap.Integrations.Persistence;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DueMap.Integrations.Accounting.Sync;

/// <summary>
/// Pulls customers + invoices for a PM from the connected accounting provider
/// and upserts them into Tenancy. Auto-links invoices to the lease when the
/// customer has exactly one active lease; otherwise leaves <c>lease_id</c>
/// null awaiting manual mapping in the PM admin UI.
/// </summary>
internal sealed partial class PmAccountingSyncService : IPmAccountingSync
{
    private readonly IAccountingConnectionService _connections;
    private readonly IntegrationsDbContext _integrationsDb;
    private readonly IEnumerable<IAccountingDataClient> _clients;
    private readonly ICustomerRepository _customers;
    private readonly IRentInvoiceRepository _invoices;
    private readonly ILeaseReader _leases;
    private readonly ILeaseWriter _leaseWriter;
    private readonly IPropertyManagerWriter _pmWriter;
    private readonly DueMap.Common.FeatureFlags.IFeatureFlags _flags;
    private readonly ILogger<PmAccountingSyncService> _logger;

    public PmAccountingSyncService(
        IAccountingConnectionService connections,
        IntegrationsDbContext integrationsDb,
        IEnumerable<IAccountingDataClient> clients,
        ICustomerRepository customers,
        IRentInvoiceRepository invoices,
        ILeaseReader leases,
        ILeaseWriter leaseWriter,
        IPropertyManagerWriter pmWriter,
        DueMap.Common.FeatureFlags.IFeatureFlags flags,
        ILogger<PmAccountingSyncService> logger)
    {
        _connections = connections;
        _integrationsDb = integrationsDb;
        _clients = clients;
        _customers = customers;
        _invoices = invoices;
        _leases = leases;
        _leaseWriter = leaseWriter;
        _pmWriter = pmWriter;
        _flags = flags;
        _logger = logger;
    }

    public async Task<PmSyncResult> SyncForAsync(int propertyManagerId, CancellationToken ct)
    {
        var conn = await _connections.GetActiveAsync(propertyManagerId, ct);
        if (conn is null)
        {
            // No connection = no sync to do. The orchestrator's planner still
            // runs against whatever lease/invoice state is already local.
            return new PmSyncResult(true, 0, 0, null);
        }

        var accessToken = await _connections.GetAccessTokenAsync(propertyManagerId, ct);
        if (accessToken is null)
        {
            return new PmSyncResult(false, 0, 0, "Token expired and could not be refreshed.");
        }

        var client = _clients.FirstOrDefault(c => c.Provider == conn.Provider);
        if (client is null)
        {
            return new PmSyncResult(false, 0, 0, $"No accounting client registered for {conn.Provider}.");
        }

        var tenancyProvider = MapProvider(conn.Provider);
        var since = conn.LastSyncAt;   // null = full sync on first run

        try
        {
            var customers = await client.ListCustomersAsync(accessToken, conn.RealmId, since, ct);
            foreach (var sc in customers)
            {
                await _customers.UpsertAsync(new Customer
                {
                    PropertyManagerId = propertyManagerId,
                    ExternalProvider = tenancyProvider,
                    ExternalId = sc.ExternalId,
                    DisplayName = sc.DisplayName,
                    Email = sc.Email,
                    Phone = sc.Phone,
                    IsActive = sc.IsActive,
                    LastSyncedAt = DateTime.UtcNow,
                    // v18 (P1-1) — surface billing state for DiscoveryService.
                    // Provider clients already normalise to 2-letter upper or
                    // null; we just pass through.
                    BillingState = sc.BillingState
                }, ct);
            }

            var invoices = await client.ListInvoicesAsync(accessToken, conn.RealmId, since, ct);
            var invoicesPersisted = 0;
            foreach (var si in invoices)
            {
                var customer = await _customers.GetByExternalAsync(
                    propertyManagerId, tenancyProvider, si.CustomerExternalId, ct);
                if (customer is null)
                {
                    // Customer wasn't in the delta — skip the invoice rather than
                    // creating a dangling row. Next full pull will resolve it.
                    LogOrphanInvoice(_logger, si.ExternalId, si.CustomerExternalId);
                    continue;
                }

                var leaseId = await TryAutoLinkLeaseAsync(propertyManagerId, customer.Id, ct);

                await _invoices.UpsertAsync(new RentInvoice
                {
                    PropertyManagerId = propertyManagerId,
                    CustomerId = customer.Id,
                    LeaseId = leaseId,
                    ExternalProvider = tenancyProvider,
                    ExternalId = si.ExternalId,
                    ExternalDocNumber = si.ExternalDocNumber,
                    IssueDate = si.IssueDate,
                    DueDate = si.DueDate,
                    TotalAmount = si.TotalAmount,
                    Balance = si.Balance,
                    Currency = si.Currency,
                    Status = MapInvoiceStatus(si.Status),
                    PublicPaymentUrl = si.PublicPaymentUrl,
                    LastSyncedAt = DateTime.UtcNow
                }, ct);

                invoicesPersisted++;
            }

            // P2-1: autopay status read-back (gated by integrations.autopay).
            // No API exposes a true enrollment flag, so we infer per lease from
            // payment behavior over its past-due-dated invoices. OFF => skipped.
            await ReadBackAutopayStatusAsync(propertyManagerId, ct);

            await UpdateLastSyncAsync(propertyManagerId, success: true, error: null, ct);

            // First successful sync flips the PM to "Synced" and then immediately
            // to "Active" — v1 has no explicit go-live step; PMs are ready as soon
            // as data is flowing. The minimum-status semantics make this idempotent
            // (every subsequent sync is a no-op for the status flag).
            await _pmWriter.SetMinimumStatusAsync(propertyManagerId, OnboardingStatus.Synced, ct);
            await _pmWriter.SetMinimumStatusAsync(propertyManagerId, OnboardingStatus.Active, ct);

            LogSynced(_logger, propertyManagerId, conn.Provider, customers.Count, invoicesPersisted);
            return new PmSyncResult(true, customers.Count, invoicesPersisted, null);
        }
        catch (Exception ex)
        {
            await UpdateLastSyncAsync(propertyManagerId, success: false, error: ex.Message, ct);

            // P0-3 self-heal: when the failure is *authentication* (refresh
            // token revoked, app uninstalled, scopes downgraded) rather than
            // transient (5xx, network blip, rate limit), mark the connection
            // Broken so the orchestrator skips this PM until they reconnect.
            // Other failure modes stay transient and don't pause processing —
            // tomorrow's sweep will retry naturally.
            //
            // We detect by substring on the client's exception message, which
            // is formatted "QuickBooks API returned 401: …" / "Xero API
            // returned 403: …". If we ever standardise on a typed
            // AccountingAuthFailedException, switch to a pattern-match here.
            if (IsAuthFailure(ex))
            {
                await _connections.MarkBrokenAsync(
                    propertyManagerId,
                    $"Sync auth failure: {ex.Message}",
                    ct);
            }

            LogSyncFailed(_logger, ex, propertyManagerId, conn.Provider);
            return new PmSyncResult(false, 0, 0, ex.Message);
        }
    }

    private static bool IsAuthFailure(Exception ex) =>
        ex.Message.Contains("401", StringComparison.Ordinal) ||
        ex.Message.Contains("403", StringComparison.Ordinal);

    private async Task<int?> TryAutoLinkLeaseAsync(int propertyManagerId, int customerId, CancellationToken ct)
    {
        // "Exactly one active lease for this customer" is the only auto-link
        // condition. Anything else is left for manual mapping in the PM UI.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leases = (await _leases.ListActiveAsync(propertyManagerId, today, ct))
            .Where(l => l.CustomerId == customerId)
            .ToList();
        return leases.Count == 1 ? leases[0].Id : null;
    }

    /// <summary>
    /// P2-1 autopay status read-back. Gated by <c>integrations.autopay</c>.
    /// For each active lease, infers autopay/reliable-payer status from the
    /// payment outcomes of its past-due-dated invoices and stamps the lease.
    /// Money-untouched — read-only against synced data.
    /// </summary>
    private async Task ReadBackAutopayStatusAsync(int propertyManagerId, CancellationToken ct)
    {
        if (!await _flags.IsEnabledAsync("integrations.autopay", propertyManagerId, ct))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leases = await _leases.ListActiveAsync(propertyManagerId, today, ct);
        var invoices = await _invoices.ListByPropertyManagerAsync(propertyManagerId, ct);

        var byLease = invoices
            .Where(i => i.LeaseId is not null)
            .GroupBy(i => i.LeaseId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        foreach (var lease in leases)
        {
            var paidFlags = (byLease.TryGetValue(lease.Id, out var list) ? list : new List<RentInvoice>())
                .Where(i => i.DueDate < today)                                  // past their due date
                .Select(i => i.Status == RentInvoiceStatus.Paid || i.Balance <= 0m)
                .ToList();

            var status = AutopayStatusInference.Infer(paidFlags);
            await _leaseWriter.UpdateAutopayStatusAsync(lease.Id, status, now, ct);
        }
    }

    private async Task UpdateLastSyncAsync(int propertyManagerId, bool success, string? error, CancellationToken ct)
    {
        var row = await _integrationsDb.PmAccountingConnections
            .FirstOrDefaultAsync(c => c.PropertyManagerId == propertyManagerId, ct);
        if (row is null) return;

        if (success)
        {
            row.LastSyncAt = DateTime.UtcNow;
            row.LastSyncError = null;
        }
        else
        {
            row.LastSyncError = error;
        }
        row.UpdatedAt = DateTime.UtcNow;
        await _integrationsDb.SaveChangesAsync(ct);
    }

    private static ExternalAccountingProvider MapProvider(AccountingProvider p) => p switch
    {
        AccountingProvider.QuickBooks => ExternalAccountingProvider.QuickBooks,
        AccountingProvider.Xero       => ExternalAccountingProvider.Xero,
        _ => throw new ArgumentOutOfRangeException(nameof(p), p, null)
    };

    private static RentInvoiceStatus MapInvoiceStatus(SyncedInvoiceStatus s) => s switch
    {
        SyncedInvoiceStatus.Open   => RentInvoiceStatus.Open,
        SyncedInvoiceStatus.Paid   => RentInvoiceStatus.Paid,
        SyncedInvoiceStatus.Voided => RentInvoiceStatus.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "Synced pm={PropertyManagerId} provider={Provider} customers={Customers} invoices={Invoices}")]
    static partial void LogSynced(ILogger logger, int propertyManagerId, AccountingProvider provider, int customers, int invoices);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning,
        Message = "Skipped invoice {ExternalId}: customer {CustomerExternalId} not in delta")]
    static partial void LogOrphanInvoice(ILogger logger, string externalId, string customerExternalId);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Error,
        Message = "Sync failed for pm={PropertyManagerId} provider={Provider}")]
    static partial void LogSyncFailed(ILogger logger, Exception ex, int propertyManagerId, AccountingProvider provider);
}
