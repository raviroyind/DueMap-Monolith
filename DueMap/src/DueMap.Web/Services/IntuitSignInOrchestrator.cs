using System.Text.Json;
using DueMap.Identity.Domain;
using DueMap.Integrations.Accounting;
using DueMap.Integrations.Accounting.OAuth;
using DueMap.Integrations.Accounting.Sync;
using DueMap.Tenancy;
using DueMap.Tenancy.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DueMap.Web.Services;

/// <summary>
/// "Sign in with Intuit" via the Connect-to-QuickBooks OAuth flow (not OIDC).
///
/// We pivoted away from the OIDC <c>id_token</c> path because Intuit only
/// issues id_tokens for apps that have explicitly been approved as
/// "Sign in with Intuit" providers — a category change most sandbox apps
/// don't have. The pure QBO OAuth flow, on the other hand, works against
/// every Connect-to-QuickBooks app and gives us back a <c>realmId</c>
/// + access_token bundle we can use to identify the user via the
/// <c>CompanyInfo</c> endpoint.
///
/// Trade-off baked into this design: the user grants accounting access at
/// the same moment they sign in. That's actually a feature — see backlog
/// task #59. The first sign-in collapses what would have been two consent
/// screens into one, and we record the resulting connection so the PM
/// doesn't have to redo it during onboarding step 1.
/// </summary>
public interface IIntuitSignInOrchestrator
{
    /// <summary>
    /// Builds the Intuit authorize URL for a sign-in flow. <paramref name="callbackUrl"/>
    /// must be a stable absolute URL that's registered as a redirect URI on
    /// the Intuit app — we re-use the existing <c>/oauth/quickbooks/callback</c>
    /// style by adding <c>/oauth/intuit/signin-callback</c>.
    /// </summary>
    string BuildSignInUrl(string callbackUrl);

    /// <summary>
    /// Handles the redirect back from Intuit. Validates the signed state,
    /// exchanges the code, identifies the user by CompanyInfo email,
    /// provisions a new ApplicationUser + PropertyManager + accounting
    /// connection on first sign-in, or returns the existing user on repeat.
    /// </summary>
    Task<IntuitSignInResult> HandleCallbackAsync(
        string code, string state, string realmId, string callbackUrl, CancellationToken ct);
}

public sealed record IntuitSignInResult(
    ApplicationUser? User,
    bool IsNewUser,
    string? Error);
