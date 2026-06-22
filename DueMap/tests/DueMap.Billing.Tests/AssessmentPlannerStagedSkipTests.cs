using DueMap.Billing.Services;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P1-3 contract: a lease with <c>FeesStaged = true</c> emits NO actions
/// from the planner regardless of cadence or policy. The single guard
/// at the top of <c>AssessmentPlanner.PlanAsync</c> upholds the "nothing
/// assessable while fees_staged = 1" promise from the prompt.
/// </summary>
public sealed class AssessmentPlannerStagedSkipTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly IEffectivePolicyService _policy   = Substitute.For<IEffectivePolicyService>();
    private readonly IRentScheduleService    _schedule = Substitute.For<IRentScheduleService>();
    private readonly IRentInvoiceRepository  _invoices = Substitute.For<IRentInvoiceRepository>();
    private readonly IRulesService           _rules    = Substitute.For<IRulesService>();
    private readonly ISequenceResolver       _sequences = Substitute.For<ISequenceResolver>();
    private readonly DueMap.Common.FeatureFlags.IFeatureFlags _flags = Substitute.For<DueMap.Common.FeatureFlags.IFeatureFlags>();

    private AssessmentPlanner NewSut() => new(_policy, _schedule, _invoices, _rules, _sequences, _flags);

    private static Lease StagedLease(bool feesStaged) => new()
    {
        Id = 1,
        PropertyManagerId = 10,
        StateId = 5,
        MonthlyRent = 2000m,
        StartDate = new DateOnly(2024, 1, 1),
        FeesStaged = feesStaged
    };

    [Fact]
    public async Task Staged_lease_emits_no_actions_even_on_a_due_date()
    {
        _invoices.GetCurrentDueDateAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                 .Returns(Today);   // daysRel = 0 — would normally fire due-date reminder

        var plan = await NewSut().PlanAsync(StagedLease(feesStaged: true), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
        // Critically, the policy service was NEVER consulted — the staged
        // guard short-circuits before any side-effect-capable read.
        await _policy.DidNotReceive().BuildAsync(
            Arg.Any<Lease>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unstaged_lease_still_falls_through_to_the_normal_path()
    {
        _invoices.GetCurrentDueDateAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                 .Returns(Today.AddDays(10));   // daysRel = -10 — outside any pre-due window

        _policy.BuildAsync(Arg.Any<Lease>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
               .Returns(new DueMap.Billing.Domain.EffectiveLeasePolicy(
                   LeaseId: 1, PropertyManagerId: 10, StateId: 5, JurisdictionId: null,
                   PreDueEnabled: true, PreDueDaysBefore: 3,
                   DueDateEnabled: true,
                   PostDueEnabled: true, PostDueMode: PostDueMode.GracePeriod,
                   EffectiveGraceDays: 5, RequestedGraceDays: 5, StateMinimumGraceDays: 5,
                   MonthlyRent: 2000m));

        var plan = await NewSut().PlanAsync(StagedLease(feesStaged: false), Today, CancellationToken.None);

        Assert.Empty(plan.Actions);
        // Proves the unstaged path WAS consulted — distinguishes the
        // "empty because staged" branch from the "empty because outside
        // cadence" branch.
        await _policy.Received(1).BuildAsync(
            Arg.Any<Lease>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
