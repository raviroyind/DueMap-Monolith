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
    decimal? FeeAmount)
{
    public static PlannedAction PreDueReminder()      => new(ActionKind.SendPreDueReminder,      "pre_due_reminder",      null);
    public static PlannedAction DueDateReminder()     => new(ActionKind.SendDueDateReminder,     "due_date_reminder",     null);
    public static PlannedAction GracePeriodReminder() => new(ActionKind.SendGracePeriodReminder, "grace_period_reminder", null);
    public static PlannedAction LateFeeNotice()       => new(ActionKind.SendLateFeeNotice,       "late_fee_notice",       null);
    public static PlannedAction LateFee(decimal amount) => new(ActionKind.AssessLateFee, "late_fee_notice", amount);
}
