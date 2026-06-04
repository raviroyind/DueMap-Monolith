namespace DueMap.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public string? ConnectionString { get; set; }
}
