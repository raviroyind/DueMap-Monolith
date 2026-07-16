using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DueMap.Integrations.Accounting.OAuth;

/// <summary>
/// Intuit OpenID Connect userinfo. The sign-in flow prefers the id_token's
/// email claim, but Intuit frequently issues id_tokens carrying only
/// sub/aud/iss and serves profile claims (email, name) from this endpoint —
/// so it's the reliable source for "who is the human signing in".
/// </summary>
public interface IIntuitUserInfoClient
{
    /// <summary>Null on any failure — callers decide how to degrade.</summary>
    Task<IntuitUserInfo?> GetAsync(string accessToken, CancellationToken ct);
}

public sealed record IntuitUserInfo(
    [property: JsonPropertyName("sub")]           string? Sub,
    [property: JsonPropertyName("email")]         string? Email,
    [property: JsonPropertyName("emailVerified")] bool?   EmailVerified,
    [property: JsonPropertyName("givenName")]     string? GivenName,
    [property: JsonPropertyName("familyName")]    string? FamilyName);

internal sealed class IntuitUserInfoClient : IIntuitUserInfoClient
{
    // Userinfo lives on the accounts host, which is environment-split the
    // same way the QBO data API is.
    private const string ProductionUrl = "https://accounts.platform.intuit.com/v1/openid_connect/userinfo";
    private const string SandboxUrl    = "https://sandbox-accounts.platform.intuit.com/v1/openid_connect/userinfo";

    private readonly HttpClient _http;
    private readonly IntegrationsOptions.QuickBooksOptions _opts;

    public IntuitUserInfoClient(HttpClient http, IOptions<IntegrationsOptions> options)
    {
        _http = http;
        _opts = options.Value.QuickBooks;
    }

    public async Task<IntuitUserInfo?> GetAsync(string accessToken, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        var url = string.Equals(_opts.Environment, "production", StringComparison.OrdinalIgnoreCase)
            ? ProductionUrl
            : SandboxUrl;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Token without openid scope (legacy connections) or transient
            // failure — the caller falls back to other identity sources.
            return null;
        }

        return await response.Content.ReadFromJsonAsync<IntuitUserInfo>(cancellationToken: ct);
    }
}
