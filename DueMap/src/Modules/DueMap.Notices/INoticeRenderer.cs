using DueMap.Notices.Domain;

namespace DueMap.Notices;

/// <summary>
/// Renders a notice template version against a variable dictionary using Scriban.
/// Validates that every entry in the template's <c>required_vars</c> is present
/// (and non-null) before rendering — failing fast is preferable to sending a
/// notice with a missing placeholder to a tenant.
/// </summary>
public interface INoticeRenderer
{
    Task<RenderedNotice> RenderAsync(
        NoticeTemplateVersion version,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken ct);
}

/// <summary>
/// Thrown when a template requires a variable that is missing or null in the
/// supplied <c>variables</c> dictionary. The render does not proceed.
/// </summary>
public sealed class MissingTemplateVariableException : Exception
{
    public IReadOnlyList<string> MissingVariables { get; }

    public MissingTemplateVariableException(IReadOnlyList<string> missing)
        : base($"Template render aborted; missing required variables: {string.Join(", ", missing)}")
    {
        MissingVariables = missing;
    }
}
