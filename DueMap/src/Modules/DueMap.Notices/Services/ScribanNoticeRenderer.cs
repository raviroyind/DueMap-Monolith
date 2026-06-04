using System.Globalization;
using System.Text.Json;
using DueMap.Notices.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Scriban;
using Scriban.Runtime;

namespace DueMap.Notices.Services;

/// <summary>
/// Renders a template version with Scriban. Compiled <see cref="Template"/>
/// instances are cached by (version id, field) to avoid the parse cost on the
/// hot path — Scriban parses are not cheap and templates are immutable once
/// approved, so caching is safe.
/// </summary>
internal sealed class ScribanNoticeRenderer : INoticeRenderer
{
    private readonly IMemoryCache _cache;
    private readonly NoticesOptions _options;

    public ScribanNoticeRenderer(IMemoryCache cache, IOptions<NoticesOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<RenderedNotice> RenderAsync(
        NoticeTemplateVersion version,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(variables);

        var required = ParseRequiredVars(version.RequiredVars);
        var missing = required
            .Where(name => !variables.TryGetValue(name, out var v) || v is null)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new MissingTemplateVariableException(missing);
        }

        var scriptObject = new ScriptObject();
        foreach (var (k, v) in variables)
        {
            scriptObject[k] = v;
        }
        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        var subject  = await RenderFieldAsync(version.Id, "subject",  version.Subject,  context, ct);
        var html     = await RenderFieldAsync(version.Id, "html",     version.BodyHtml, context, ct);
        var text     = await RenderFieldAsync(version.Id, "text",     version.BodyText, context, ct);

        return new RenderedNotice(version.Id, subject, html, text);
    }

    private async Task<string> RenderFieldAsync(
        int versionId,
        string field,
        string templateSource,
        TemplateContext context,
        CancellationToken ct)
    {
        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"ntv-compiled::{versionId}::{field}");
        var compiled = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _options.CompiledTemplateCacheTtl;
            var template = Template.Parse(templateSource);
            if (template.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Template version {versionId} field '{field}' failed to parse: " +
                    string.Join("; ", template.Messages));
            }
            return template;
        })!;

        ct.ThrowIfCancellationRequested();
        return await compiled.RenderAsync(context).ConfigureAwait(false);
    }

    private static string[] ParseRequiredVars(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
