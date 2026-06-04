using DueMap.Tenancy.Domain;

namespace DueMap.Billing.Services;

/// <summary>
/// Monthly rent on the day-of-month derived from the lease start date.
/// Clamps to the last day of the month for shorter months (Feb 28/29 etc.).
/// </summary>
internal sealed class MonthlyRentScheduleService : IRentScheduleService
{
    public DateOnly GetCurrentDueDate(Lease lease, DateOnly assessmentDate)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var anchorDay = lease.StartDate.Day;

        var thisMonth = ClampToMonth(assessmentDate.Year, assessmentDate.Month, anchorDay);

        // "Current" rent period = the most recent due date <= assessmentDate.
        // If today is before this month's anchor, the current period belongs to last month.
        if (assessmentDate < thisMonth)
        {
            var prevMonth = assessmentDate.AddMonths(-1);
            return ClampToMonth(prevMonth.Year, prevMonth.Month, anchorDay);
        }

        return thisMonth;
    }

    private static DateOnly ClampToMonth(int year, int month, int day)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, lastDay));
    }
}
