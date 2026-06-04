using DueMap.Tenancy.Domain;

namespace DueMap.Billing;

/// <summary>
/// Determines the current rent period's due date for a lease as of a given
/// assessment date. v1 assumes monthly rent, due on the day-of-month equal to
/// the lease's start date (clamped for short months). Pluggable so future
/// variants (bi-weekly, due-on-1st, custom anchors) can drop in.
/// </summary>
public interface IRentScheduleService
{
    DateOnly GetCurrentDueDate(Lease lease, DateOnly assessmentDate);
}
