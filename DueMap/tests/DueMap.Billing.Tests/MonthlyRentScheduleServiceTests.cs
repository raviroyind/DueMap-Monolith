using DueMap.Billing.Services;
using DueMap.Tenancy.Domain;
using Xunit;

namespace DueMap.Billing.Tests;

public sealed class MonthlyRentScheduleServiceTests
{
    private readonly MonthlyRentScheduleService _sut = new();

    private static Lease LeaseStartedOn(int year, int month, int day) =>
        new() { Id = 1, MonthlyRent = 1000m, StartDate = new DateOnly(year, month, day) };

    [Fact]
    public void Returns_anchor_day_in_current_month_when_assessment_is_after_anchor()
    {
        var lease = LeaseStartedOn(2024, 1, 15);
        var due = _sut.GetCurrentDueDate(lease, new DateOnly(2026, 6, 20));
        Assert.Equal(new DateOnly(2026, 6, 15), due);
    }

    [Fact]
    public void Returns_anchor_day_in_current_month_when_assessment_is_on_anchor()
    {
        var lease = LeaseStartedOn(2024, 1, 15);
        var due = _sut.GetCurrentDueDate(lease, new DateOnly(2026, 6, 15));
        Assert.Equal(new DateOnly(2026, 6, 15), due);
    }

    [Fact]
    public void Returns_previous_month_anchor_when_assessment_is_before_this_month_anchor()
    {
        var lease = LeaseStartedOn(2024, 1, 15);
        var due = _sut.GetCurrentDueDate(lease, new DateOnly(2026, 6, 5));
        Assert.Equal(new DateOnly(2026, 5, 15), due);
    }

    [Fact]
    public void Clamps_anchor_day_to_short_month_end_february_non_leap()
    {
        var lease = LeaseStartedOn(2024, 1, 31);
        var due = _sut.GetCurrentDueDate(lease, new DateOnly(2026, 2, 28));
        Assert.Equal(new DateOnly(2026, 2, 28), due);
    }

    [Fact]
    public void Clamps_anchor_day_to_february_29_in_leap_year()
    {
        var lease = LeaseStartedOn(2020, 1, 31);
        var due = _sut.GetCurrentDueDate(lease, new DateOnly(2024, 2, 29));
        Assert.Equal(new DateOnly(2024, 2, 29), due);
    }

    [Fact]
    public void Returns_january_previous_year_when_assessment_is_january_before_anchor()
    {
        var lease = LeaseStartedOn(2024, 1, 15);
        var due = _sut.GetCurrentDueDate(lease, new DateOnly(2026, 1, 10));
        Assert.Equal(new DateOnly(2025, 12, 15), due);
    }

    [Fact]
    public void Throws_when_lease_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.GetCurrentDueDate(null!, new DateOnly(2026, 1, 1)));
    }
}
