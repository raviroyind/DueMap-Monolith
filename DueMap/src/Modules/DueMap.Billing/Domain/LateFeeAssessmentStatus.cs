namespace DueMap.Billing.Domain;

public enum LateFeeAssessmentStatus
{
    Assessed,
    Reversed,
    Waived
}

internal static class LateFeeAssessmentStatusMapping
{
    public static string ToWire(this LateFeeAssessmentStatus value) => value switch
    {
        LateFeeAssessmentStatus.Assessed => "assessed",
        LateFeeAssessmentStatus.Reversed => "reversed",
        LateFeeAssessmentStatus.Waived   => "waived",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static LateFeeAssessmentStatus FromWire(string value) => value switch
    {
        "assessed" => LateFeeAssessmentStatus.Assessed,
        "reversed" => LateFeeAssessmentStatus.Reversed,
        "waived"   => LateFeeAssessmentStatus.Waived,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown status")
    };
}
