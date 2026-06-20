namespace DueMap.Tenancy.Discovery;

/// <summary>
/// What the engine inferred for one lease, surfaced to the onboarding
/// review screen. Any field can be NULL (=== "we couldn't decide") and
/// the UI should fall back to a manual prompt.
/// </summary>
/// <param name="LeaseId">Target lease.</param>
/// <param name="TenantDisplayName">Customer display name (for the row label).</param>
/// <param name="InferredRentAmount">Mode of the customer's recurring invoice totals, rounded to the nearest dollar. NULL when tied / no data.</param>
/// <param name="InferredDueDay">Mode of day-of-month from invoice due dates (1–28 clamp). NULL when tied / no data.</param>
/// <param name="InferredState">2-letter uppercase state from <c>Customer.BillingState</c>. NULL when ambiguous.</param>
/// <param name="InferredAt">When the inference last ran.</param>
/// <param name="ConfirmedAt">When the PM tapped "Looks right." NULL until confirmed.</param>
public sealed record DiscoveryResult(
    int LeaseId,
    string TenantDisplayName,
    decimal? InferredRentAmount,
    byte? InferredDueDay,
    string? InferredState,
    DateTime? InferredAt,
    DateTime? ConfirmedAt);
