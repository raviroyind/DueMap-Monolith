using DueMap.Billing;
using DueMap.Billing.Domain;
using DueMap.Billing.Services;
using DueMap.Common.FeatureFlags;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P2-3: with billing.sequences ON, the planner emits reminders from the PM's
/// sequence — each carrying its step_key so the idempotency ledger fires each
/// touch exactly once. Late-fee logic is unchanged.
/// </summary>
public sealed class AssessmentPlannerSequenceTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly IEffectivePolicyService _policy   = Substitute.For<IEffectivePolicyService>();
    private readonly IRentScheduleService    _schedule = Substitute.For<IRentScheduleService>();
    private readonly IRentInvoiceRepository  _invoices = Substitute.For<IRentInvoiceRepository>();
    private readonly IRulesService           _rules    = Substitute.For<IRulesService>();
    private readonly ISequenceResolver       _sequences = Substitute.For<ISequenceResolver>();
    private readonly IPaymentPromiseReader   _promises = Substitute.For<IPaymentPromiseReader>();
    private readonly IFeatureFlags           _flags    = Substitute.For<IFeatureFlags>();

    private const int PmId = 10;

    private AssessmentPlanner NewSut() => new(_policy, _schedule, _invoices, _rules, _sequences, _promises, _flags);

    private static Lease Lease() =>
        new() { Id = 1, PropertyManagerId = PmId, StateId = 5, MonthlyRent = 2000m, StartDate = new DateOnly(2024, 1, 1) };

    private static EffectiveLeasePolicy Policy() => new(
        LeaseId: 1, PropertyManagerId: PmId, StateId: 5, JurisdictionId: null,
        PreDueEnabled: true, PreDueDaysBefore: 3, DueDateEnabled: true,
        PostDueEnabled: true, PostDueMode: PostDueMode.GracePeriod,
        EffectiveGraceDays: 5, RequestedGraceDays: 5, StateMinimumGraceDays: 5,
        MonthlyRent: 2000m);

    private void Arrange(DateOnly dueDate)
    {
        _flags.IsEnabledAsync("billing.sequences", PmId, Arg.Any<CancellationToken>()).Returns(true);
        _sequences.ResolveAsync(PmId, Arg.Any<CancellationToken>()).Returns(SequenceSchedule.Default(PmId));
        _policy.BuildAsync(Arg.Any<Lease>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Policy());
        _invoices.GetCurrentDueDateAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(dueDate);
    }

    [Fact]
    public async Task Pre_due_day_emits_sequence_step_with_step_key()
    {
        Arrange(Today.AddDays(3));   // daysRel = -3
        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(ActionKind.SendPreDueReminder, action.Kind);
        Assert.Equal("seq:-3:pre_due_reminder", action.StepKey);
    }

    [Fact]
    public async Task Due_day_emits_due_date_step()
    {
        Arrange(Today);   // daysRel = 0
        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(ActionKind.SendDueDateReminder, action.Kind);
        Assert.Equal("seq:0:due_date_reminder", action.StepKey);
    }

    [Fact]
    public async Task Grace_day_emits_grace_step_but_no_duplicate_from_post_due()
    {
        Arrange(Today.AddDays(-1));   // daysRel = +1
        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        // Exactly the sequence's grace step — the post-due path must NOT also
        // emit its own grace reminder when sequences drive reminders.
        var grace = Assert.Single(plan.Actions, a => a.Kind == ActionKind.SendGracePeriodReminder);
        Assert.Equal("seq:1:grace_period_reminder", grace.StepKey);
    }

    [Fact]
    public async Task Late_fee_still_fires_from_post_due_when_sequences_on()
    {
        // grace = 5 → fee at daysRel = 6.
        Arrange(Today.AddDays(-6));
        _rules.ResolveRuleByStateIdAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
              .Returns(new DueMap.Rules.Domain.ResolvedRule(
                  StateCode: "CA", StateRuleVersionId: 1, LocalRuleOverrideId: null,
                  AssessmentDate: Today, GracePeriodDays: 5,
                  LateFeeType: DueMap.Rules.Domain.LateFeeType.Flat, FlatAmount: 50m, PercentOfRent: null, HardCapAmount: null,
                  NoticeRequiredBeforeFee: false, NoticeAdvanceDays: null, SourceCitation: "t"));

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.AssessLateFee);
    }
}
