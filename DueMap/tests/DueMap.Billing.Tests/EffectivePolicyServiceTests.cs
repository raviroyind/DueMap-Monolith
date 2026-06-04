using DueMap.Billing.Services;
using DueMap.Rules;
using DueMap.Rules.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

public sealed class EffectivePolicyServiceTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly IPmNoticePreferencesService _pmPrefs = Substitute.For<IPmNoticePreferencesService>();
    private readonly ILeaseNoticeSettingsService _leaseSettings = Substitute.For<ILeaseNoticeSettingsService>();
    private readonly IRulesService _rules = Substitute.For<IRulesService>();

    private EffectivePolicyService NewSut() => new(_pmPrefs, _leaseSettings, _rules);

    private static Lease Lease(int id = 1, int pmId = 10, int stateId = 5, decimal rent = 2000m) =>
        new() { Id = id, PropertyManagerId = pmId, StateId = stateId, MonthlyRent = rent,
                StartDate = new DateOnly(2024, 1, 1) };

    private static PmNoticePreferences DefaultPrefs(int pmId, Action<PmNoticePreferences>? mut = null)
    {
        var p = new PmNoticePreferences
        {
            PropertyManagerId = pmId,
            PreDueMasterEnabled = true,
            PreDueDefaultDaysBefore = 3,
            DueDateMasterEnabled = true,
            PostDueMasterEnabled = true,
            PostDueDefaultMode = PostDueMode.GracePeriod,
            PostDueDefaultGraceDays = 5
        };
        mut?.Invoke(p);
        return p;
    }

    private static ResolvedRule StateRuleWithGrace(int graceDays) => new(
        StateCode: "CA",
        StateRuleVersionId: 1,
        LocalRuleOverrideId: null,
        AssessmentDate: Today,
        GracePeriodDays: graceDays,
        LateFeeType: LateFeeType.Percent,
        FlatAmount: null,
        PercentOfRent: 0.05m,
        HardCapAmount: null,
        NoticeRequiredBeforeFee: false,
        NoticeAdvanceDays: null,
        SourceCitation: "test");

    private void StubPmPrefs(int pmId, PmNoticePreferences prefs) =>
        _pmPrefs.GetOrCreateAsync(pmId, Arg.Any<CancellationToken>()).Returns(prefs);

    private void StubLeaseSettings(int leaseId, LeaseNoticeSettings? settings) =>
        _leaseSettings.GetAsync(leaseId, Arg.Any<CancellationToken>()).Returns(settings);

    private void StubStateRule(int stateId, ResolvedRule? rule) =>
        _rules.ResolveRuleByStateIdAsync(stateId, Arg.Any<DateOnly>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
              .Returns(rule);

    [Fact]
    public async Task PreDue_master_off_disables_pre_due_even_when_lease_enabled()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId, p => p.PreDueMasterEnabled = false));
        StubLeaseSettings(lease.Id, new LeaseNoticeSettings { LeaseId = lease.Id, PreDueEnabled = true });
        StubStateRule(lease.StateId, StateRuleWithGrace(0));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.False(policy.PreDueEnabled);
    }

    [Fact]
    public async Task PreDue_lease_off_disables_pre_due_even_when_master_on()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId));
        StubLeaseSettings(lease.Id, new LeaseNoticeSettings { LeaseId = lease.Id, PreDueEnabled = false });
        StubStateRule(lease.StateId, StateRuleWithGrace(0));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.False(policy.PreDueEnabled);
    }

    [Fact]
    public async Task PreDue_days_before_lease_override_wins_over_pm_default()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId, p => p.PreDueDefaultDaysBefore = 3));
        StubLeaseSettings(lease.Id, new LeaseNoticeSettings { LeaseId = lease.Id, PreDueDaysBefore = 7 });
        StubStateRule(lease.StateId, StateRuleWithGrace(0));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.Equal(7, policy.PreDueDaysBefore);
    }

    [Fact]
    public async Task PreDue_days_before_falls_back_to_pm_default_when_no_lease_override()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId, p => p.PreDueDefaultDaysBefore = 4));
        StubLeaseSettings(lease.Id, null);   // no lease row yet
        StubStateRule(lease.StateId, StateRuleWithGrace(0));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.Equal(4, policy.PreDueDaysBefore);
    }

    [Fact]
    public async Task State_law_floor_raises_grace_when_pm_value_is_lower()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId, p => p.PostDueDefaultGraceDays = 2));
        StubLeaseSettings(lease.Id, null);
        StubStateRule(lease.StateId, StateRuleWithGrace(graceDays: 5));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.Equal(5, policy.EffectiveGraceDays);
        Assert.Equal(2, policy.RequestedGraceDays);
        Assert.Equal(5, policy.StateMinimumGraceDays);
        Assert.True(policy.GraceWasRaisedByLaw);
    }

    [Fact]
    public async Task State_law_floor_does_nothing_when_pm_value_meets_minimum()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId, p => p.PostDueDefaultGraceDays = 10));
        StubLeaseSettings(lease.Id, null);
        StubStateRule(lease.StateId, StateRuleWithGrace(graceDays: 5));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.Equal(10, policy.EffectiveGraceDays);
        Assert.False(policy.GraceWasRaisedByLaw);
    }

    [Fact]
    public async Task Lease_post_due_mode_overrides_pm_default()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId,
            p => p.PostDueDefaultMode = PostDueMode.GracePeriod));
        StubLeaseSettings(lease.Id, new LeaseNoticeSettings
        {
            LeaseId = lease.Id,
            PostDueMode = PostDueMode.ImmediateLateFee
        });
        StubStateRule(lease.StateId, StateRuleWithGrace(3));

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.Equal(PostDueMode.ImmediateLateFee, policy.PostDueMode);
    }

    [Fact]
    public async Task No_state_rule_resolves_state_minimum_to_zero()
    {
        var lease = Lease();
        StubPmPrefs(lease.PropertyManagerId, DefaultPrefs(lease.PropertyManagerId, p => p.PostDueDefaultGraceDays = 5));
        StubLeaseSettings(lease.Id, null);
        StubStateRule(lease.StateId, null);   // unknown / unseeded state

        var policy = await NewSut().BuildAsync(lease, Today, CancellationToken.None);
        Assert.Equal(0, policy.StateMinimumGraceDays);
        Assert.Equal(5, policy.EffectiveGraceDays);   // PM request stands, no floor pushed it up
    }
}
