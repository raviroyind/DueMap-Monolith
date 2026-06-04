namespace DueMap.Notices.Domain;

public sealed class NoticeTemplateVersion
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int VersionNumber { get; set; }

    public string Subject { get; set; } = default!;
    public string BodyHtml { get; set; } = default!;
    public string BodyText { get; set; } = default!;

    /// <summary>JSON array of placeholder names this template requires.</summary>
    public string RequiredVars { get; set; } = "[]";

    public DateOnly EffectiveDate { get; set; }
    public string? ApprovedBy { get; set; }
    public TemplateVersionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
