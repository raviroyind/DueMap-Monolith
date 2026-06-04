using DueMap.Notices.Domain;

namespace DueMap.Notices;

public interface INoticeTypeReader
{
    Task<IReadOnlyList<NoticeType>> ListAllAsync(CancellationToken ct);
}
