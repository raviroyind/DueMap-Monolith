using DueMap.Billing.Domain;
using DueMap.Billing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Billing.Services;

internal sealed class LateFeeAssessmentRepository : ILateFeeAssessmentRepository
{
    private readonly BillingDbContext _db;

    public LateFeeAssessmentRepository(BillingDbContext db)
    {
        _db = db;
    }

    public Task<LateFeeAssessment?> GetByPeriodAsync(int leaseId, DateOnly dueDate, CancellationToken ct) =>
        _db.LateFeeAssessments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.LeaseId == leaseId && a.DueDate == dueDate, ct);

    public async Task<IReadOnlyList<LateFeeAssessment>> ListByLeaseAsync(int leaseId, CancellationToken ct) =>
        await _db.LateFeeAssessments.AsNoTracking()
            .Where(a => a.LeaseId == leaseId)
            .OrderByDescending(a => a.DueDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LateFeeAssessment>> ListForLeasesAsync(IReadOnlyCollection<int> leaseIds, CancellationToken ct)
    {
        if (leaseIds is null || leaseIds.Count == 0) return Array.Empty<LateFeeAssessment>();
        return await _db.LateFeeAssessments.AsNoTracking()
            .Where(a => leaseIds.Contains(a.LeaseId))
            .OrderByDescending(a => a.DueDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LateFeeAssessment>> ListForLeasesInRangeAsync(
        IReadOnlyCollection<int> leaseIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        if (leaseIds is null || leaseIds.Count == 0) return Array.Empty<LateFeeAssessment>();
        return await _db.LateFeeAssessments.AsNoTracking()
            .Where(a => leaseIds.Contains(a.LeaseId)
                     && a.CreatedAt >= fromUtc
                     && a.CreatedAt <  toUtc)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<LateFeeAssessment> RecordAsync(LateFeeAssessment row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Id != 0)
        {
            throw new InvalidOperationException("LateFeeAssessment.Id must be 0 on insert.");
        }

        if (row.CreatedAt == default) row.CreatedAt = DateTime.UtcNow;
        if (row.Status == default)    row.Status    = LateFeeAssessmentStatus.Assessed;

        _db.LateFeeAssessments.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task ReverseAsync(int id, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var row = await _db.LateFeeAssessments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Late fee assessment {id} not found.");

        if (row.Status == LateFeeAssessmentStatus.Reversed)
        {
            return; // idempotent: already reversed
        }

        row.Status = LateFeeAssessmentStatus.Reversed;
        row.ReversalReason = reason;
        row.ReversedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
