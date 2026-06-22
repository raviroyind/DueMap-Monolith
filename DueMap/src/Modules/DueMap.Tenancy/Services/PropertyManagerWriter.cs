using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PropertyManagerWriter : IPropertyManagerWriter
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PropertyManagerWriter(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<PropertyManager> CreateAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var row = new PropertyManager
        {
            Name = name.Trim(),
            // explicit so default-changes can't accidentally silently bump new PMs
            OnboardingStatus = OnboardingStatus.Registered,
            TimeZoneId = "UTC",
            CreatedAt = DateTime.UtcNow
        };
        db.PropertyManagers.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.PropertyManagers.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row is null) return;
        db.PropertyManagers.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetMinimumStatusAsync(int propertyManagerId, OnboardingStatus status, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.PropertyManagers.FirstOrDefaultAsync(p => p.Id == propertyManagerId, ct);
        if (row is null) return;
        // Only advance — never regress. Callers can fire this at every milestone
        // without coordinating order.
        if ((int)status > (int)row.OnboardingStatus)
        {
            row.OnboardingStatus = status;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RecordAutoSetupSummaryAsync(int propertyManagerId, string summaryJson, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summaryJson);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.PropertyManagers.FirstOrDefaultAsync(p => p.Id == propertyManagerId, ct)
            ?? throw new InvalidOperationException($"PropertyManager {propertyManagerId} not found.");

        row.AutoSetupSummary = summaryJson;
        row.AutoSetupDoneAt  = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
