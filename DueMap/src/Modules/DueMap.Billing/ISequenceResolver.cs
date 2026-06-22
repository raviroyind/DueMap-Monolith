using DueMap.Billing.Domain;

namespace DueMap.Billing;

/// <summary>
/// Resolves the reminder sequence the planner should use for a PM (P2-3),
/// and decides which steps are due on a given day. Returns a built-in default
/// when the PM hasn't configured one. Consulted by the AssessmentPlanner only
/// when the <c>billing.sequences</c> flag is on.
/// </summary>
public interface ISequenceResolver
{
    Task<NoticeSequence> ResolveAsync(int propertyManagerId, CancellationToken ct);
}

/// <summary>Pure sequence helpers — no I/O, fully unit-testable.</summary>
public static class SequenceSchedule
{
    /// <summary>
    /// The steps due on a day whose offset from the due date is
    /// <paramref name="daysRelativeToDue"/> (e.g. -3 = three days before due).
    /// Empty when the sequence is inactive or no step matches.
    /// </summary>
    public static IReadOnlyList<NoticeSequenceStep> StepsDueOn(NoticeSequence sequence, int daysRelativeToDue)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (!sequence.IsActive) return System.Array.Empty<NoticeSequenceStep>();
        return sequence.Steps.Where(s => s.OffsetDays == daysRelativeToDue).ToList();
    }

    /// <summary>
    /// The built-in cadence used when a PM hasn't configured one: a friendly
    /// pre-due nudge, a due-day reminder, and a +1 grace reminder. Channels
    /// default to email (safe); a PM can switch any step to SMS.
    /// </summary>
    public static NoticeSequence Default(int propertyManagerId) => new()
    {
        Id = 0,
        PropertyManagerId = propertyManagerId,
        Name = "Default reminders",
        IsActive = true,
        Steps =
        {
            new NoticeSequenceStep(OffsetDays: -3, Channel: "email", NoticeTypeCode: "pre_due_reminder"),
            new NoticeSequenceStep(OffsetDays:  0, Channel: "email", NoticeTypeCode: "due_date_reminder"),
            new NoticeSequenceStep(OffsetDays:  1, Channel: "email", NoticeTypeCode: "grace_period_reminder")
        }
    };
}
