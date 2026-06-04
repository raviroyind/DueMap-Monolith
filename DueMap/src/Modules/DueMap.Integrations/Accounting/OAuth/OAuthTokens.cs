namespace DueMap.Integrations.Accounting.OAuth;

/// <summary>
/// Provider-agnostic token bundle returned by code exchange and refresh calls.
/// Plaintext — the connection service encrypts before persistence.
/// </summary>
public sealed record OAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime? RefreshTokenExpiresAt,
    string? Scopes,
    string RealmId);
