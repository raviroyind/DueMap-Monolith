using System.Globalization;
using System.Security.Claims;
using DueMap.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DueMap.Identity.Services;

/// <summary>
/// Adds the <c>PmId</c> claim to every signed-in user's principal. Pages and
/// services read this claim to scope queries to the user's PM.
/// </summary>
internal sealed class PmClaimsFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public const string PmIdClaimType = "PmId";

    public PmClaimsFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(
            PmIdClaimType,
            user.PropertyManagerId.ToString(CultureInfo.InvariantCulture)));
        return identity;
    }
}
