namespace DueMap.Notices.Domain;

/// <summary>
/// The subject/body that should actually be rendered for a delivery, after
/// applying any PM override on top of the system template. Carries provenance
/// pointers so the audit row can record exactly which sources fed the render.
/// </summary>
public sealed record RenderableTemplate(
    int SystemTemplateVersionId,
    int? PmTemplateOverrideId,
    string Subject,
    string BodyHtml,
    string BodyText,
    string RequiredVars);
