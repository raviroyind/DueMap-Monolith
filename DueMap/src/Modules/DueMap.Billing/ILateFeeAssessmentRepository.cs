using DueMap.Billing.Domain;

namespace DueMap.Billing;

public interface ILateFeeAssessmentRepository
{
    Task<LateFeeAssessment?> GetByPeriodAsync(int leaseId, DateOnly dueDate, CancellationToken ct);

    Task<IReadOnlyList<LateFeeAssessment>> ListByLeaseAsync(int leaseId, CancellationToken ct);

    /// <summary>
    /// All late-fee assessments across a set of lease IDs. Used by the /leases
    /// list page to mark rows where a fee has already posted for the current
    /// cycle. The caller (Tenancy / Web) supplies the lease IDs because the
    /// Billing module doesn't know which leases belong to which PM.
    /// </summary>
    Task<IReadOnlyList<LateFeeAssessment>> ListForLeasesAsync(IReadOnlyCollection<int> leaseIds, CancellationToken ct);

    /// <summary>
    /// Late-fee rows whose CreatedAt falls in the half-open range [fromUtc, toUtc),
    /// scoped to a set of lease IDs. Used by the Daily Close Report to summarise
    /// fees assessed in the last 24 hours.
    /// </summary>
    Task<IReadOnlyList<LateFeeAssessment>> ListForLeasesInRangeAsync(
        IReadOnlyCollection<int> leaseIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>
    /// Insert a new assessment. Returns the persisted entity. Throws
    /// <see cref="InvalidOperationException"/> if one already exists for the
    /// same (lease, due_date) — the schema enforces "at most one fee per
    /// period."
    /// </summary>
    Task<LateFeeAssessment> RecordAsync(LateFeeAssessment row, CancellationToken ct);

    /// <summary>
    /// Reverse an already-assessed fee. Records the reason and timestamp;
    /// the original row is not mutated beyond status/reversal columns.
    /// </summary>
    Task ReverseAsync(int id, string reason, CancellationToken ct);
}
