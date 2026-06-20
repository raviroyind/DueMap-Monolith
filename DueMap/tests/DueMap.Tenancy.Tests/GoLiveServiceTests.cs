using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using DueMap.Tenancy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DueMap.Tenancy.Tests;

/// <summary>
/// P1-4 acceptance: the "Go live" transition is the ONLY thing that makes a
/// PM's staged late-fee profiles assessable. These tests drive
/// <see cref="GoLiveService"/> against the EF in-memory provider so the
/// staged→live flip + status advance are exercised end-to-end (no real SQL).
///
/// Together with <c>AssessmentPlannerStagedSkipTests</c> (staged ⇒ no actions;
/// unstaged ⇒ normal) this proves the prompt's contract: "staged fees become
/// live only at Go live."
/// </summary>
public sealed class GoLiveServiceTests
{
    // ----- in-memory IDbContextFactory ------------------------------------
    private sealed class InMemoryFactory : IDbContextFactory<TenancyDbContext>
    {
        private readonly DbContextOptions<TenancyDbContext> _options;
        public InMemoryFactory(string dbName) =>
            _options = new DbContextOptionsBuilder<TenancyDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        public TenancyDbContext CreateDbContext() => new(_options);
        public Task<TenancyDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new TenancyDbContext(_options));
    }

    private static GoLiveService NewSut(IDbContextFactory<TenancyDbContext> factory) =>
        new(factory, NullLogger<GoLiveService>.Instance);

    private static PropertyManager Pm(int id, OnboardingStatus status) =>
        new() { Id = id, Name = $"PM{id}", OnboardingStatus = status, TimeZoneId = "UTC", CreatedAt = DateTime.UtcNow };

    private static Lease Lease(int id, int pmId, bool staged) =>
        new()
        {
            Id = id, PropertyManagerId = pmId, StateId = 1, MonthlyRent = 1000m,
            StartDate = new DateOnly(2025, 1, 1), FeesStaged = staged
        };

    // ----------------------------------------------------------------------
    // Happy path: flips this PM's staged leases live + advances to Active.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Go_live_flips_staged_leases_and_sets_active()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        await using (var seed = factory.CreateDbContext())
        {
            seed.PropertyManagers.Add(Pm(1, OnboardingStatus.Synced));
            seed.Leases.AddRange(
                Lease(10, pmId: 1, staged: true),
                Lease(11, pmId: 1, staged: true),
                Lease(12, pmId: 1, staged: false));   // already live — untouched
            // Another PM, also staged — must NOT be flipped (scoping).
            seed.PropertyManagers.Add(Pm(2, OnboardingStatus.Synced));
            seed.Leases.Add(Lease(20, pmId: 2, staged: true));
            await seed.SaveChangesAsync();
        }

        var result = await NewSut(factory).GoLiveAsync(1, CancellationToken.None);

        Assert.Equal(2, result.LeasesActivated);
        Assert.False(result.WasAlreadyActive);

        await using var check = factory.CreateDbContext();
        Assert.Equal(OnboardingStatus.Active, (await check.PropertyManagers.FindAsync(1))!.OnboardingStatus);
        Assert.False((await check.Leases.FindAsync(10))!.FeesStaged);
        Assert.False((await check.Leases.FindAsync(11))!.FeesStaged);
        Assert.False((await check.Leases.FindAsync(12))!.FeesStaged);   // was already live
        // PM 2 untouched.
        Assert.True((await check.Leases.FindAsync(20))!.FeesStaged);
        Assert.Equal(OnboardingStatus.Synced, (await check.PropertyManagers.FindAsync(2))!.OnboardingStatus);
    }

    // ----------------------------------------------------------------------
    // Idempotent: a second Go-live activates nothing and reports already-live.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Go_live_is_idempotent()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        await using (var seed = factory.CreateDbContext())
        {
            seed.PropertyManagers.Add(Pm(1, OnboardingStatus.Synced));
            seed.Leases.Add(Lease(10, pmId: 1, staged: true));
            await seed.SaveChangesAsync();
        }

        var sut = NewSut(factory);
        var first  = await sut.GoLiveAsync(1, CancellationToken.None);
        var second = await sut.GoLiveAsync(1, CancellationToken.None);

        Assert.Equal(1, first.LeasesActivated);
        Assert.False(first.WasAlreadyActive);

        Assert.Equal(0, second.LeasesActivated);
        Assert.True(second.WasAlreadyActive);
    }

    // ----------------------------------------------------------------------
    // An already-Active PM with no staged leases is a clean no-op.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Go_live_on_already_live_pm_with_no_staged_leases_activates_nothing()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        await using (var seed = factory.CreateDbContext())
        {
            seed.PropertyManagers.Add(Pm(1, OnboardingStatus.Active));
            seed.Leases.Add(Lease(10, pmId: 1, staged: false));
            await seed.SaveChangesAsync();
        }

        var result = await NewSut(factory).GoLiveAsync(1, CancellationToken.None);

        Assert.Equal(0, result.LeasesActivated);
        Assert.True(result.WasAlreadyActive);
    }

    // ----------------------------------------------------------------------
    // Missing PM throws (caller passed a bad id).
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Go_live_for_unknown_pm_throws()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSut(factory).GoLiveAsync(999, CancellationToken.None));
    }
}
