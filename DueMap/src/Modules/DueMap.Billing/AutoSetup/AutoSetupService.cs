using System.Text.Json;
using DueMap.Common.FeatureFlags;
using DueMap.Rules;
using DueMap.Rules.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging;
using DomainLateFeeType = DueMap.Rules.Domain.LateFeeType;

namespace DueMap.Billing.AutoSetup;

/// <summary>
/// Deterministic AutoSetup. No LLM in the money path.
///
/// Per lease:
/// <list type="number">
///   <item>Resolve the state — prefer <c>lease.StateId</c>, fall back to the
///   P1-1 <c>InferredState</c> code, else flag ambiguous.</item>
///   <item>Resolve <see cref="ResolvedRule"/> via <see cref="IRulesService"/>.</item>
///   <item>Pick the proposed fee % — <c>safe_default_pct</c> for
///   reasonableness states, otherwise the rule's recommended
///   <c>PercentOfRent</c>.</item>
///   <item><strong>Clamp</strong> via <see cref="ResolvedRule.ClampToStateMax"/>
///   (this is the §6 guardrail — the PM cannot configure their way past it).</item>
///   <item>Persist with <c>FeesStaged = true</c> via
///   <see cref="ILeaseWriter.UpdateLateFeeProfileAsync"/>.</item>
/// </list>
///
/// After all leases:
/// <list type="number">
///   <item>Run <see cref="IComplianceScanService"/> over the in-memory
///   scan rows (NOT a DB re-read — the writes already happened).</item>
///   <item>Serialize <see cref="AutoSetupSummary"/> to JSON.</item>
///   <item>Stamp the PM row via
///   <see cref="IPropertyManagerWriter.RecordAutoSetupSummaryAsync"/>.</item>
/// </list>
/// </summary>
internal sealed partial class AutoSetupService : IAutoSetupService
{
    private const string FlagKey = "onboarding.auto_setup";

    private readonly ILeaseReader            _leases;
    private readonly ILeaseWriter            _leaseWriter;
    private readonly IPropertyManagerWriter  _pmWriter;
    private readonly IPmNoticePreferencesService _pmPrefs;
    private readonly IRulesService           _rules;
    private readonly IStateReader            _states;
    private readonly IComplianceScanService  _scan;
    private readonly IFeatureFlags           _flags;
    private readonly ILogger<AutoSetupService> _logger;

    public AutoSetupService(
        ILeaseReader leases,
        ILeaseWriter leaseWriter,
        IPropertyManagerWriter pmWriter,
        IPmNoticePreferencesService pmPrefs,
        IRulesService rules,
        IStateReader states,
        IComplianceScanService scan,
        IFeatureFlags flags,
        ILogger<AutoSetupService> logger)
    {
        _leases = leases;
        _leaseWriter = leaseWriter;
        _pmWriter = pmWriter;
        _pmPrefs = pmPrefs;
        _rules = rules;
        _states = states;
        _scan = scan;
        _flags = flags;
        _logger = logger;
    }

