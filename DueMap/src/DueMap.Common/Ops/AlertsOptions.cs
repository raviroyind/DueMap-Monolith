namespace DueMap.Common.Ops;

/// <summary>
/// Bound from <c>Ops:Alerts</c> in configuration / user-secrets / env vars.
/// Set <see cref="Channel"/> to <c>"email"</c> or <c>"webhook"</c> to enable
/// alerting; leave blank or <c>"null"</c> to disable.
///
/// Example user-secrets for dev (email channel):
/// <code>
///   dotnet user-secrets set Ops:Alerts:Channel email
///   dotnet user-secrets set Ops:Alerts:To      ops@example.com
/// </code>
///
/// Example for the webhook channel (Slack-style incoming webhook URL):
/// <code>
///   dotnet user-secrets set Ops:Alerts:Channel    webhook
///   dotnet user-secrets set Ops:Alerts:WebhookUrl https://hooks.slack.com/...
/// </code>
/// </summary>
public sealed class AlertsOptions
{
    public const string SectionName = "Ops:Alerts";

    /// <summary>"email", "webhook", or empty/"null" (no-op).</summary>
    public string? Channel { get; set; }

    /// <summary>Required when <see cref="Channel"/> = "email".</summary>
    public string? To { get; set; }

    /// <summary>Required when <see cref="Channel"/> = "webhook".</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Dedupe window. Same <c>alertKey</c> raised again inside this window
    /// is silently swallowed. Stops a stuck failure from paging us 100x/hr.
    /// Override only for tests or aggressive ops modes.
    /// </summary>
    public TimeSpan DedupeWindow { get; set; } = TimeSpan.FromMinutes(30);
}
