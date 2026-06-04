namespace DueMap.Tenancy.Domain;

public enum PostDueMode
{
    /// <summary>Reminders during grace; late fee + late-fee notice at grace end.</summary>
    GracePeriod,

    /// <summary>No grace reminders; late fee + late-fee notice as soon as state law permits.</summary>
    ImmediateLateFee
}

internal static class PostDueModeMapping
{
    public static string ToWire(this PostDueMode value) => value switch
    {
        PostDueMode.GracePeriod      => "grace_period",
        PostDueMode.ImmediateLateFee => "immediate_late_fee",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static PostDueMode FromWire(string value) => value switch
    {
        "grace_period"       => PostDueMode.GracePeriod,
        "immediate_late_fee" => PostDueMode.ImmediateLateFee,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown post_due_mode")
    };
}
