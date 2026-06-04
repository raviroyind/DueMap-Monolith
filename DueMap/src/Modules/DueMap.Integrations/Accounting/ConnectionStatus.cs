namespace DueMap.Integrations.Accounting;

public enum ConnectionStatus
{
    Connected,
    TokenExpired,
    Disconnected
}

internal static class ConnectionStatusMapping
{
    public static string ToWire(this ConnectionStatus value) => value switch
    {
        ConnectionStatus.Connected     => "connected",
        ConnectionStatus.TokenExpired  => "token_expired",
        ConnectionStatus.Disconnected  => "disconnected",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static ConnectionStatus FromWire(string value) => value switch
    {
        "connected"     => ConnectionStatus.Connected,
        "token_expired" => ConnectionStatus.TokenExpired,
        "disconnected"  => ConnectionStatus.Disconnected,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown status")
    };
}

internal static class AccountingProviderMapping
{
    public static string ToWire(this AccountingProvider value) => value switch
    {
        AccountingProvider.QuickBooks => "quickbooks",
        AccountingProvider.Xero       => "xero",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static AccountingProvider FromWire(string value) => value switch
    {
        "quickbooks" => AccountingProvider.QuickBooks,
        "xero"       => AccountingProvider.Xero,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown provider")
    };
}
