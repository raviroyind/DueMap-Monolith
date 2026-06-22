using DueMap.Billing.Domain;
using DueMap.Billing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Billing.Services;

/// <summary>
/// Returns the PM's active reminder sequence, or the built-in default when
/// none is configured. The default keeps a brand-new PM working without any
/// sequence setup (per-PM editing is a follow-up).
/// </summary>
internal sealed class SequenceResolver : ISequenceResolver
{
    private readonly BillingDbContext _db;

    public SequenceResolver(BillingDbContext db) => _db = db;

    public async Task<NoticeSequence> ResolveAsync(int propertyManagerId, CancellationToken ct)
    {
        var active = await _db.NoticeSequences.AsNoTracking()
            .Where(s => s.PropertyManagerId == propertyManagerId && s.IsActive)
            .FirstOrDefaultAsync(ct);

        return active ?? SequenceSchedule.Default(propertyManagerId);
    }
}
