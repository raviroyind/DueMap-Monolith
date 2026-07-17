namespace DueMap.Tenancy.Domain;

/// <summary>
/// A Today-queue row the PM has dismissed ("seen it, nothing to do"). Keyed by
/// a deterministic item key (e.g. "delivery:345", "promise:12",
/// "expiry:lease:7") so the same underlying fact never resurfaces once
/// dismissed, while a NEW fact about the same lease (next cycle's fee risk,
/// a fresh failed delivery) gets a new key and does.
/// </summary>
public sealed class WorkItemDismissal
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public string ItemKey { get; set; } = default!;
    public DateTime DismissedAt { get; set; }
}
