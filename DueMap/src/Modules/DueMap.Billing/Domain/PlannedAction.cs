namespace DueMap.Billing.Domain;

public enum ActionKind
{
    SendPreDueReminder,
    SendDueDateReminder,
    SendGracePeriodReminder,
    AssessLateFee,
    SendLateFeeNotice
}

/// <summary>
/// One thing the orchestrator should do for one lease today. Notice-type code
/// is included so the caller can look up the right template without a switch
/// statement; <see cref="FeeAmount"/> is only set for <see cref="ActionKind.AssessLateFee"/>.
/// </summary>
public sealed record PlannedAction(
    ActionKind Kind,
    string NoticeTypeCode,
    decimal? FeeAmount,
    // P2-3: distinguishes touches that share a (lease, due_date, kind) so the
    // idempotency ledger fires each sequence step exactly once. Null for the
    // legacy cadence (one touch per kind per period).
    string? StepKey = null,
    // P2-3: preferred channel ("email"/"sms"); advisory — the executor falls
    // back to its default channel choice when this isn't deliverable.
    string? PreferredChannel = null)
{
    public static PlannedAction PreDueReminder()      => new(ActionKind.SendPreDueReminder,      "pre_due_reminder",      null);
    public static PlannedAction DueDateReminder()     => new(ActionKind.SendDueDateReminder,     "due_date_reminder",     null);
    public static PlannedAction GracePeriodReminder() => new(ActionKind.SendGracePeriodReminder, "grace_period_reminder", null);
    public static PlannedAction LateFeeNotice()       => new(ActionKind.SendLateFeeNotice,       "late_fee_notice",       null);
    public static PlannedAction LateFee(decimal amount) => new(ActionKind.AssessLateFee, "late_fee_notice", amount);

    /// <summary>
    /// Build a reminder action from a sequence step (P2-3). Unknown notice
    /// codes return null so an unrecognized step is skipped, not thrown.
    /// </summary>
    public static PlannedAction? FromSequenceStep(NoticeSequenceStep step)
    {
        var kind = step.NoticeTypeCode switch
        {
            "pre_due_reminder"      => ActionKind.SendPreDueReminder,
            "due_date_reminder"     => ActionKind.SendDueDateReminder,
            "grace_period_reminder" => ActionKind.SendGracePeriodReminder,
            _                       => (ActionKind?)null
        };
        if (kind is null) return null;
        return new PlannedAction(
            kind.Value, step.NoticeTypeCode, FeeAmount: null,
            StepKey: $"seq:{step.OffsetDays}:{step.NoticeTypeCode}",
            PreferredChannel: string.IsNullOrWhiteSpace(step.Channel) ? null : step.Channel.ToLowerInvariant());
    }
}
