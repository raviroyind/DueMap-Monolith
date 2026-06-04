namespace DueMap.Web.Services;

/// <summary>
/// Single source of truth for the rent-roll sample download (xlsx + csv).
/// Program.cs's MapGet handler reads from here; the static CSV fixture in
/// wwwroot/samples mirrors the same data by hand.
/// </summary>
internal static class RentRollSample
{
    public static readonly string[] Headers =
    {
        "Tenant Name", "Unit", "Email", "Monthly Rent",
        "Start Date",  "End Date", "State"
    };
}
