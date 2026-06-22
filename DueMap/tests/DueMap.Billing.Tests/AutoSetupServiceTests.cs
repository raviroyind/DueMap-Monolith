using DueMap.Billing.AutoSetup;
using DueMap.Common.FeatureFlags;
using DueMap.Rules;
using DueMap.Rules.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// AutoSetup unit tests. The service orchestrates writes against
/// <see cref="ILeaseWriter"/> + <see cref="IPropertyManagerWriter"/> +
/// <see cref="IPmNoticePreferencesService"/>; we mock all of them and
/// verify the SHAPE of what the service wrote — never the underlying DB.
///
/// The compliance-critical guarantees the roadmap prompt demanded:
///   * every auto-set fee is within state limits,
///   * ambiguous state → safe default + flag,
///   * reasonableness state → safe default + flag.
///
/// All three live here.
/// </summary>
public sealed class AutoSetupServiceTests
{
    private const int PmId = 42;
    private static readonly DateOnly Today = new(2026, 6, 9);

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static (AutoSetupService Sut,
                    ILeaseWriter LeaseWriter,
                    IPropertyManagerWriter PmWriter,
                    IFeatureFlags Flags) Build(
        IReadOnlyList<Lease> leases,
        IReadOnlyList<State> states,
        Func<int, ResolvedRule?> ruleByStateId)
    {
        var leaseReader = Substitute.For<ILeaseReader>();
        leaseReader.ListActiveAsync(PmId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(leases);

        var leaseWriter = Substitute.For<ILeaseWriter>();
        var pmWriter    = Substitute.For<IPropertyManagerWriter>();
        var pmPrefs     = Substitute.For<IPmNoticePreferencesService>();
        pmPrefs.GetOrCreateAsync(PmId, Arg.Any<CancellationToken>())
            .Returns(new PmNoticePreferences { PropertyManagerId = PmId });

        var rules = Substitute.For<IRulesService>();
        rules.ResolveRuleByStateIdAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call => ruleByStateId((int)call[0]));

        var stateReader = Substitute.For<IStateReader>();
        stateReader.ListAllAsync(Arg.Any<CancellationToken>()).Returns(states);

        var scan = new ComplianceScanService();

        var flags = Substitute.For<IFeatureFlags>();
        flags.IsEnabledAsync("onboarding.auto_setup", PmId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new AutoSetupService(
            leaseReader, leaseWriter, pmWriter, pmPrefs, rules, stateReader, scan, flags,
            NullLogger<AutoSetupService>.Instance);

        return (sut, leaseWriter, pmWriter, flags);
    }

    private static Lease NewLease(int id, int stateId, decimal rent = 1000m, string? inferredState = null) =>
        new()
        {
            Id = id,
            PropertyManagerId = PmId,
            StateId = stateId,
            MonthlyRent = rent,
            StartDate = new DateOnly(2025, 1, 1),
            InferredState = inferredState
        };

    private static State NewState(int id, string code) =>
        new() { Id = id, Code = code, Name = code };

    private static ResolvedRule Rule(
        decimal? recommendedPct = null,
        decimal? maxPercent = null,
        byte minGrace = 0,
        byte standardKind = 1,
        decimal? safeDefaultPct = null) =>
        new(
            StateCode: "CA",
            StateRuleVersionId: 1,
            LocalRuleOverrideId: null,
            AssessmentDate: Today,
            GracePeriodDays: minGrace,
            LateFeeType: LateFeeType.Percent,
            FlatAmount: null,
            PercentOfRent: recommendedPct,    // fraction (0.08 = 8%)
            HardCapAmount: null,
            NoticeRequiredBeforeFee: false,
            NoticeAdvanceDays: null,
            SourceCitation: "test",
            StateMaxPercent:   maxPercent,
            StateMaxFlatAmount: null,
            StateMinGraceDays: minGrace,
            DailyAccrualOk:    false,
            RequiresWrittenDisclosure: true,
            StandardKind:      standardKind,
            SafeDefaultPct:    safeDefaultPct);

    // ----------------------------------------------------------------------
    // 1. The compliance-critical promise: an auto-set fee NEVER lands above
    //    the state cap. Rule says recommended = 8% but max = 5% → AutoSetup
    //    writes 5%, not 8%.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Auto_set_fee_is_clamped_to_state_max_percent()
    {
        var lease = NewLease(id: 100, stateId: 1, rent: 1500m);
        var (sut, leaseWriter, _, _) = Build(
            leases: new[] { lease },
            states: new[] { NewState(1, "CA") },
            ruleByStateId: _ => Rule(recommendedPct: 0.08m, maxPercent: 5m));

        await sut.RunAsync(PmId, CancellationToken.None);

        // The single recorded write should carry late_fee_percent = 5
        // (not 8). FeesStaged side-effect is enforced by the writer impl
        // and out of scope for this mock-shaped test.
        await leaseWriter.Received(1).UpdateLateFeeProfileAsync(
            100,
            Arg.Is<LateFeeProfileInput>(p => p.LateFeePercent == 5m),
            Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // 2. Ambiguous state — no StateId AND no inferred_state → no rule
    //    resolved → ComplianceFinding(AmbiguousState).
    //    The service should NOT call the lease writer for this lease.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Ambiguous_state_lease_emits_a_finding_and_is_not_written()
    {
        // StateId = 0 doesn't match any state in our lookup → ambiguous.
        var lease = NewLease(id: 200, stateId: 0, inferredState: null);

        var (sut, leaseWriter, _, _) = Build(
            leases: new[] { lease },
            states: new[] { NewState(1, "CA") },
            ruleByStateId: _ => null);

        var summary = await sut.RunAsync(PmId, CancellationToken.None);

        Assert.Contains(summary.Findings, f =>
            f.LeaseId == 200 && f.Kind == ComplianceFindingKind.AmbiguousState);

        // No write — staging an ambiguous-state lease without a rule would
        // be the exact compliance bug we're guarding against.
        await leaseWriter.DidNotReceive().UpdateLateFeeProfileAsync(
            200, Arg.Any<LateFeeProfileInput>(), Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // 3. Reasonableness jurisdiction (standard_kind = 2) — use the
    //    safe_default_pct AND emit a finding so the PM is told the number
    //    is a conservative guess, not a statute.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Reasonableness_state_uses_safe_default_and_emits_finding()
    {
        var lease = NewLease(id: 300, stateId: 2, rent: 1000m);
        var (sut, leaseWriter, _, _) = Build(
            leases: new[] { lease },
            states: new[] { NewState(2, "TX") },
            ruleByStateId: _ => Rule(
                recommendedPct: 0.10m,      // would be too generous
                maxPercent: null,
                standardKind: 2,
                safeDefaultPct: 5m));

        var summary = await sut.RunAsync(PmId, CancellationToken.None);

        // safe_default_pct = 5% wins over the recommended 10%.
        await leaseWriter.Received(1).UpdateLateFeeProfileAsync(
            300,
            Arg.Is<LateFeeProfileInput>(p => p.LateFeePercent == 5m),
            Arg.Any<CancellationToken>());

        Assert.Contains(summary.Findings, f =>
            f.LeaseId == 300 && f.Kind == ComplianceFindingKind.ReasonablenessJurisdiction);
    }

    // ----------------------------------------------------------------------
    // 4. Summary write — AutoSetup stamps the PM row exactly once with
    //    JSON that round-trips through System.Text.Json.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Auto_setup_records_summary_on_property_manager_exactly_once()
    {
        var lease = NewLease(id: 400, stateId: 1);
        var (sut, _, pmWriter, _) = Build(
            leases: new[] { lease },
            states: new[] { NewState(1, "CA") },
            ruleByStateId: _ => Rule(recommendedPct: 0.05m, maxPercent: 5m));

        await sut.RunAsync(PmId, CancellationToken.None);

        await pmWriter.Received(1).RecordAutoSetupSummaryAsync(
            PmId,
            Arg.Is<string>(json => json.Contains("LeasesProcessed", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------
    // 5. Flag off → no writes anywhere, empty summary. Lets us safely wire
    //    AutoSetup into the post-sync flow before flipping the flag for
    //    real PMs.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Flag_off_is_a_no_op()
    {
        var lease = NewLease(id: 500, stateId: 1);
        var (sut, leaseWriter, pmWriter, flags) = Build(
            leases: new[] { lease },
            states: new[] { NewState(1, "CA") },
            ruleByStateId: _ => Rule(recommendedPct: 0.05m, maxPercent: 5m));

        flags.IsEnabledAsync("onboarding.auto_setup", PmId, Arg.Any<CancellationToken>())
            .Returns(false);

        var summary = await sut.RunAsync(PmId, CancellationToken.None);

        Assert.Equal(0, summary.LeasesProcessed);
        Assert.Empty(summary.Findings);
        await leaseWriter.DidNotReceive().UpdateLateFeeProfileAsync(
            Arg.Any<int>(), Arg.Any<LateFeeProfileInput>(), Arg.Any<CancellationToken>());
        await pmWriter.DidNotReceive().RecordAutoSetupSummaryAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
