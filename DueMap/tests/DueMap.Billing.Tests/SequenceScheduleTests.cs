using DueMap.Billing;
using DueMap.Billing.Domain;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P2-3 pure sequence logic: which steps are due on a given day, the built-in
/// default, and step→action mapping with stable, distinct step keys (the basis
/// for "each step fires exactly once").
/// </summary>
public sealed class SequenceScheduleTests
{
    private static NoticeSequence ThreeStep() => new()
    {
        PropertyManagerId = 1,
        Name = "test",
        IsActive = true,
        Steps =
        {
            new NoticeSequenceStep(-3, "email", "pre_due_reminder"),
            new NoticeSequenceStep(0,  "sms",   "due_date_reminder"),
            new NoticeSequenceStep(1,  "sms",   "grace_period_reminder"),
        }
    };

    [Theory]
    [InlineData(-3, "pre_due_reminder")]
    [InlineData(0,  "due_date_reminder")]
    [InlineData(1,  "grace_period_reminder")]
    public void Steps_due_on_returns_the_matching_step(int daysRel, string expectedCode)
    {
        var due = SequenceSchedule.StepsDueOn(ThreeStep(), daysRel);
        Assert.Single(due);
        Assert.Equal(expectedCode, due[0].NoticeTypeCode);
    }

    [Fact]
    public void No_step_on_a_non_touch_day()
    {
        Assert.Empty(SequenceSchedule.StepsDueOn(ThreeStep(), -1));
    }

    [Fact]
    public void Inactive_sequence_yields_no_steps()
    {
        var seq = ThreeStep();
        seq.IsActive = false;
        Assert.Empty(SequenceSchedule.StepsDueOn(seq, 0));
    }

    [Fact]
    public void Default_sequence_has_three_distinct_offset_steps()
    {
        var def = SequenceSchedule.Default(7);
        Assert.Equal(3, def.Steps.Count);
        Assert.Equal(3, def.Steps.Select(s => s.OffsetDays).Distinct().Count());
    }

    [Fact]
    public void Each_step_maps_to_a_distinct_stable_step_key()
    {
        var actions = ThreeStep().Steps
            .Select(PlannedAction.FromSequenceStep)
            .Where(a => a is not null)
            .Select(a => a!.StepKey)
            .ToList();

        Assert.Equal(3, actions.Count);
        Assert.Equal(3, actions.Distinct().Count());                 // distinct per step
        Assert.Contains("seq:-3:pre_due_reminder", actions);
        // Stable across calls (re-run produces the same key → idempotency hit).
        Assert.Equal("seq:0:due_date_reminder",
            PlannedAction.FromSequenceStep(new NoticeSequenceStep(0, "sms", "due_date_reminder"))!.StepKey);
    }

    [Fact]
    public void Unknown_notice_code_is_skipped()
    {
        Assert.Null(PlannedAction.FromSequenceStep(new NoticeSequenceStep(0, "email", "made_up_code")));
    }

    [Fact]
    public void Sms_step_carries_preferred_channel()
    {
        var a = PlannedAction.FromSequenceStep(new NoticeSequenceStep(0, "SMS", "due_date_reminder"));
        Assert.Equal("sms", a!.PreferredChannel);
    }
}
