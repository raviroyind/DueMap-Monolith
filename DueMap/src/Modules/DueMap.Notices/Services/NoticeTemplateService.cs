using System.Globalization;
using DueMap.Notices.Domain;
using DueMap.Notices.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DueMap.Notices.Services;

internal sealed partial class NoticeTemplateService : INoticeTemplateService
{
    private readonly NoticesDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly NoticesOptions _options;
    private readonly ILogger<NoticeTemplateService> _logger;

    public NoticeTemplateService(
        NoticesDbContext db,
        IMemoryCache cache,
        IOptions<NoticesOptions> options,
        ILogger<NoticeTemplateService> logger)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NoticeTemplateVersion?> ResolveActiveVersionAsync(
        int? stateId,
        string noticeTypeCode,
        DateOnly asOf,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noticeTypeCode);

        var key = string.Create(CultureInfo.InvariantCulture,
            $"ntv::{stateId?.ToString(CultureInfo.InvariantCulture) ?? "_"}::{noticeTypeCode}::{asOf:yyyy-MM-dd}");

        if (_cache.TryGetValue<NoticeTemplateVersion>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        // Prefer the state-specific active template; fall back to a generic
        // (state_id IS NULL) template if none exists for this state.
        var templateId = await ResolveTemplateIdAsync(stateId, noticeTypeCode, preferStateSpecific: true, ct);
        if (templateId is null)
        {
            LogNoTemplate(_logger, stateId, noticeTypeCode);
            return null;
        }

        // Latest approved version with effective_date <= asOf.
        var version = await _db.NoticeTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == templateId
                     && v.Status == TemplateVersionStatus.Approved
                     && v.EffectiveDate <= asOf)
            .OrderByDescending(v => v.EffectiveDate)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (version is null)
        {
            LogNoApprovedVersion(_logger, templateId.Value, asOf);
            return null;
        }

        _cache.Set(key, version, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.TemplateCacheTtl
        });

        return version;
    }

    public async Task<RenderableTemplate?> ResolveRenderableAsync(
        int propertyManagerId,
        int? stateId,
        string noticeTypeCode,
        DateOnly asOf,
        CancellationToken ct)
    {
        var systemVersion = await ResolveActiveVersionAsync(stateId, noticeTypeCode, asOf, ct);
        if (systemVersion is null) return null;

        // Look up the notice_type id once so we can match against pm_template_overrides.
        var noticeTypeId = await _db.NoticeTypes.AsNoTracking()
            .Where(t => t.Code == noticeTypeCode)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (noticeTypeId is null) return null;

        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"pto::{propertyManagerId}::{noticeTypeId}::{stateId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "_"}");

        var ovr = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _options.TemplateCacheTtl;

            // Prefer the state-specific active override; fall back to a generic PM override.
            var candidates = await _db.PmTemplateOverrides.AsNoTracking()
                .Where(o => o.IsActive
                         && o.PropertyManagerId == propertyManagerId
                         && o.NoticeTypeId == noticeTypeId.Value
                         && (o.StateId == stateId || o.StateId == null))
                .ToListAsync(ct);

            return stateId is int sid
                ? candidates.FirstOrDefault(c => c.StateId == sid)
                  ?? candidates.FirstOrDefault(c => c.StateId == null)
                : candidates.FirstOrDefault(c => c.StateId == null);
        });

        return new RenderableTemplate(
            SystemTemplateVersionId: systemVersion.Id,
            PmTemplateOverrideId: ovr?.Id,
            Subject: ovr?.Subject ?? systemVersion.Subject,
            BodyHtml: ovr?.BodyHtml ?? systemVersion.BodyHtml,
            BodyText: ovr?.BodyText ?? systemVersion.BodyText,
            RequiredVars: systemVersion.RequiredVars);
    }

    private async Task<int?> ResolveTemplateIdAsync(
        int? stateId,
        string noticeTypeCode,
        bool preferStateSpecific,
        CancellationToken ct)
    {
        var query =
            from t in _db.NoticeTemplates.AsNoTracking()
            join nt in _db.NoticeTypes.AsNoTracking() on t.NoticeTypeId equals nt.Id
            where t.IsActive && nt.Code == noticeTypeCode
            select new { t.Id, t.StateId };

        var candidates = await query.ToListAsync(ct);

        if (preferStateSpecific && stateId is int sid)
        {
            var specific = candidates.FirstOrDefault(c => c.StateId == sid);
            if (specific is not null) return specific.Id;
        }

        return candidates.FirstOrDefault(c => c.StateId is null)?.Id;
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning,
        Message = "No active template found for state={StateId} type={NoticeTypeCode}")]
    static partial void LogNoTemplate(ILogger logger, int? stateId, string noticeTypeCode);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "No approved version for template {TemplateId} as of {AsOf}")]
    static partial void LogNoApprovedVersion(ILogger logger, int templateId, DateOnly asOf);
}
