using DueMap.Notices.Domain;

namespace DueMap.Notices;

/// <summary>
/// Resolves which template version applies to a given (state, notice type, date).
/// State-specific templates win over the generic fallback; among versions, the
/// latest <c>approved</c> row with <c>effective_date &lt;= asOf</c> wins.
/// </summary>
public interface INoticeTemplateService
{
    /// <summary>
    /// Resolve the active <see cref="NoticeTemplateVersion"/> for the given
    /// context, or <c>null</c> if neither a state-specific nor generic template
    /// has an approved version in effect.
    /// </summary>
    Task<NoticeTemplateVersion?> ResolveActiveVersionAsync(
        int? stateId,
        string noticeTypeCode,
        DateOnly asOf,
        CancellationToken ct);

    /// <summary>
    /// Resolve the final renderable subject/body for a specific PM. PM overrides
    /// (state-specific, then generic) replace the system template's subject and
    /// body. The system template's <c>required_vars</c> always wins — PMs cannot
    /// drop legally-required placeholders.
    /// </summary>
    Task<RenderableTemplate?> ResolveRenderableAsync(
        int propertyManagerId,
        int? stateId,
        string noticeTypeCode,
        DateOnly asOf,
        CancellationToken ct);
}
