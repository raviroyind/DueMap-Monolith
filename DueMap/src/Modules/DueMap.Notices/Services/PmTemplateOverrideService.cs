using DueMap.Notices.Domain;
using DueMap.Notices.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Notices.Services;

internal sealed class PmTemplateOverrideService : IPmTemplateOverrideService
{
    private readonly NoticesDbContext _db;

    public PmTemplateOverrideService(NoticesDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PmTemplateOverride>> ListForPmAsync(int propertyManagerId, CancellationToken ct) =>
        await _db.PmTemplateOverrides.AsNoTracking()
            .Where(o => o.PropertyManagerId == propertyManagerId)
            .OrderBy(o => o.NoticeTypeId).ThenBy(o => o.StateId)
            .ToListAsync(ct);

    public async Task<PmTemplateOverride?> GetAsync(int id, CancellationToken ct) =>
        await _db.PmTemplateOverrides.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<PmTemplateOverride> UpsertAsync(PmTemplateOverride row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);

        var now = DateTime.UtcNow;

        if (row.Id == 0)
        {
            // Deactivate any existing active row for the same (pm, type, state) — the
            // filtered unique indexes in SQL enforce this, but doing it explicitly here
            // gives a cleaner error path and a single "current" row to read back.
            var existing = await _db.PmTemplateOverrides
                .Where(o => o.IsActive
                         && o.PropertyManagerId == row.PropertyManagerId
                         && o.NoticeTypeId == row.NoticeTypeId
                         && o.StateId == row.StateId)
                .ToListAsync(ct);
            foreach (var prior in existing) prior.IsActive = false;

            row.IsActive = true;
            row.CreatedAt = now;
            row.UpdatedAt = now;
            _db.PmTemplateOverrides.Add(row);
        }
        else
        {
            var tracked = await _db.PmTemplateOverrides.FirstOrDefaultAsync(o => o.Id == row.Id, ct)
                ?? throw new InvalidOperationException($"PM template override {row.Id} not found.");
            tracked.Subject = row.Subject;
            tracked.BodyHtml = row.BodyHtml;
            tracked.BodyText = row.BodyText;
            tracked.IsActive = row.IsActive;
            tracked.UpdatedAt = now;
            row = tracked;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var row = await _db.PmTemplateOverrides.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return;
        row.IsActive = false;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
