namespace DueMap.Identity;

public sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    public string? ConnectionString { get; set; }

    /// <summary>Minimum password length. Default 8.</summary>
    public int MinPasswordLength { get; set; } = 8;

    public bool RequireDigit { get; set; } = true;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; }
}
