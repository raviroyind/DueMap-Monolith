namespace DueMap.Integrations.Accounting.Sync;

/// <summary>Provider-agnostic snapshot of a customer returned by an accounting client.</summary>
public sealed record SyncedCustomer(
    string ExternalId,
    string DisplayName,
    string? Email,
    string? Phone,
    bool IsActive,
    // ---- v18 (P1-1) ----
    // 2-letter US state from the billing address (QBO BillAddr.CountrySubDivisionCode
    // / Xero first address with non-empty Region). NULL when the provider didn't
    // surface a region or it isn't a 2-letter code. Normalised upstream to
    // uppercase+trim by the clients; persistence layer trusts the value as-is.
    string? BillingState = null);
