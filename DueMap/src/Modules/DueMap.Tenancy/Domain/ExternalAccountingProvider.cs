namespace DueMap.Tenancy.Domain;

/// <summary>
/// Echo of <c>DueMap.Integrations.Accounting.AccountingProvider</c>, mirrored
/// inside Tenancy so this module has no compile-time dependency on Integrations.
/// The sync orchestrator converts between the two enums at the boundary.
/// </summary>
public enum ExternalAccountingProvider
{
    QuickBooks,
    Xero
}

internal static class ExternalAccountingProviderMapping
{
    public static string ToWire(this ExternalAccountingProvider value) => value switch
    {
        ExternalAccountingProvider.QuickBooks => "quickbooks",
        ExternalAccountingProvider.Xero       => "xero",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static ExternalAccountingProvider FromWire(string value) => value switch
    {
        "quickbooks" => ExternalAccountingProvider.QuickBooks,
        "xero"       => ExternalAccountingProvider.Xero,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown provider")
    };
}
