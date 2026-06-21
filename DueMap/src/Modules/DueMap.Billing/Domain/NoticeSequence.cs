namespace DueMap.Billing.Domain;

/// <summary>
/// One touch in a reminder sequence (P2-3). <see cref="OffsetDays"/> is
/// relative to the rent due date (negative = before, 0 = due day, positive =
/// after). <see cref="Channel"/> is the preferred channel ("email"/"sms") —
/// advisory: the executor falls back if that channel isn't available.
/// </summary>
public sealed record NoticeSequenceStep(int OffsetDays, string Channel, string NoticeTypeCode);

/// <summary>
/// A PM's configurable reminder cadence. Persisted in
/// <c>billing.notice_sequences</c> (steps as JSON). The
/// <see cref="ISequenceResolver"/> returns the PM's active sequence or a
/// built-in default.
/// </summary>
public sealed class NoticeSequence
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public List<NoticeSequenceStep> Steps { get; set; } = new();
}
