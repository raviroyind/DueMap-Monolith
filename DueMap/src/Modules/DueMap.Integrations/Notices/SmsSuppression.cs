namespace DueMap.Integrations.Notices;

/// <summary>One opted-out phone number (P2-2). Stored normalized (digits).</summary>
public sealed class SmsSuppression
{
    public int Id { get; set; }
    public string Phone { get; set; } = default!;   // normalized digits (last-10)
    public int? PropertyManagerId { get; set; }
    public string Reason { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
