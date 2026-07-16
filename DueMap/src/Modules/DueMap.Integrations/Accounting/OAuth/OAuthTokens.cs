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
    string RealmId,
    // Raw OIDC id_token from the code exchange, when openid scope was
    // granted. Sign-in flows read the human's email from it; connect-only
    // flows ignore it. Never persisted.
    string? IdToken = null);
