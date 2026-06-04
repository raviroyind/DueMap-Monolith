using DueMap.Rules.Domain;

namespace DueMap.Rules;

public interface IStateReader
{
    Task<IReadOnlyList<State>> ListAllAsync(CancellationToken ct);
}
