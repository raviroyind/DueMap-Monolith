namespace DueMap.Rules.Domain;

public enum LateFeeType
{
    Flat,
    Percent,
    GreaterOf,
    LesserOf
}

internal static class LateFeeTypeMapping
{
    public static string ToWire(this LateFeeType value) => value switch
    {
        LateFeeType.Flat       => "flat",
        LateFeeType.Percent    => "percent",
        LateFeeType.GreaterOf  => "greater_of",
        LateFeeType.LesserOf   => "lesser_of",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static LateFeeType FromWire(string value) => value switch
    {
        "flat"       => LateFeeType.Flat,
        "percent"    => LateFeeType.Percent,
        "greater_of" => LateFeeType.GreaterOf,
        "lesser_of"  => LateFeeType.LesserOf,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown late_fee_type")
    };
}
