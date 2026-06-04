using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;

namespace DueMap.Web.Services;

/// <summary>
/// Per-circuit current PM, resolved from the <c>PmId</c> claim on the
/// authenticated user. Cached after the first resolution so pages don't pay
/// the cost on every call. Throws if no claim is present — the fallback
/// authorize policy means unauthenticated users never reach a Blazor page.
/// </summary>
public sealed class PmContext
{
    public const string PmIdClaimType = "PmId";

    private readonly AuthenticationStateProvider _auth;
    private int? _cached;

    public PmContext(AuthenticationStateProvider auth)
    {
        _auth = auth;
    }

    public async Task<int> GetPmIdAsync()
    {
        if (_cached is int cached) return cached;

        var state = await _auth.GetAuthenticationStateAsync();
        var claim = state.User.FindFirst(PmIdClaimType)
            ?? throw new InvalidOperationException(
                "Current user has no PmId claim. Sign out and sign back in.");

        var value = int.Parse(claim.Value, CultureInfo.InvariantCulture);
        _cached = value;
        return value;
    }

    public async Task<string?> GetUserDisplayNameAsync()
    {
        var state = await _auth.GetAuthenticationStateAsync();
        return state.User.Identity?.Name;
    }
}
