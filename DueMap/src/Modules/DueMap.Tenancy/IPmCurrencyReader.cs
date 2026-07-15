namespace DueMap.Tenancy;

/// <summary>
/// Resolves the ISO-4217 currency code a PM's amounts should display in.
/// Never falls back to the SERVER's locale — a US book must render $ even when
/// the host machine runs en-IN (QA P2: every screen showed ₹). Resolution
/// order: explicit accounting default → majority currency across the PM's
/// synced invoices → USD.
/// </summary>
public interface IPmCurrencyReader
{
    Task<string> GetCurrencyCodeAsync(int propertyManagerId, CancellationToken ct);
}
