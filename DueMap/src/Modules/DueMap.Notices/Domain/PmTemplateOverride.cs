namespace DueMap.Notices.Domain;

/// <summary>
/// PM-authored custom copy that overrides the system template's subject/body
/// for a given notice type (and optionally a specific state). The system
/// template version still feeds the <c>required_vars</c> contract — PMs can
/// rewrite the wording but cannot drop legally-required placeholders.
/// </summary>
public sealed class PmTemplateOverride
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int NoticeTypeId { get; set; }

    /// <summary>NULL = applies to all states for this PM (generic).</summary>
    public int? StateId { get; set; }

    public string Subject { get; set; } = default!;
    public string BodyHtml { get; set; } = default!;
    public string BodyText { get; set; } = default!;

    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
