namespace DueMap.Notices.Domain;

public sealed class NoticeTemplate
{
    public int Id { get; set; }

    /// <summary>NULL = generic fallback template (not state-specific).</summary>
    public int? StateId { get; set; }

    public int NoticeTypeId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
