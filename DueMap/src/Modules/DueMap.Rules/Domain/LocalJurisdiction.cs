namespace DueMap.Rules.Domain;

public sealed class LocalJurisdiction
{
    public int Id { get; set; }
    public int StateId { get; set; }
    public string Name { get; set; } = default!;
    public string JurisdictionType { get; set; } = default!;   // city | county | borough | parish
    public string? FipsCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
