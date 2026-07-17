using DueMap.Billing.Domain;
using DueMap.Billing.Services;
using DueMap.Rules;
using DueMap.Rules.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

public sealed class AssessmentPlannerTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly IEffectivePolicyService _policy = Substitute.For<IEffectivePolicyService>();
    private readonly IRentScheduleService _schedule = Substitute.For<IRentScheduleService>();
    private readonly IRentInvoiceRepository _invoices = Substitute.For<IRentInvoiceRepository>();
    private readonly IRulesService _rules = Substitute.For<IRulesService>();
    private readonly ISequenceResolver _sequences = Substitute.For<ISequenceResolver>();
    private readonly IPaymentPromiseReader _promises = Substitute.For<IPaymentPromiseReader>();
    private readonly DueMap.Common.FeatureFlags.IFeatureFlags _flags = Substitute.For<DueMap.Common.FeatureFlags.IFeatureFlags>();

    // billing.sequences defaults OFF (mock returns false) → legacy cadence.
    // The promise mock defaults to null → no suppression.
    private AssessmentPlanner NewSut() => new(_policy, _schedule, _invoices, _rules, _sequences, _promises, _flags);

    private void StubActivePromise(DateOnly promisedDate) =>
        _promises.GetActiveForLeaseAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                 .Returns(new PaymentPromise
                 {
                     Id = 7, LeaseId = 1, PropertyManagerId = 10,
                     Amount = 1200m, PromisedDate = promisedDate,
                     Status = PromiseStatus.Active, CreatedAt = DateTime.UtcNow
                 });

    private static Lease Lease() =>
        new() { Id = 1, PropertyManagerId = 10, StateId = 5, MonthlyRent = 2000m,
                StartDate = new DateOnly(2024, 1, 1) };

    private static EffectiveLeasePolicy Policy(
        bool preDue = true, int preDueDays = 3,
        bool dueDate = true,
        bool postDue = true, PostDueMode mode = PostDueMode.GracePeriod,
        int effectiveGrace = 5, int requestedGrace = 5, int stateMin = 5) =>
        new(
            LeaseId: 1, PropertyManagerId: 10, StateId: 5, JurisdictionId: null,
            PreDueEnabled: preDue, PreDueDaysBefore: preDueDays,
            DueDateEnabled: dueDate,
            PostDueEnabled: postDue, PostDueMode: mode,
            EffectiveGraceDays: effectiveGrace,
            RequestedGraceDays: requestedGrace,
            StateMinimumGraceDays: stateMin,
            MonthlyRent: 2000m);

    private void StubPolicy(EffectiveLeasePolicy policy) =>
        _policy.BuildAsync(Arg.Any<Lease>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(policy);

    /// <summary>Configure the planner to think today's due date is <paramref name="dueDate"/>.</summary>
    private void StubDueDate(DateOnly dueDate)
    {
        // Invoice repo wins. Make it return the requested due date so the
        // schedule fallback doesn't matter for cadence tests.
        _invoices.GetCurrentDueDateAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                 .Returns(dueDate);
    }

    private void StubRuleForFeeMath() =>
        _rules.ResolveRuleByStateIdAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
              .Returns(new ResolvedRule(
                  StateCode: "CA", StateRuleVersionId: 1, LocalRuleOverrideId: null,
                  AssessmentDate: Today, GracePeriodDays: 5,
                  LateFeeType: LateFeeType.Percent, FlatAmount: null, PercentOfRent: 0.05m, HardCapAmount: null,
                  NoticeRequiredBeforeFee: false, NoticeAdvanceDays: null, SourceCitation: "t"));

    [Fact]
    public async Task PreDue_reminder_fires_when_daysRel_equals_negative_PreDueDaysBefore()
    {
        StubPolicy(Policy(preDueDays: 3));
        StubDueDate(Today.AddDays(3));   // daysRel = -3

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Single(plan.Actions);
        Assert.Equal(ActionKind.SendPreDueReminder, plan.Actions[0].Kind);
    }

    [Fact]
    public async Task PreDue_reminder_does_not_fire_on_other_days()
    {
        StubPolicy(Policy(preDueDays: 3));
        StubDueDate(Today.AddDays(2));   // daysRel = -2 (off by one)

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task PreDue_disabled_suppresses_even_at_the_right_day()
    {
        StubPolicy(Policy(preDue: false, preDueDays: 3));
        StubDueDate(Today.AddDays(3));

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task DueDate_reminder_fires_when_daysRel_is_zero()
    {
        StubPolicy(Policy());
        StubDueDate(Today);

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.SendDueDateReminder);
    }

    [Fact]
    public async Task DueDate_disabled_suppresses_at_zero_days()
    {
        StubPolicy(Policy(dueDate: false));
        StubDueDate(Today);

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task GracePeriod_mode_fires_grace_reminder_on_day_one()
    {
        StubPolicy(Policy(mode: PostDueMode.GracePeriod, effectiveGrace: 5));
        StubDueDate(Today.AddDays(-1));   // daysRel = 1

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Single(plan.Actions);
        Assert.Equal(ActionKind.SendGracePeriodReminder, plan.Actions[0].Kind);
    }

    [Fact]
    public async Task GracePeriod_mode_fires_late_fee_at_grace_plus_one()
    {
        StubPolicy(Policy(mode: PostDueMode.GracePeriod, effectiveGrace: 5));
        StubDueDate(Today.AddDays(-6));   // daysRel = 6 = EffectiveGrace + 1
        StubRuleForFeeMath();

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Equal(2, plan.Actions.Count);
        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.AssessLateFee);
        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.SendLateFeeNotice);

        var fee = plan.Actions.First(a => a.Kind == ActionKind.AssessLateFee);
        Assert.Equal(100m, fee.FeeAmount);   // 5% of $2000
    }

    [Fact]
    public async Task GracePeriod_mode_fires_nothing_between_day_2_and_grace()
    {
        StubPolicy(Policy(mode: PostDueMode.GracePeriod, effectiveGrace: 5));
        StubDueDate(Today.AddDays(-3));   // daysRel = 3, inside grace, after reminder day

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task ImmediateLateFee_mode_fires_at_state_min_plus_one()
    {
        StubPolicy(Policy(mode: PostDueMode.ImmediateLateFee, stateMin: 3, effectiveGrace: 3));
        StubDueDate(Today.AddDays(-4));   // daysRel = 4 = stateMin + 1
        StubRuleForFeeMath();

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Equal(2, plan.Actions.Count);
        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.AssessLateFee);
        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.SendLateFeeNotice);
    }

    [Fact]
    public async Task ImmediateLateFee_mode_does_not_fire_grace_reminder()
    {
        StubPolicy(Policy(mode: PostDueMode.ImmediateLateFee, stateMin: 3, effectiveGrace: 3));
        StubDueDate(Today.AddDays(-1));   // daysRel = 1

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task PostDue_disabled_suppresses_both_modes()
    {
        StubPolicy(Policy(postDue: false, mode: PostDueMode.GracePeriod, effectiveGrace: 5));
        StubDueDate(Today.AddDays(-6));

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task Invoice_due_date_wins_over_schedule_when_present()
    {
        StubPolicy(Policy());
        _invoices.GetCurrentDueDateAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                 .Returns(Today);   // invoice says today
        _schedule.GetCurrentDueDate(Arg.Any<Lease>(), Arg.Any<DateOnly>())
                 .Returns(Today.AddDays(10));   // schedule disagrees — should be ignored

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Equal(Today, plan.CurrentDueDate);
        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.SendDueDateReminder);
    }

    [Fact]
    public async Task Falls_back_to_schedule_when_invoice_repo_returns_null()
    {
        StubPolicy(Policy());
        _invoices.GetCurrentDueDateAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                 .Returns((DateOnly?)null);
        _schedule.GetCurrentDueDate(Arg.Any<Lease>(), Arg.Any<DateOnly>())
                 .Returns(Today);

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Equal(Today, plan.CurrentDueDate);
        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.SendDueDateReminder);
    }

    [Fact]
    public async Task DaysRelativeToDue_is_signed_and_correct()
    {
        StubPolicy(Policy());
        StubDueDate(Today.AddDays(-4));

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Equal(4, plan.DaysRelativeToDue);
    }

    // ---- Promise-to-pay (task #112) --------------------------------------

    [Fact]
    public async Task Active_promise_covering_the_date_pauses_the_whole_sequence()
    {
        // Tenant is 1 day late — normally the grace reminder would fire.
        StubPolicy(Policy());
        StubDueDate(Today.AddDays(-1));
        StubActivePromise(promisedDate: Today.AddDays(3));   // "I'll pay Friday"

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task Promise_pauses_through_the_promised_day_itself()
    {
        StubPolicy(Policy());
        StubDueDate(Today.AddDays(-1));
        StubActivePromise(promisedDate: Today);   // due TODAY — tenant has all day

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task Stale_active_promise_past_its_date_does_not_suppress()
    {
        // Belt-and-braces: if resolution didn't run for some reason, an Active
        // promise whose date already passed must NOT keep the machine paused.
        StubPolicy(Policy());
        StubDueDate(Today.AddDays(-1));           // grace reminder day
        StubActivePromise(promisedDate: Today.AddDays(-1));

        var plan = await NewSut().PlanAsync(Lease(), Today, CancellationToken.None);

        Assert.Contains(plan.Actions, a => a.Kind == ActionKind.SendGracePeriodReminder);
    }
}
