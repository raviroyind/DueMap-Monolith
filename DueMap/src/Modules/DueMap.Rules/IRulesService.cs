using DueMap.Rules.Domain;

namespace DueMap.Rules;

/// <summary>
/// Resolves the late-fee rule that applies to a specific lease on a specific
/// assessment date. This is the hottest code path in the product — called every
/// time a fee is assessed and every time a notice is rendered.
/// </summary>
public interface IRulesService
{
    /// <summary>
    /// Resolve the effective rule for a (state, date) pair, optionally merging
    /// in a city/county override. Returns <c>null</c> if no state rule is in
    /// effect for that date — callers must treat that as "do not assess".
    /// </summary>
    /// <param name="stateCode">Two-letter US state code, e.g. "CA".</param>
    /// <param name="assessmentDate">Calendar date of the assessment in property local time.</param>
    /// <param name="jurisdictionId">Optional local jurisdiction (city/county) row id.</param>
    Task<ResolvedRule?> ResolveRuleAsync(
        string stateCode,
        DateOnly assessmentDate,
        int? jurisdictionId,
        CancellationToken ct);

    /// <summary>
    /// Same as <see cref="ResolveRuleAsync(string, DateOnly, int?, CancellationToken)"/>
    /// but keyed on the <c>rules.states.id</c> surrogate — what other modules
    /// (Tenancy/Billing) carry on their entities.
    /// </summary>
    Task<ResolvedRule?> ResolveRuleByStateIdAsync(
        int stateId,
        DateOnly assessmentDate,
        int? jurisdictionId,
        CancellationToken ct);
}
