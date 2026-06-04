using DueMap.Billing.Domain;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;

namespace DueMap.Billing.Services;

internal sealed class EffectivePolicyService : IEffectivePolicyService
{
    private readonly IPmNoticePreferencesService _pmPrefs;
    private readonly ILeaseNoticeSettingsService _leaseSettings;
    private readonly IRulesService _rules;

    public EffectivePolicyService(
        IPmNoticePreferencesService pmPrefs,
        ILeaseNoticeSettingsService leaseSettings,
        IRulesService rules)
    {
        _pmPrefs = pmPrefs;
        _leaseSettings = leaseSettings;
        _rules = rules;
    }

    public async Task<EffectiveLeasePolicy> BuildAsync(
        Lease lease,
        DateOnly assessmentDate,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var pmPrefs       = await _pmPrefs.GetOrCreateAsync(lease.PropertyManagerId, ct);
        var leaseSettings = await _leaseSettings.GetAsync(lease.Id, ct);
        var stateRule     = await _rules.ResolveRuleByStateIdAsync(
            stateId: lease.StateId,
            assessmentDate: assessmentDate,
            jurisdictionId: lease.JurisdictionId,
            ct: ct);

        var stateMinGrace = stateRule?.GracePeriodDays ?? 0;
        var requestedGrace = leaseSettings?.PostDueGraceDays ?? pmPrefs.PostDueDefaultGraceDays;
        var effectiveGrace = Math.Max(requestedGrace, stateMinGrace);

        // Master toggles act as kill switches AND-ed with per-lease enable flags.
        // Per-lease defaults to true so "row not yet created" still receives notices.
        var preDueEnabled  = pmPrefs.PreDueMasterEnabled  && (leaseSettings?.PreDueEnabled  ?? true);
        var dueDateEnabled = pmPrefs.DueDateMasterEnabled && (leaseSettings?.DueDateEnabled ?? true);
        var postDueEnabled = pmPrefs.PostDueMasterEnabled && (leaseSettings?.PostDueEnabled ?? true);

        return new EffectiveLeasePolicy(
            LeaseId: lease.Id,
            PropertyManagerId: lease.PropertyManagerId,
            StateId: lease.StateId,
            JurisdictionId: lease.JurisdictionId,
            PreDueEnabled: preDueEnabled,
            PreDueDaysBefore: leaseSettings?.PreDueDaysBefore ?? pmPrefs.PreDueDefaultDaysBefore,
            DueDateEnabled: dueDateEnabled,
            PostDueEnabled: postDueEnabled,
            PostDueMode: leaseSettings?.PostDueMode ?? pmPrefs.PostDueDefaultMode,
            EffectiveGraceDays: effectiveGrace,
            RequestedGraceDays: requestedGrace,
            StateMinimumGraceDays: stateMinGrace,
            MonthlyRent: lease.MonthlyRent);
    }

}
