using DueMap.Billing.Domain;
using DueMap.Billing.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Billing.Services;

internal sealed class PmProcessingRunRepository : IPmProcessingRunRepository
{
    private const int SqlUniqueViolation = 2627;
    private const int SqlIndexViolation  = 2601;

    private readonly BillingDbContext _db;

    public PmProcessingRunRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<PmProcessingRun?> TryStartAsync(
        int propertyManagerId,
        DateOnly businessDate,
        CancellationToken ct)
    {
        var row = new PmProcessingRun
        {
            PropertyManagerId = propertyManagerId,
            BusinessDate = businessDate,
            StartedAt = DateTime.UtcNow,
            Status = PmProcessingStatus.Started
        };

        _db.PmProcessingRuns.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.Entry(row).State = EntityState.Detached;
            return null;
        }
    }

    public async Task MarkCompletedAsync(long runId, int leasesPlanned, int actionsExecuted, CancellationToken ct)
    {
        var row = await _db.PmProcessingRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new InvalidOperationException($"Processing run {runId} not found.");
        row.Status = PmProcessingStatus.Completed;
        row.CompletedAt = DateTime.UtcNow;
        row.LeasesPlanned = leasesPlanned;
        row.ActionsExecuted = actionsExecuted;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(long runId, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var row = await _db.PmProcessingRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new InvalidOperationException($"Processing run {runId} not found.");
        row.Status = PmProcessingStatus.Failed;
        row.CompletedAt = DateTime.UtcNow;
        row.FailureReason = reason;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> ListPropertyManagersAlreadyProcessedAsync(
        DateOnly businessDate,
        CancellationToken ct) =>
        await _db.PmProcessingRuns.AsNoTracking()
            .Where(r => r.BusinessDate == businessDate
                     && (r.Status == PmProcessingStatus.Started || r.Status == PmProcessingStatus.Completed))
            .Select(r => r.PropertyManagerId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PmProcessingRun>> ListRecentAsync(int propertyManagerId, int take, CancellationToken ct) =>
        await _db.PmProcessingRuns.AsNoTracking()
            .Where(r => r.PropertyManagerId == propertyManagerId)
            .OrderByDescending(r => r.BusinessDate)
            .ThenByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(ct);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql
        && (sql.Number == SqlUniqueViolation || sql.Number == SqlIndexViolation);
}
