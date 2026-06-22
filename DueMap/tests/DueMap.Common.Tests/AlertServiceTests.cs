using DueMap.Common.Ops;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DueMap.Common.Tests;

/// <summary>
/// P0-4 acceptance: <see cref="AlertService"/> dedupes by key within the
/// configured window. The dedupe is the "threshold" — callers count, the
/// service guarantees once-per-window emission. Sink invocation is
/// verified via NSubstitute.
///
/// The clock is injected (test-only ctor) so we can fast-forward without
/// sleeping; no time-based flakiness in CI.
/// </summary>
public sealed class AlertServiceTests
{
    private readonly IAlertSink _sink = Substitute.For<IAlertSink>();
    private DateTime _now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private AlertService NewSut(TimeSpan? dedupe = null) =>
        new(
            _sink,
            Options.Create(new AlertsOptions { DedupeWindow = dedupe ?? TimeSpan.FromMinutes(30) }),
            NullLogger<AlertService>.Instance,
            utcNow: () => _now);

    // ----------------------------------------------------------------------
    // First raise fires the sink.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task First_raise_with_a_key_fires_the_sink()
    {
        var sut = NewSut();

        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Critical,
            "Hangfire hasn't ticked for 30 minutes");

        await _sink.Received(1).SendAsync(
            Arg.Is<string>(s => s.Contains("CRIT") && s.Contains("sweep_stalled")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // Repeat inside the window is silently swallowed.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Repeat_raise_inside_window_is_suppressed()
    {
        var sut = NewSut(dedupe: TimeSpan.FromMinutes(30));

        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Warning, "1st");

        // Fast-forward 10 minutes — still inside the 30-minute window.
        _now = _now.AddMinutes(10);

        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Warning, "2nd");
        _now = _now.AddMinutes(15);     // 25 min total — still inside
        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Warning, "3rd");

        // Only the FIRST raise should have hit the sink.
        await _sink.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // Different key always fires.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Different_keys_each_fire_independently()
    {
        var sut = NewSut();

        await sut.RaiseOperatorAsync("sweep_stalled",      AlertSeverity.Critical, "a");
        await sut.RaiseOperatorAsync("sync_auth_failures", AlertSeverity.Warning,  "b");
        await sut.RaiseOperatorAsync("dispatch_provider_down", AlertSeverity.Warning, "c");

        await _sink.Received(3).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // After window elapses, repeats fire again.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task After_dedupe_window_elapses_the_same_key_fires_again()
    {
        var sut = NewSut(dedupe: TimeSpan.FromMinutes(30));

        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Warning, "1st");

        // Inside window — suppressed.
        _now = _now.AddMinutes(20);
        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Warning, "2nd");

        // Past window (20 + 11 = 31 min since first fire).
        _now = _now.AddMinutes(11);
        await sut.RaiseOperatorAsync("sweep_stalled", AlertSeverity.Warning, "3rd");

        await _sink.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
