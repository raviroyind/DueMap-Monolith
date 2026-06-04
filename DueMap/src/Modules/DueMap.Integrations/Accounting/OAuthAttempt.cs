namespace DueMap.Integrations.Accounting;

public sealed class OAuthAttempt
{
    public string State { get; set; } = default!;
    public int PropertyManagerId { get; set; }
    public AccountingProvider Provider { get; set; }
    public string RedirectUri { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
