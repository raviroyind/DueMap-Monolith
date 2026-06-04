namespace DueMap.Web.Services;

/// <summary>
/// Snapshot of which OIDC SSO providers are actually configured at startup.
/// Registered as a singleton in Program.cs after the OIDC schemes are wired.
/// The Login/Register page handlers consult this before issuing Challenge —
/// challenging an unregistered scheme throws InvalidOperationException, which
/// would surface to the user as a 500. With this gate we degrade gracefully
/// to the existing TempData "not configured yet" notice instead.
/// </summary>
public sealed record SsoProviderRegistry(bool Intuit, bool Xero)
{
    public bool IsEnabled(string providerSlug) => providerSlug.ToLowerInvariant() switch
    {
        "intuit" => Intuit,
        "xero"   => Xero,
        _        => false
    };

    public string ToSchemeName(string providerSlug) => providerSlug.ToLowerInvariant() switch
    {
        "intuit" => "Intuit",
        "xero"   => "Xero",
        _        => throw new ArgumentException($"Unknown provider slug '{providerSlug}'.", nameof(providerSlug))
    };
}
