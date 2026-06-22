using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DueMap.Tenancy.Services;

/// <summary>
/// EF-backed implementation. One DbContext per call (factory pattern). The
/// flip + status advance happen in a single SaveChanges so a PM never lands
/// half-live (some leases assessable, status still pre-Active).
/// </summary>
internal sealed partial class GoLiveService : IGoLiveService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;
    private readonly ILogger<GoLiveService> _logger;

    public GoLiveService(
        IDbContextFactory<TenancyDbContext> dbFactory,
        ILogger<GoLiveService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<GoLiveResult> GoLiveAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var pm = await db.PropertyManagers.FirstOrDefaultAsync(p => p.Id == propertyManagerId, ct)
            ?? throw new InvalidOperationException($"PropertyManager {propertyManagerId} not found.");

        var wasAlreadyActive = pm.OnboardingStatus == OnboardingStatus.Active;

        // Flip ONLY the staged leases. Leases that were already live (manual
        // setup, or a prior partial go-live) are left untouched so the count
        // reflects what THIS call changed.
        var staged = await db.Leases
            .Where(l => l.PropertyManagerId == propertyManagerId && l.FeesStaged)
            .ToListAsync(ct);

        foreach (var lease in staged)
        {
            lease.FeesStaged = false;
        }

        // Advance status — never regress (mirrors SetMinimumStatusAsync).
        if ((int)OnboardingStatus.Active > (int)pm.OnboardingStatus)
        {
            pm.OnboardingStatus = OnboardingStatus.Active;
        }

        if (staged.Count > 0 || !wasAlreadyActive)
        {
            await db.SaveChangesAsync(ct);
        }

        LogWentLive(_logger, propertyManagerId, staged.Count, wasAlreadyActive);
        return new GoLiveResult(staged.Count, wasAlreadyActive);
    }

    [LoggerMessage(EventId = 9701, Level = LogLevel.Information,
        Message = "PM {PmId} went live — {LeasesActivated} staged lease(s) activated (wasAlreadyActive={WasAlreadyActive})")]
    static partial void LogWentLive(ILogger logger, int pmId, int leasesActivated, bool wasAlreadyActive);
}
