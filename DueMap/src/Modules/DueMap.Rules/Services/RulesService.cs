using System.Globalization;
using DueMap.Rules.Domain;
using DueMap.Rules.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DueMap.Rules.Services;

internal sealed partial class RulesService : IRulesService
{
    private readonly RulesDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly RulesOptions _options;
    private readonly ILogger<RulesService> _logger;

    public RulesService(
        RulesDbContext db,
        IMemoryCache cache,
        IOptions<RulesOptions> options,
        ILogger<RulesService> logger)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ResolvedRule?> ResolveRuleAsync(
        string stateCode,
        DateOnly assessmentDate,
        int? jurisdictionId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateCode);

        var jurisdictionKey = jurisdictionId?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var key = string.Create(CultureInfo.InvariantCulture,
            $"rule::{stateCode.ToUpperInvariant()}::{assessmentDate:yyyy-MM-dd}::{jurisdictionKey}");

        if (_cache.TryGetValue<ResolvedRule>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        // Single round-trip: state lookup + active rule version, filtered by date window.
        // The "active row" predicate is the contract enforced by the filtered unique
        // index in db/duemap_schema.sql — at most one row matches per state per date.
        var stateBase = await (
            from s in _db.States.AsNoTracking()
            join r in _db.StateRuleVersions.AsNoTracking() on s.Id equals r.StateId
            where s.Code == stateCode
               && r.EffectiveDate <= assessmentDate
               && (r.ExpiresDate == null || r.ExpiresDate > assessmentDate)
            select new { State = s, Rule = r }
        ).FirstOrDefaultAsync(ct);

        if (stateBase is null)
        {
            LogNoActiveStateRule(_logger, stateCode, assessmentDate);
            return null;
        }

        LocalRuleOverride? overrideRow = null;
        if (jurisdictionId is int jid)
        {
            overrideRow = await _db.LocalRuleOverrides.AsNoTracking()
                .Where(o => o.JurisdictionId == jid
                         && o.EffectiveDate <= assessmentDate
                         && (o.ExpiresDate == null || o.ExpiresDate > assessmentDate))
                .OrderByDescending(o => o.EffectiveDate)
                .FirstOrDefaultAsync(ct);
        }

        var resolved = Merge(stateBase.State, stateBase.Rule, overrideRow, assessmentDate);

        _cache.Set(key, resolved, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.CacheTtl
        });

        return resolved;
    }

    public async Task<ResolvedRule?> ResolveRuleByStateIdAsync(
        int stateId,
        DateOnly assessmentDate,
        int? jurisdictionId,
        CancellationToken ct)
    {
        var code = await _db.States.AsNoTracking()
            .Where(s => s.Id == stateId)
            .Select(s => s.Code)
            .FirstOrDefaultAsync(ct);

        return code is null
            ? null
            : await ResolveRuleAsync(code, assessmentDate, jurisdictionId, ct);
    }

    /// <summary>
    /// "Explicit override wins" merge semantics — see ADR / README. Any non-null
    /// override column replaces the state base value verbatim. If you ever want
    /// stricter-wins behavior, do it via explicit override rows authored by the
    /// rule editors, not by burying logic in this merge.
    /// </summary>
    private static ResolvedRule Merge(
        State state,
        StateRuleVersion baseRule,
        LocalRuleOverride? ovr,
        DateOnly assessmentDate)
    {
        var citation = ovr is null
            ? baseRule.SourceCitation
            : $"{baseRule.SourceCitation}; override: {ovr.SourceCitation}";

        return new ResolvedRule(
            StateCode: state.Code,
            StateRuleVersionId: baseRule.Id,
            LocalRuleOverrideId: ovr?.Id,
            AssessmentDate: assessmentDate,
            GracePeriodDays: ovr?.GracePeriodDays ?? baseRule.GracePeriodDays,
            LateFeeType: baseRule.LateFeeType,
            FlatAmount: ovr?.FlatAmount ?? baseRule.FlatAmount,
            PercentOfRent: ovr?.PercentOfRent ?? baseRule.PercentOfRent,
            HardCapAmount: ovr?.HardCapAmount ?? baseRule.HardCapAmount,
            NoticeRequiredBeforeFee: baseRule.NoticeRequiredBeforeFee,
            NoticeAdvanceDays: ovr?.NoticeAdvanceDays ?? baseRule.NoticeAdvanceDays,
            SourceCitation: citation);
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "No active state rule for {StateCode} on {AssessmentDate}")]
    static partial void LogNoActiveStateRule(ILogger logger, string stateCode, DateOnly assessmentDate);
}
