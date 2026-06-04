namespace DueMap.Tenancy.Domain;

/// <summary>
/// An invoice synced from QB / Xero. Once linked to a lease (via
/// <see cref="LeaseId"/>), the Billing planner reads <see cref="DueDate"/>
/// directly off this row instead of computing it from the lease start date.
/// </summary>
public sealed class RentInvoice
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int CustomerId { get; set; }
    public int? LeaseId { get; set; }

    public ExternalAccountingProvider ExternalProvider { get; set; }
    public string ExternalId { get; set; } = default!;
    public string? ExternalDocNumber { get; set; }

    public DateOnly? IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public RentInvoiceStatus Status { get; set; } = RentInvoiceStatus.Open;

    /// <summary>
    /// Public pay-by-link URL from the accounting provider.
    /// QBO populates this as <c>InvoiceLink</c> when QB Payments is enabled;
    /// Xero exposes it via <c>OnlineInvoiceUrl</c>. Null when the PM hasn't
    /// turned on online payments — Scriban templates should branch on it.
    /// </summary>
    public string? PublicPaymentUrl { get; set; }

    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
