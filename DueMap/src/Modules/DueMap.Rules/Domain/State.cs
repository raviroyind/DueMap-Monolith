namespace DueMap.Rules.Domain;

public sealed class State
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
