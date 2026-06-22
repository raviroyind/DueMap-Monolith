using DueMap.Integrations.Persistence;
using DueMap.Integrations.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DueMap.Integrations.Tests;

/// <summary>
/// P2-2 opt-out store, exercised against the EF in-memory provider.
/// </summary>
public sealed class SmsSuppressionStoreTests
{
    private sealed class InMemoryFactory : IDbContextFactory<IntegrationsDbContext>
    {
        private readonly DbContextOptions<IntegrationsDbContext> _options;
        public InMemoryFactory(string dbName) =>
            _options = new DbContextOptionsBuilder<IntegrationsDbContext>()
                .UseInMemoryDatabase(dbName).Options;
        public IntegrationsDbContext CreateDbContext() => new(_options);
        public Task<IntegrationsDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new IntegrationsDbContext(_options));
    }

    private static SmsSuppressionStore NewSut() =>
        new(new InMemoryFactory(Guid.NewGuid().ToString()));

    [Fact]
    public async Task Suppress_then_is_suppressed_true()
    {
        var sut = NewSut();
        await sut.SuppressAsync("+1 (415) 555-1234", "Inbound SMS: STOP", null, CancellationToken.None);

        // Same number in a different format still matches (normalized).
        Assert.True(await sut.IsSuppressedAsync("4155551234", CancellationToken.None));
        Assert.True(await sut.IsSuppressedAsync("+14155551234", CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_number_is_not_suppressed()
    {
        var sut = NewSut();
        Assert.False(await sut.IsSuppressedAsync("+19998887777", CancellationToken.None));
    }

    [Fact]
    public async Task Unsuppress_clears_it()
    {
        var sut = NewSut();
        await sut.SuppressAsync("4155551234", "STOP", null, CancellationToken.None);
        await sut.UnsuppressAsync("+1-415-555-1234", CancellationToken.None);
        Assert.False(await sut.IsSuppressedAsync("4155551234", CancellationToken.None));
    }

    [Fact]
    public async Task Suppress_is_idempotent()
    {
        var sut = NewSut();
        await sut.SuppressAsync("4155551234", "STOP", null, CancellationToken.None);
        await sut.SuppressAsync("4155551234", "STOP again", null, CancellationToken.None);   // no unique-index blowup
        Assert.True(await sut.IsSuppressedAsync("4155551234", CancellationToken.None));
    }
}
