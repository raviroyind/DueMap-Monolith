namespace DueMap.Web.Services.Assistant;

/// <summary>
/// Claude API configuration for the PM assistant chat. Set the key with
/// <c>dotnet user-secrets set Anthropic:ApiKey &lt;key&gt;</c> (same secrets
/// pool the QBO/Xero credentials use). When the key is absent the /assistant
/// page renders a friendly "not configured" notice instead of failing —
/// mirrors the SSO-provider pattern.
/// </summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }

    /// <summary>Claude model id. Opus 4.8 unless overridden in config.</summary>
    public string Model { get; set; } = "claude-opus-4-8";

    /// <summary>
    /// Hard per-response output cap. Chat answers are short; this mainly
    /// bounds runaway responses and keeps non-streaming calls inside the
    /// SDK's HTTP timeout guidance.
    /// </summary>
    public int MaxTokens { get; set; } = 8192;
}
