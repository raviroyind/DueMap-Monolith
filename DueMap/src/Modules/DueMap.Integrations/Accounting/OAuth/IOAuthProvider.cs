namespace DueMap.Integrations.Accounting.OAuth;

/// <summary>
/// Provider-specific OAuth2 facade. One implementation per accounting provider;
/// the connection service picks the right one based on the requested provider.
/// </summary>
public interface IOAuthProvider
{
    AccountingProvider Provider { get; }

    /// <summary>Build the provider's authorize URL for an outbound redirect.</summary>
    string BuildAuthorizationUrl(string state, string redirectUri);

    /// <summary>Exchange the OAuth code (returned to the callback) for tokens.</summary>
    Task<OAuthTokens> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string? realmIdHint,
        CancellationToken ct);

    /// <summary>Refresh an expiring access token using the stored refresh token.</summary>
    Task<OAuthTokens> RefreshAsync(string refreshToken, string realmId, CancellationToken ct);
}
