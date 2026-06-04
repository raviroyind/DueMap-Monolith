namespace DueMap.Billing.Domain;

internal static class ActionKindMapping
{
    public static string ToWire(this ActionKind value) => value switch
    {
        ActionKind.SendPreDueReminder      => "send_pre_due_reminder",
        ActionKind.SendDueDateReminder     => "send_due_date_reminder",
        ActionKind.SendGracePeriodReminder => "send_grace_period_reminder",
        ActionKind.AssessLateFee           => "assess_late_fee",
        ActionKind.SendLateFeeNotice       => "send_late_fee_notice",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static ActionKind FromWire(string value) => value switch
    {
        "send_pre_due_reminder"      => ActionKind.SendPreDueReminder,
        "send_due_date_reminder"     => ActionKind.SendDueDateReminder,
        "send_grace_period_reminder" => ActionKind.SendGracePeriodReminder,
        "assess_late_fee"            => ActionKind.AssessLateFee,
        "send_late_fee_notice"       => ActionKind.SendLateFeeNotice,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown action_kind")
    };
}
