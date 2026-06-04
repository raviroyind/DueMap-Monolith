using DueMap.Rules.Domain;
using DueMap.Rules.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Rules.Services;

internal sealed class StateReader : IStateReader
{
    private readonly RulesDbContext _db;

    public StateReader(RulesDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<State>> ListAllAsync(CancellationToken ct) =>
        await _db.States.AsNoTracking()
            .OrderBy(s => s.Code)
            .ToListAsync(ct);
}
