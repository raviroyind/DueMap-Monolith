using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DueMap.Integrations.Accounting.OAuth;

/// <summary>
/// Xero OAuth2. Unlike QuickBooks, the realm id (Xero "tenantId") is not part
/// of the callback — after token exchange the caller hits
/// <c>https://api.xero.com/connections</c> to enumerate which tenants the
/// access token grants. We capture the first connection's <c>tenantId</c> as
/// the realm; multi-tenant Xero auth is out of scope for v1.
/// </summary>
internal sealed class XeroOAuthProvider : IOAuthProvider
{
    private const string AuthorizeUrl   = "https://login.xero.com/identity/connect/authorize";
    private const string TokenUrl       = "https://identity.xero.com/connect/token";
    private const string ConnectionsUrl = "https://api.xero.com/connections";

    private readonly HttpClient _http;
    private readonly IntegrationsOptions.XeroOptions _opts;

    public AccountingProvider Provider => AccountingProvider.Xero;

    public XeroOAuthProvider(HttpClient http, IOptions<IntegrationsOptions> options)
    {
        _http = http;
        _opts = options.Value.Xero;
    }

    public string BuildAuthorizationUrl(string state, string redirectUri)
    {
        ArgumentException.ThrowIfNullOrEmpty(state);
        ArgumentException.ThrowIfNullOrEmpty(redirectUri);

        if (string.IsNullOrEmpty(_opts.ClientId))
            throw new InvalidOperationException("Integrations:Xero:ClientId is not configured.");

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"]     = _opts.ClientId,
            ["redirect_uri"]  = redirectUri,
            ["scope"]         = _opts.Scopes,
            ["state"]         = state
        };
        return $"{AuthorizeUrl}?{QueryStringUtil.Build(query)}";
    }

    public async Task<OAuthTokens> ExchangeCodeAsync(
        string code, string redirectUri, string? realmIdHint, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]   = "authorization_code",
            ["code"]         = code,
            ["redirect_uri"] = redirectUri
        });

        var tokens = await PostTokenRequestAsync(body, ct);

        // Xero requires a follow-up call to discover the tenantId.
        var realmId = await DiscoverTenantIdAsync(tokens.AccessToken, ct);

        return MapTokens(tokens, realmId);
    }

    public async Task<OAuthTokens> RefreshAsync(string refreshToken, string realmId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);
        ArgumentException.ThrowIfNullOrEmpty(realmId);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var tokens = await PostTokenRequestAsync(body, ct);
        // Realm doesn't change on refresh; reuse what we already have.
        return MapTokens(tokens, realmId);
    }

    private async Task<XeroTokenResponse> PostTokenRequestAsync(HttpContent body, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_opts.ClientId) || string.IsNullOrEmpty(_opts.ClientSecret))
        {
            throw new InvalidOperationException("Integrations:Xero:ClientId/ClientSecret not configured.");
        }

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var httpResponse = await _http.SendAsync(request, ct);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var error = await httpResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Xero token endpoint returned {(int)httpResponse.StatusCode}: {error}");
        }

        return await httpResponse.Content.ReadFromJsonAsync<XeroTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Xero token response was empty.");
    }

    private async Task<string> DiscoverTenantIdAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ConnectionsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Xero /connections returned {(int)response.StatusCode}: {error}");
        }

        var connections = await response.Content.ReadFromJsonAsync<XeroConnection[]>(cancellationToken: ct);
        var first = connections?.FirstOrDefault()
            ?? throw new InvalidOperationException("Xero token had no tenant connections.");
        return first.TenantId;
    }

    private static OAuthTokens MapTokens(XeroTokenResponse r, string realmId)
    {
        var now = DateTime.UtcNow;
        return new OAuthTokens(
            AccessToken:           r.AccessToken,
            RefreshToken:          r.RefreshToken,
            AccessTokenExpiresAt:  now.AddSeconds(r.ExpiresIn),
            RefreshTokenExpiresAt: null,   // Xero doesn't return refresh token expiry
            Scopes:                r.Scope,
            RealmId:               realmId);
    }

    private sealed record XeroTokenResponse(
        [property: JsonPropertyName("access_token")]  string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")]    int ExpiresIn,
        [property: JsonPropertyName("token_type")]    string? TokenType,
        [property: JsonPropertyName("scope")]         string? Scope);

    private sealed record XeroConnection(
        [property: JsonPropertyName("id")]         string Id,
        [property: JsonPropertyName("tenantId")]   string TenantId,
        [property: JsonPropertyName("tenantType")] string TenantType,
        [property: JsonPropertyName("tenantName")] string TenantName);
}
