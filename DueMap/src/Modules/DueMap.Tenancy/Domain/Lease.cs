namespace DueMap.Tenancy.Domain;

public sealed class Lease
{
    public int Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int StateId { get; set; }
    public int? JurisdictionId { get; set; }
    public int? CustomerId { get; set; }
    public decimal MonthlyRent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// PM-supplied unit identifier — e.g. "Apt 3B", "Unit 12". Free-form for v1.
    /// Imported from the PM's rent-roll spreadsheet; normalised properties +
    /// property_units table arrives in a future schema migration.
    /// </summary>
    public string? UnitLabel { get; set; }

    public DateTime CreatedAt { get; set; }
}
