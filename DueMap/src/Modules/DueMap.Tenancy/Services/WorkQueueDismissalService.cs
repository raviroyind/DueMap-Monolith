using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class WorkQueueDismissalService : IWorkQueueDismissalService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public WorkQueueDismissalService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task DismissAsync(int propertyManagerId, string itemKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemKey);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var exists = await db.WorkItemDismissals.AsNoTracking()
            .AnyAsync(d => d.PropertyManagerId == propertyManagerId && d.ItemKey == itemKey, ct);
        if (exists) return;

        db.WorkItemDismissals.Add(new WorkItemDismissal
        {
            PropertyManagerId = propertyManagerId,
            ItemKey = itemKey,
            DismissedAt = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique (pm, key) constraint — a concurrent dismiss of the same
            // row (double-click) already won. That's the outcome we wanted.
        }
    }

    public async Task<IReadOnlySet<string>> ListKeysAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var keys = await db.WorkItemDismissals.AsNoTracking()
            .Where(d => d.PropertyManagerId == propertyManagerId)
            .Select(d => d.ItemKey)
            .ToListAsync(ct);
        return keys.ToHashSet(StringComparer.Ordinal);
    }
}
