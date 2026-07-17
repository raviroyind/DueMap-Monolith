using DueMap.Billing.Domain;
using DueMap.Billing.Services;
using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Accounting;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P0-3 acceptance: the orchestrator's health gate skips PMs with broken
/// connections — but only when the <c>ops.connection_health</c> feature
/// flag is enabled. Three tests:
/// <list type="number">
///   <item>Flag off + broken connection → existing behaviour preserved
///   (sync attempted, no skip). This is the "ship dark" regression guard.</item>
///   <item>Flag on + broken connection → skip with a paused outcome AND
///   the slot claim / sync are never attempted.</item>
///   <item>Flag on + healthy connection → normal processing.</item>
/// </list>
/// </summary>
public sealed class PmDailyOrchestratorHealthGateTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private const int PmId = 7;

    private readonly IPmProcessingRunRepository _runs        = Substitute.For<IPmProcessingRunRepository>();
    private readonly IPmAccountingSync          _sync        = Substitute.For<IPmAccountingSync>();
    private readonly IAccountingConnectionService _connections = Substitute.For<IAccountingConnectionService>();
    private readonly ILeaseReader               _leases      = Substitute.For<ILeaseReader>();
    private readonly IAssessmentPlanner         _planner     = Substitute.For<IAssessmentPlanner>();
    private readonly IActionExecutor            _executor    = Substitute.For<IActionExecutor>();
    private readonly IPaymentPromiseService     _promises    = Substitute.For<IPaymentPromiseService>();
    private readonly IFeatureFlags              _flags       = Substitute.For<IFeatureFlags>();

    private PmDailyOrchestrator NewSut() => new(
        _runs, _sync, _connections, _leases, _planner, _executor, _promises, _flags,
        NullLogger<PmDailyOrchestrator>.Instance);

    // ----------------------------------------------------------------------
    // Regression guard: flag off → broken connections are NOT skipped.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Flag_off_processes_PM_even_when_connection_broken()
    {
        _flags.IsEnabledAsync("ops.connection_health", PmId, Arg.Any<CancellationToken>())
            .Returns(false);

        // The slot claim returns null ("already processed") so we short-circuit
        // before the empty-leases path — that's fine, the assertion is that we
        // GOT to the slot claim at all, meaning the health gate didn't fire.
        _runs.TryStartAsync(PmId, Today, Arg.Any<CancellationToken>())
            .Returns((PmProcessingRun?)null);

        var sut = NewSut();
        var outcome = await sut.ProcessAsync(PmId, Today);

        Assert.Equal("already processed", outcome.FailureReason);
        // The health gate must not have been consulted at all.
        await _connections.DidNotReceive().GetHealthAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        // And the slot-claim path was reached.
        await _runs.Received(1).TryStartAsync(PmId, Today, Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // Flag on → broken connection → skip with paused outcome.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Flag_on_skips_PM_when_connection_broken_and_returns_paused_reason()
    {
        _flags.IsEnabledAsync("ops.connection_health", PmId, Arg.Any<CancellationToken>())
            .Returns(true);

        _connections.GetHealthAsync(PmId, Arg.Any<CancellationToken>())
            .Returns(new ConnectionHealthSnapshot(
                PropertyManagerId: PmId,
                Provider:          AccountingProvider.QuickBooks,
                HealthStatus:      ConnectionHealthStatus.Broken,
                PausedReason:      "Token refresh failed: invalid_grant",
                LastHealthCheck:   DateTime.UtcNow));

        var sut = NewSut();
        var outcome = await sut.ProcessAsync(PmId, Today);

        Assert.False(outcome.Started);
        Assert.NotNull(outcome.FailureReason);
        Assert.Contains("connection broken", outcome.FailureReason);
        Assert.Contains("invalid_grant", outcome.FailureReason);

        // CRITICAL: zero downstream work happened.
        await _runs.DidNotReceive().TryStartAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _sync.DidNotReceive().SyncForAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _leases.DidNotReceive().ListActiveAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // Flag on → healthy connection → proceeds (slot claim attempted).
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Flag_on_processes_normally_when_connection_healthy()
    {
        _flags.IsEnabledAsync("ops.connection_health", PmId, Arg.Any<CancellationToken>())
            .Returns(true);

        _connections.GetHealthAsync(PmId, Arg.Any<CancellationToken>())
            .Returns(new ConnectionHealthSnapshot(
                PropertyManagerId: PmId,
                Provider:          AccountingProvider.QuickBooks,
                HealthStatus:      ConnectionHealthStatus.Healthy,
                PausedReason:      null,
                LastHealthCheck:   DateTime.UtcNow));

        // Short-circuit via "already processed" again — keeps the test focused
        // on "did the gate let us through?" without standing up the whole
        // sync + planner + executor stack.
        _runs.TryStartAsync(PmId, Today, Arg.Any<CancellationToken>())
            .Returns((PmProcessingRun?)null);

        var sut = NewSut();
        var outcome = await sut.ProcessAsync(PmId, Today);

        Assert.Equal("already processed", outcome.FailureReason);
        await _connections.Received(1).GetHealthAsync(PmId, Arg.Any<CancellationToken>());
        await _runs.Received(1).TryStartAsync(PmId, Today, Arg.Any<CancellationToken>());
    }
}
