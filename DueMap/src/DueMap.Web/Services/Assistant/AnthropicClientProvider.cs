using Anthropic;
using Microsoft.Extensions.Options;

namespace DueMap.Web.Services.Assistant;

/// <summary>
/// Singleton holder for the Anthropic SDK client. The client is thread-safe
/// and reused across circuits; it is only constructed when an API key is
/// configured so the rest of the app never trips over a missing credential.
/// </summary>
public sealed class AnthropicClientProvider
{
    public AnthropicClientProvider(IOptions<AssistantOptions> options)
    {
        // Prefer the explicit config/user-secrets value; fall back to the
        // conventional ANTHROPIC_API_KEY environment variable so a dev box
        // that already has one exported works with zero setup.
        var apiKey = options.Value.ApiKey
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            Client = new AnthropicClient { ApiKey = apiKey };
        }
    }

    public AnthropicClient? Client { get; }

    public bool IsConfigured => Client is not null;
}
