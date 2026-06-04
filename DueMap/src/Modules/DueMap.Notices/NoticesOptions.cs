namespace DueMap.Notices;

public sealed class NoticesOptions
{
    public const string SectionName = "Notices";

    public string? ConnectionString { get; set; }

    /// <summary>How long resolved template-version lookups stay in memory.</summary>
    public TimeSpan TemplateCacheTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long compiled Scriban templates stay in memory.</summary>
    public TimeSpan CompiledTemplateCacheTtl { get; set; } = TimeSpan.FromHours(1);
}
