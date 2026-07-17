namespace DueMap.Tenancy.Domain;

public enum PromiseStatus : byte
{
    /// <summary>Open agreement — pauses the lease's notice sequence through the promised date.</summary>
    Active = 1,
    /// <summary>Promise date passed and the balance owed at that date was cleared.</summary>
    Kept = 2,
    /// <summary>Promise date passed with money still owing — escalation resumed.</summary>
    Broken = 3,
    /// <summary>PM withdrew the promise before it came due; notices resume immediately.</summary>
    Cancelled = 4
}

/// <summary>
/// A tenant's promise to pay ("$1,200 by Jul 10, agreed by phone"), logged by
/// the PM against a lease. While Active and not yet due it pauses that lease's
/// whole notice sequence; the daily run resolves it to Kept or Broken once the
/// date passes. Rows are never deleted — the Kept/Broken history on a lease is
/// the PM's documentation ("promised twice, broke both") for an eviction filing.
/// </summary>
public sealed class PaymentPromise
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int LeaseId { get; set; }

    /// <summary>Promised amount; NULL means "the full outstanding balance".</summary>
    public decimal? Amount { get; set; }

    /// <summary>Pay-by date. The notice pause covers assessment dates up to and including this day.</summary>
    public DateOnly PromisedDate { get; set; }

    /// <summary>Free-form context: "agreed by phone", who spoke to whom.</summary>
    public string? Note { get; set; }

    public PromiseStatus Status { get; set; } = PromiseStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
