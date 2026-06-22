using DueMap.Billing.Domain;
using DueMap.Common.FeatureFlags;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using TenancyPostDueMode = DueMap.Tenancy.Domain.PostDueMode;

namespace DueMap.Billing.Services;

/// <summary>
/// Pure decision logic. Given a lease and an assessment date, produces the list
/// of actions the orchestrator should execute. No side effects, no I/O beyond
/// reading the policy and the resolved rule.
///
/// Cadence rules (v1):
/// <list type="bullet">
///   <item><b>Pre-due reminder</b> fires when days_relative == -policy.PreDueDaysBefore.</item>
///   <item><b>Due-date reminder</b> fires when days_relative == 0.</item>
///   <item><b>Post-due, grace mode:</b> grace_period_reminder on day 1 of being late;
///         late fee + late_fee_notice on day (EffectiveGraceDays + 1).</item>
///   <item><b>Post-due, immediate mode:</b> late fee + late_fee_notice on day
///         (StateMinimumGraceDays + 1) — earliest legally permitted.</item>
/// </list>
/// </summary>
internal sealed class AssessmentPlanner : IAssessmentPlanner
{
    private const string SequencesFlag = "billing.sequences";

    private readonly IEffectivePolicyService _policySvc;
    private readonly IRentScheduleService _schedule;
    private readonly IRentInvoiceRepository _invoices;
    private readonly IRulesService _rules;
    private readonly ISequenceResolver _sequences;
    private readonly IFeatureFlags _flags;

    public AssessmentPlanner(
        IEffectivePolicyService policySvc,
        IRentScheduleService schedule,
        IRentInvoiceRepository invoices,
        IRulesService rules,
        ISequenceResolver sequences,
        IFeatureFlags flags)
    {
        _policySvc = policySvc;
        _schedule = schedule;
        _invoices = invoices;
        _rules = rules;
        _sequences = sequences;
        _flags = flags;
    }

    public async Task<AssessmentPlan> PlanAsync(Lease lease, DateOnly assessmentDate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);

        // P1-3: AutoSetup writes a staged late-fee profile and flips
        // FeesStaged=true. The lease is held in review limbo until the PM
        // clicks "Go live" (P1-4); the planner emits NO actions for it —
        // no reminders, no fees. Returning an empty plan early keeps the
        // upstream orchestrator + scheduler unchanged.
        if (lease.FeesStaged)
        {
            // We don't even need to look up the policy or due date — the
            // lease is paused. The current due date in the plan is still
            // useful for diagnostics, so compute it cheaply.
            var stagedDueDate = await _invoices.GetCurrentDueDateAsync(lease.Id, assessmentDate, ct)
                              ?? _schedule.GetCurrentDueDate(lease, assessmentDate);
            return new AssessmentPlan(
                LeaseId:           lease.Id,
                CurrentDueDate:    stagedDueDate,
                AssessmentDate:    assessmentDate,
                DaysRelativeToDue: assessmentDate.DayNumber - stagedDueDate.DayNumber,
                Actions:           Array.Empty<PlannedAction>());
        }

        var policy = await _policySvc.BuildAsync(lease, assessmentDate, ct);

        // Prefer the synced invoice's due date; fall back to the computed
        // schedule if no invoice is linked (no accounting sync yet, or the
        // customer has no open invoice).
        var dueDate = await _invoices.GetCurrentDueDateAsync(lease.Id, assessmentDate, ct)
                   ?? _schedule.GetCurrentDueDate(lease, assessmentDate);
        var daysRel = assessmentDate.DayNumber - dueDate.DayNumber;

        var actions = new List<PlannedAction>(capacity: 2);

        // P2-3: when billing.sequences is on, the PM's reminder sequence drives
        // the pre-due / due-date / grace touches (each carrying a step_key for
        // idempotency); the post-due late-fee logic is unchanged. When off,
        // the legacy single-touch-per-kind cadence runs exactly as before.
        var sequencesOn = await _flags.IsEnabledAsync(SequencesFlag, lease.PropertyManagerId, ct);

        if (sequencesOn)
        {
            var sequence = await _sequences.ResolveAsync(lease.PropertyManagerId, ct);
            foreach (var step in SequenceSchedule.StepsDueOn(sequence, daysRel))
            {
                var planned = PlannedAction.FromSequenceStep(step);
                if (planned is not null) actions.Add(planned);
            }

            if (policy.PostDueEnabled && daysRel > 0)
            {
                // Sequence already supplies the grace reminder — only the late
                // fee comes from the post-due path here.
                await AppendPostDueActionsAsync(policy, daysRel, assessmentDate, actions, ct, emitGraceReminder: false);
            }
        }
        else
        {
            if (policy.PreDueEnabled && daysRel == -policy.PreDueDaysBefore)
            {
                actions.Add(PlannedAction.PreDueReminder());
            }

            if (policy.DueDateEnabled && daysRel == 0)
            {
                actions.Add(PlannedAction.DueDateReminder());
            }

            if (policy.PostDueEnabled && daysRel > 0)
            {
                await AppendPostDueActionsAsync(policy, daysRel, assessmentDate, actions, ct, emitGraceReminder: true);
            }
        }

        return new AssessmentPlan(
            LeaseId: lease.Id,
            CurrentDueDate: dueDate,
            AssessmentDate: assessmentDate,
            DaysRelativeToDue: daysRel,
            Actions: actions);
    }

    private async Task AppendPostDueActionsAsync(
        EffectiveLeasePolicy policy,
        int daysRel,
        DateOnly assessmentDate,
        List<PlannedAction> actions,
        CancellationToken ct,
        bool emitGraceReminder = true)
    {
        switch (policy.PostDueMode)
        {
            case TenancyPostDueMode.GracePeriod:
                if (emitGraceReminder && daysRel == 1)
                {
                    actions.Add(PlannedAction.GracePeriodReminder());
                }
                if (daysRel == policy.EffectiveGraceDays + 1)
                {
                    var fee = await ResolveFeeAmountAsync(policy, assessmentDate, ct);
                    actions.Add(PlannedAction.LateFee(fee));
                    actions.Add(PlannedAction.LateFeeNotice());
                }
                break;

            case TenancyPostDueMode.ImmediateLateFee:
                if (daysRel == policy.StateMinimumGraceDays + 1)
                {
                    var fee = await ResolveFeeAmountAsync(policy, assessmentDate, ct);
                    actions.Add(PlannedAction.LateFee(fee));
                    actions.Add(PlannedAction.LateFeeNotice());
                }
                break;
        }
    }

    private async Task<decimal> ResolveFeeAmountAsync(
        EffectiveLeasePolicy policy,
        DateOnly assessmentDate,
        CancellationToken ct)
    {
        var rule = await _rules.ResolveRuleByStateIdAsync(
            policy.StateId,
            assessmentDate,
            policy.JurisdictionId,
            ct);
        return rule?.ComputeFee(policy.MonthlyRent) ?? 0m;
    }
}
