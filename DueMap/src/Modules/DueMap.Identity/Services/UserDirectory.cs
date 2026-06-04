using DueMap.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Identity.Services;

internal sealed class UserDirectory : IUserDirectory
{
    private readonly AppIdentityDbContext _db;

    public UserDirectory(AppIdentityDbContext db)
    {
        _db = db;
    }

    public Task<string?> GetPrimaryEmailForPmAsync(int propertyManagerId, CancellationToken ct) =>
        _db.Users.AsNoTracking()
            .Where(u => u.PropertyManagerId == propertyManagerId)
            .OrderBy(u => u.Id)   // deterministic "first" — replace with a primary-contact flag when multi-user orgs land
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);
}
