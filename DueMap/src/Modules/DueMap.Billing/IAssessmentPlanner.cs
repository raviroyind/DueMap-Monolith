using DueMap.Billing.Domain;
using DueMap.Tenancy.Domain;

namespace DueMap.Billing;

/// <summary>
/// Turns a (lease, assessment date) pair into a concrete <see cref="AssessmentPlan"/>
/// — the list of actions the orchestrator should execute today. Pure decision
/// logic; produces no side effects. The dispatcher is responsible for actually
/// rendering notices and inserting delivery rows.
/// </summary>
public interface IAssessmentPlanner
{
    Task<AssessmentPlan> PlanAsync(Lease lease, DateOnly assessmentDate, CancellationToken ct);
}
