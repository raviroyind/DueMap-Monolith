namespace DueMap.Notices.Domain;

public sealed record RenderedNotice(
    int TemplateVersionId,
    string Subject,
    string BodyHtml,
    string BodyText);
