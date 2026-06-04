namespace DueMap.Tenancy.Domain;

/// <summary>
/// PM-level accounting metadata, populated by the QB/Xero sync and editable in
/// DueMap. Other modules read this for:
/// <list type="bullet">
///   <item>Billing scheduler — <see cref="TimezoneId"/> ("midnight in PM local")</item>
///   <item>Integrations late-fee posting — <see cref="DefaultLateFeeItemExternalId"/></item>
///   <item>UI display — <see cref="DefaultCurrency"/>, <see cref="DefaultPaymentTermsDays"/></item>
/// </list>
/// </summary>
public sealed class PmAccountingDefaults
{
    public int PropertyManagerId { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public int DefaultPaymentTermsDays { get; set; }
    public string? DefaultLateFeeItemExternalId { get; set; }
    public string TimezoneId { get; set; } = "America/New_York";
    public DateTime? LastSyncedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
