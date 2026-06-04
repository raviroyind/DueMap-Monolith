using DueMap.Notices.Domain;
using DueMap.Notices.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Notices.Services;

internal sealed class NoticeDeliveryRepository : INoticeDeliveryRepository
{
    private readonly NoticesDbContext _db;

    public NoticeDeliveryRepository(NoticesDbContext db)
    {
        _db = db;
    }

    public async Task<NoticeDelivery> RecordAsync(NoticeDelivery delivery, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (delivery.Id != 0)
        {
            throw new InvalidOperationException(
                "NoticeDelivery.Id must be 0 on insert; deliveries are append-only.");
        }

        if (delivery.SentAt == default)
        {
            delivery.SentAt = DateTime.UtcNow;
        }

        _db.NoticeDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
        return delivery;
    }

    public async Task<IReadOnlyDictionary<int, NoticeDelivery>> ListLatestByLeaseAsync(int propertyManagerId, CancellationToken ct)
    {
        // Per-lease "most recent delivery" via group-then-pick. EF turns this
        // into a window-function or correlated subquery depending on provider;
        // for the modest row counts we deal with the cost is negligible.
        var latest = await _db.NoticeDeliveries.AsNoTracking()
            .Where(d => d.PropertyManagerId == propertyManagerId)
            .GroupBy(d => d.LeaseId)
            .Select(g => g.OrderByDescending(d => d.SentAt).First())
            .ToListAsync(ct);

        return latest.ToDictionary(d => d.LeaseId);
    }

    public async Task<IReadOnlyList<NoticeDelivery>> ListByLeaseAsync(int leaseId, CancellationToken ct) =>
        await _db.NoticeDeliveries.AsNoTracking()
            .Where(d => d.LeaseId == leaseId)
            .OrderByDescending(d => d.SentAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NoticeDelivery>> ListSentInRangeForPmAsync(
        int propertyManagerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await _db.NoticeDeliveries.AsNoTracking()
            .Where(d => d.PropertyManagerId == propertyManagerId
                     && d.SentAt >= fromUtc
                     && d.SentAt <  toUtc)
            .OrderByDescending(d => d.SentAt)
            .ToListAsync(ct);
}
