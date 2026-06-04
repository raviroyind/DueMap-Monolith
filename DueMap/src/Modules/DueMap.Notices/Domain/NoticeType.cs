namespace DueMap.Notices.Domain;

public sealed class NoticeType
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public int LegalPriority { get; set; }
    public DateTime CreatedAt { get; set; }
}
