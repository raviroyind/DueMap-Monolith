namespace DueMap.Web.Security;

/// <summary>
/// Bound from <c>Ops:AdminAuth</c> in configuration / user-secrets / env vars.
/// Both fields are required — if either is missing,
/// <see cref="AdminBasicAuthMiddleware"/> returns 503 with a clear log line
/// rather than falling back to hardcoded dev creds (P0-4 hardening).
///
/// Set in dev with user-secrets:
/// <code>
///   dotnet user-secrets set Ops:AdminAuth:Username admin
///   dotnet user-secrets set Ops:AdminAuth:Password "&lt;long random string&gt;"
/// </code>
/// In prod, drive these from env vars / a secret store.
/// </summary>
public sealed class AdminBasicAuthOptions
{
    public const string SectionName = "Ops:AdminAuth";

    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
