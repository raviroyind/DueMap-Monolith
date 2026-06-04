namespace DueMap.Rules;

public sealed class RulesOptions
{
    public const string SectionName = "Rules";

    public string? ConnectionString { get; set; }

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(10);
}
