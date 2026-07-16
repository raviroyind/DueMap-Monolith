using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DueMap.Integrations.Accounting.OAuth;

/// <summary>
/// Intuit / QuickBooks Online OAuth2. Authorize on <c>appcenter.intuit.com</c>;
/// token exchange and refresh on <c>oauth.platform.intuit.com</c>. The realm
/// id (QB "company") arrives as a query param on the callback.
/// </summary>
internal sealed class QuickBooksOAuthProvider : IOAuthProvider
{
    private const string AuthorizeUrl    = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenUrl        = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    private readonly HttpClient _http;
    private readonly IntegrationsOptions.QuickBooksOptions _opts;

    public AccountingProvider Provider => AccountingProvider.QuickBooks;

    public QuickBooksOAuthProvider(HttpClient http, IOptions<IntegrationsOptions> options)
    {
        _http = http;
        _opts = options.Value.QuickBooks;
    }

    public string BuildAuthorizationUrl(string state, string redirectUri)
    {
        ArgumentException.ThrowIfNullOrEmpty(state);
        ArgumentException.ThrowIfNullOrEmpty(redirectUri);

        if (string.IsNullOrEmpty(_opts.ClientId))
            throw new InvalidOperationException("Integrations:QuickBooks:ClientId is not configured.");

        var query = new Dictionary<string, string?>
        {
            ["client_id"]     = _opts.ClientId,
            ["response_type"] = "code",
            ["scope"]         = _opts.Scopes,
            ["redirect_uri"]  = redirectUri,
            ["state"]         = state
        };

        return $"{AuthorizeUrl}?{QueryStringUtil.Build(query)}";
    }

    public async Task<OAuthTokens> ExchangeCodeAsync(
        string code, string redirectUri, string? realmIdHint, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(realmIdHint);   // QB callback always carries realmId

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]   = "authorization_code",
            ["code"]         = code,
            ["redirect_uri"] = redirectUri
        });

        var response = await PostTokenRequestAsync(body, ct);
        return MapTokens(response, realmIdHint);
    }

    public async Task<OAuthTokens> RefreshAsync(string refreshToken, string realmId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await PostTokenRequestAsync(body, ct);
        return MapTokens(response, realmId);
    }

    private async Task<QbTokenResponse> PostTokenRequestAsync(HttpContent body, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_opts.ClientId) || string.IsNullOrEmpty(_opts.ClientSecret))
        {
            throw new InvalidOperationException("Integrations:QuickBooks:ClientId/ClientSecret not configured.");
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
                $"QuickBooks token endpoint returned {(int)httpResponse.StatusCode}: {error}");
        }

        return await httpResponse.Content.ReadFromJsonAsync<QbTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("QuickBooks token response was empty.");
    }

    private static OAuthTokens MapTokens(QbTokenResponse r, string realmId)
    {
        var now = DateTime.UtcNow;
        return new OAuthTokens(
            AccessToken:           r.AccessToken,
            RefreshToken:          r.RefreshToken,
            AccessTokenExpiresAt:  now.AddSeconds(r.ExpiresIn),
            RefreshTokenExpiresAt: r.RefreshExpiresIn is int rExp ? now.AddSeconds(rExp) : null,
            Scopes:                null,    // QB doesn't echo scopes
            RealmId:               realmId,
            IdToken:               r.IdToken);
    }

    private sealed record QbTokenResponse(
        [property: JsonPropertyName("access_token")]      string AccessToken,
        [property: JsonPropertyName("refresh_token")]     string RefreshToken,
        [property: JsonPropertyName("expires_in")]        int ExpiresIn,
        [property: JsonPropertyName("x_refresh_token_expires_in")] int? RefreshExpiresIn,
        [property: JsonPropertyName("token_type")]        string? TokenType,
        [property: JsonPropertyName("id_token")]          string? IdToken = null);
}

internal static class QueryStringUtil
{
    public static string Build(IDictionary<string, string?> kv) =>
        string.Join("&", kv
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

    public static string FormatExpiry(DateTime utc) =>
        utc.ToString("o", CultureInfo.InvariantCulture);
}
