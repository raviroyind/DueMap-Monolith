using DueMap.Tenancy.Domain;

namespace DueMap.Tenancy;

/// <summary>
/// Narrow read surface consumed by the Billing AssessmentPlanner: "is this
/// lease's notice sequence currently paused by a promise?" Split from the full
/// service so the planner (pure decision logic) can't accidentally mutate.
/// </summary>
public interface IPaymentPromiseReader
{
    /// <summary>The lease's Active promise, or null. At most one exists per lease.</summary>
    Task<PaymentPromise?> GetActiveForLeaseAsync(int leaseId, CancellationToken ct);
}

public interface IPaymentPromiseService : IPaymentPromiseReader
{
    /// <summary>
    /// Log a promise. Validates the lease belongs to the PM, the date isn't in
    /// the past, and no other Active promise exists for the lease (cancel the
    /// old one first — two overlapping verbal agreements is a data-entry error).
    /// </summary>
    Task<PaymentPromise> CreateAsync(NewPromiseInput input, CancellationToken ct);

    /// <summary>PM withdraws an Active promise; notices resume on the next run.</summary>
    Task CancelAsync(int promiseId, int propertyManagerId, CancellationToken ct);

    /// <summary>Full promise history for a lease, newest first (activity timeline).</summary>
    Task<IReadOnlyList<PaymentPromise>> ListForLeaseAsync(int leaseId, CancellationToken ct);

    /// <summary>Promises broken on/after <paramref name="since"/> for the Today queue.</summary>
    Task<IReadOnlyList<PaymentPromise>> ListRecentlyBrokenAsync(int propertyManagerId, DateOnly since, CancellationToken ct);

    /// <summary>
    /// Resolve every Active promise whose date has passed: the balance still
    /// open on invoices due on-or-before the promised date decides Kept vs
    /// Broken. Runs at the top of the PM's daily close (so a broken promise
    /// resumes escalation the SAME run) and when the Today queue loads (so the
    /// 8am view is fresh even if the worker hasn't ticked). Idempotent.
    /// Returns the number of promises newly marked Broken.
    /// </summary>
    Task<int> ResolveDueAsync(int propertyManagerId, DateOnly today, CancellationToken ct);
}

/// <summary>Caller-supplied data for a new promise.</summary>
public sealed record NewPromiseInput(
    int PropertyManagerId,
    int LeaseId,
    decimal? Amount,
    DateOnly PromisedDate,
    string? Note);
