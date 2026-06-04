using DueMap.Notices.Domain;
using DueMap.Notices.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Notices.Services;

internal sealed class NoticeTypeReader : INoticeTypeReader
{
    private readonly NoticesDbContext _db;

    public NoticeTypeReader(NoticesDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NoticeType>> ListAllAsync(CancellationToken ct) =>
        await _db.NoticeTypes.AsNoTracking()
            .OrderBy(t => t.LegalPriority)
            .ToListAsync(ct);
}
