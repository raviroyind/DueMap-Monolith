using DueMap.Notices.Domain;

namespace DueMap.Notices;

/// <summary>
/// Append-only writes to <c>notices.notice_deliveries</c>. The table is the
/// audit trail of every notice ever sent; rows must never be updated or
/// deleted after insertion. DB-level INSTEAD OF triggers enforce this; this
/// repository exposes only the operations the application is allowed to do.
/// </summary>
public interface INoticeDeliveryRepository
{
    /// <summary>
    /// Persist a fully-rendered delivery row. The caller has already produced
    /// the rendered subject/body and chosen the channel.
    /// </summary>
    Task<NoticeDelivery> RecordAsync(NoticeDelivery delivery, CancellationToken ct);

    /// <summary>
    /// For each lease in the PM's portfolio, the most recently sent delivery
    /// (by SentAt). Used by the /leases list page to show "Last notice" at a
    /// glance. Leases with no deliveries are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, NoticeDelivery>> ListLatestByLeaseAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Full notice history for one lease, newest first. Drives the activity
    /// timeline on the lease detail page.
    /// </summary>
    Task<IReadOnlyList<NoticeDelivery>> ListByLeaseAsync(int leaseId, CancellationToken ct);

    /// <summary>
    /// All deliveries sent for a PM whose SentAt falls in the half-open range
    /// [fromUtc, toUtc). Used by the Daily Close Report to summarise yesterday's
    /// notice traffic. Newest first.
    /// </summary>
    Task<IReadOnlyList<NoticeDelivery>> ListSentInRangeForPmAsync(
        int propertyManagerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
