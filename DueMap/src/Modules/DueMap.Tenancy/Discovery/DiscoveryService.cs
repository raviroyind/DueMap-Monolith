using DueMap.Common.FeatureFlags;
using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DueMap.Tenancy.Discovery;

/// <summary>
/// EF-backed implementation. One DbContext per public call (factory
/// pattern, matches the rest of Tenancy) — no per-instance state means
/// the service can be a singleton if we ever want it that way.
/// </summary>
internal sealed partial class DiscoveryService : IDiscoveryService
{
    private const string FlagKey = "tenancy.autodiscovery";

    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;
    private readonly IFeatureFlags _flags;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(
        IDbContextFactory<TenancyDbContext> dbFactory,
        IFeatureFlags flags,
        ILogger<DiscoveryService> logger)
    {
        _dbFactory = dbFactory;
        _flags = flags;
        _logger = logger;
    }

    // ----------------------------------------------------------------------
    // RunForPmAsync — pull all leases for this PM, gather their invoices +
    // customer state, run the engine, write back. Idempotent overwrite.
    // ----------------------------------------------------------------------
    public async Task<int> RunForPmAsync(int propertyManagerId, CancellationToken ct)
    {
        if (!await _flags.IsEnabledAsync(FlagKey, propertyManagerId, ct))
        {
            LogSkipped(_logger, propertyManagerId);
            return 0;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Single round-trip — pull leases, their invoices (via LeaseId), and
        // the joined Customer.BillingState. EF Core compiles this to a
        // bounded set of queries even on PMs with thousands of leases
        // because we explicitly project the shape we need.
        var leases = await db.Leases
            .Where(l => l.PropertyManagerId == propertyManagerId)
            .Select(l => new
            {
                Lease = l,
                CustomerBillingState = l.CustomerId == null
                    ? null
                    : db.Customers.Where(c => c.Id == l.CustomerId).Select(c => c.BillingState).FirstOrDefault(),
                Invoices = db.Set<RentInvoice>()
                    .Where(i => i.LeaseId == l.Id)
                    .Select(i => new { i.TotalAmount, i.DueDate })
                    .ToList()
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var written = 0;

        foreach (var row in leases)
        {
            var amounts = row.Invoices.Select(i => i.TotalAmount).ToArray();
            var dueDays = row.Invoices.Select(i => i.DueDate.Day).ToArray();
            var outcome = DiscoveryInferenceEngine.Infer(amounts, dueDays, row.CustomerBillingState);

            // Tracked entity — set the inferred_* fields and let SaveChanges
            // figure out the UPDATE. Confirmation stamp is left untouched
            // by design (re-running shouldn't blow it away).
            row.Lease.InferredRentAmount = outcome.Rent;
            row.Lease.InferredDueDay     = outcome.DueDay;
            row.Lease.InferredState      = outcome.State;
            row.Lease.InferredAt         = now;
            written++;
        }

        if (written > 0)
        {
            await db.SaveChangesAsync(ct);
            LogRan(_logger, propertyManagerId, written);
        }
        return written;
    }

    // ----------------------------------------------------------------------
    // ConfirmAsync — one-tap stamp from the review UI.
    // ----------------------------------------------------------------------
    public async Task<bool> ConfirmAsync(int leaseId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return false;

        // Idempotent: never overwrite an existing confirmation timestamp.
        if (lease.DiscoveryConfirmedAt is not null) return true;

        lease.DiscoveryConfirmedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        LogConfirmed(_logger, leaseId);
        return true;
    }

    // ----------------------------------------------------------------------
    // GetForPmAsync — projection for the onboarding review screen.
    // ----------------------------------------------------------------------
    public async Task<IReadOnlyList<DiscoveryResult>> GetForPmAsync(int propertyManagerId, CancellationToken ct)
    {
        if (!await _flags.IsEnabledAsync(FlagKey, propertyManagerId, ct))
        {
            return Array.Empty<DiscoveryResult>();
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Leases
            .Where(l => l.PropertyManagerId == propertyManagerId)
            .Select(l => new DiscoveryResult(
                l.Id,
                l.CustomerId == null
                    ? "(unlinked lease)"
                    : db.Customers.Where(c => c.Id == l.CustomerId).Select(c => c.DisplayName).FirstOrDefault() ?? "(unknown)",
                l.InferredRentAmount,
                l.InferredDueDay,
                l.InferredState,
                l.InferredAt,
                l.DiscoveryConfirmedAt))
            .ToListAsync(ct);

        return rows;
    }

    [LoggerMessage(EventId = 9501, Level = LogLevel.Information,
        Message = "Auto-discovery skipped (flag tenancy.autodiscovery off) for PM {PmId}")]
    static partial void LogSkipped(ILogger logger, int pmId);

    [LoggerMessage(EventId = 9502, Level = LogLevel.Information,
        Message = "Auto-discovery ran for PM {PmId} — wrote {LeasesWritten} lease inferences")]
    static partial void LogRan(ILogger logger, int pmId, int leasesWritten);

    [LoggerMessage(EventId = 9503, Level = LogLevel.Information,
        Message = "Auto-discovery confirmed for lease {LeaseId}")]
    static partial void LogConfirmed(ILogger logger, int leaseId);
}