    public async Task<AutoSetupSummary> RunAsync(int propertyManagerId, CancellationToken ct)
    {
        var ranAt = DateTime.UtcNow;

        if (!await _flags.IsEnabledAsync(FlagKey, propertyManagerId, ct))
        {
            LogSkipped(_logger, propertyManagerId);
            return new AutoSetupSummary(0, 0, ranAt, Array.Empty<ComplianceFinding>());
        }

        // ---- Pre-load lookups one time, not per lease. ---------------------
        var today = DateOnly.FromDateTime(ranAt);
        var states = await _states.ListAllAsync(ct);
        var codeToState = states.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var idToState   = states.ToDictionary(s => s.Id);

        // Materialise PM-level notice preferences so the row exists with the
        // sane defaults (PreDue/Due/PostDue master toggles ON, grace = 5).
        // EffectivePolicyService then floors that grace per-lease against
        // the state min when assessments run.
        _ = await _pmPrefs.GetOrCreateAsync(propertyManagerId, ct);

        // ---- Walk every active lease. -------------------------------------
        var leases = await _leases.ListActiveAsync(propertyManagerId, today, ct);
        var scanned = new List<ScannedLease>(capacity: leases.Count);
        var processed = 0;
        var skipped   = 0;

        foreach (var lease in leases)
        {
            var (stateCode, rule) = await ResolveStateAndRuleAsync(lease, today, codeToState, idToState, ct);

            // Reasonableness states get the safe default %, otherwise we
            // mirror the rule's recommended PercentOfRent (stored as a
            // fraction, e.g. 0.05 → 5%).
            decimal? proposedPct = null;
            if (rule is { StandardKind: 2, SafeDefaultPct: decimal safe })
            {
                proposedPct = safe;
            }
            else if (rule?.PercentOfRent is decimal frac)
            {
                proposedPct = frac * 100m;
            }
            else if (rule is null)
            {
                // No rule means we don't have a defensible default — skip the
                // money write entirely. ComplianceScanService will flag it.
                scanned.Add(new ScannedLease(lease.Id, stateCode, Rule: null,
                    AssignedFeePercent: null, AssignedFeeFlatAmount: null,
                    MonthlyRent: lease.MonthlyRent));
                skipped++;
                continue;
            }

            // Clamp via the §6 guardrail. The cap on % is stored as a whole
            // number (5.00 = 5%); same units as proposedPct here, so a
            // straight Math.Min is the correct clamp.
            decimal? finalPct = proposedPct;
            if (rule!.StateMaxPercent is decimal maxPct && finalPct is decimal p)
            {
                finalPct = Math.Min(p, maxPct);
            }

            // Map the rule's late-fee type onto the lease columns. We carry
            // only Flat / Percent today — GreaterOf/LesserOf require BOTH a
            // flat and a percent and AutoSetup writes one or the other.
            byte storedType = rule.LateFeeType switch
            {
                DomainLateFeeType.Flat     => 1,
                DomainLateFeeType.Percent  => 2,
                DomainLateFeeType.GreaterOf=> 2,  // pick percent; cap still clamps
                DomainLateFeeType.LesserOf => 2,
                _ => 2
            };

            var graceDays = (byte)rule.ClampGrace(requestedDays: 3);
            var profile = new LateFeeProfileInput(
                LateFeeType:        storedType,
                LateFeePercent:     finalPct,
                LateFeeFlatAmount:  rule.FlatAmount,        // may be NULL
                GraceDays:          graceDays,
                DailyAccrual:       rule.DailyAccrualOk);

            await _leaseWriter.UpdateLateFeeProfileAsync(lease.Id, profile, ct);

            scanned.Add(new ScannedLease(
                lease.Id, stateCode, rule,
                AssignedFeePercent:    finalPct,
                AssignedFeeFlatAmount: rule.FlatAmount,
                MonthlyRent:           lease.MonthlyRent));
            processed++;
        }

        // ---- Compliance scan + persist summary. ---------------------------
        var findings = _scan.Scan(scanned);
        var summary = new AutoSetupSummary(processed, skipped, ranAt, findings);
        var json = JsonSerializer.Serialize(summary);
        await _pmWriter.RecordAutoSetupSummaryAsync(propertyManagerId, json, ct);

        LogCompleted(_logger, propertyManagerId, processed, skipped, findings.Count);
        return summary;
    }

    /// <summary>
    /// Lease's explicit StateId wins; the P1-1 inferred state code is the
    /// fallback; nothing means "ambiguous" — return (null, null) and let
    /// the compliance scan flag it.
    /// </summary>
    private async Task<(string? StateCode, ResolvedRule? Rule)> ResolveStateAndRuleAsync(
        Lease lease,
        DateOnly today,
        Dictionary<string, State> codeToState,
        Dictionary<int, State> idToState,
        CancellationToken ct)
    {
        // 1. Explicit StateId set on the lease.
        if (idToState.TryGetValue(lease.StateId, out var explicitState))
        {
            var rule = await _rules.ResolveRuleByStateIdAsync(lease.StateId, today, lease.JurisdictionId, ct);
            return (explicitState.Code, rule);
        }

        // 2. P1-1 inferred state code — try to map.
        if (!string.IsNullOrWhiteSpace(lease.InferredState)
            && codeToState.TryGetValue(lease.InferredState, out var inferred))
        {
            var rule = await _rules.ResolveRuleByStateIdAsync(inferred.Id, today, lease.JurisdictionId, ct);
            return (inferred.Code, rule);
        }

        // 3. Nothing.
        return (null, null);
    }

    [LoggerMessage(EventId = 9601, Level = LogLevel.Information,
        Message = "AutoSetup skipped (flag onboarding.auto_setup off) for PM {PmId}")]
    static partial void LogSkipped(ILogger logger, int pmId);

    [LoggerMessage(EventId = 9602, Level = LogLevel.Information,
        Message = "AutoSetup completed for PM {PmId} — processed={Processed} skipped={Skipped} findings={Findings}")]
    static partial void LogCompleted(ILogger logger, int pmId, int processed, int skipped, int findings);
}
