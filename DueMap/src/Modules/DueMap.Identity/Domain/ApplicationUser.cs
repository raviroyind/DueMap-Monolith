using Microsoft.AspNetCore.Identity;

namespace DueMap.Identity.Domain;

/// <summary>
/// Auth user. Each user belongs to exactly one property manager — that link
/// is the one DueMap-specific column on top of the standard Identity schema.
/// The PmId claim is added at sign-in by <see cref="DueMap.Identity.Services.ClaimsFactory"/>.
/// </summary>
public sealed class ApplicationUser : IdentityUser<int>
{
    public int PropertyManagerId { get; set; }
    public DateTime CreatedAt { get; set; }
}
