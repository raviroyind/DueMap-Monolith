using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PropertyManagerReader : IPropertyManagerReader
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PropertyManagerReader(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<PropertyManager>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PropertyManagers.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<PropertyManager?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PropertyManagers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }
}
