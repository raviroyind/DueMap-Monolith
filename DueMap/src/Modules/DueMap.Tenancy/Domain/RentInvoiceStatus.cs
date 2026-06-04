namespace DueMap.Tenancy.Domain;

public enum RentInvoiceStatus
{
    Open,
    Paid,
    Voided
}

internal static class RentInvoiceStatusMapping
{
    public static string ToWire(this RentInvoiceStatus value) => value switch
    {
        RentInvoiceStatus.Open   => "open",
        RentInvoiceStatus.Paid   => "paid",
        RentInvoiceStatus.Voided => "voided",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static RentInvoiceStatus FromWire(string value) => value switch
    {
        "open"   => RentInvoiceStatus.Open,
        "paid"   => RentInvoiceStatus.Paid,
        "voided" => RentInvoiceStatus.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown invoice status")
    };
}
