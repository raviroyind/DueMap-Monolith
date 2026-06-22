using DueMap.Billing.Domain;
using DueMap.Billing.Services;
using DueMap.Integrations.Notices;
using DueMap.Notices;
using DueMap.Rules;
using DueMap.Rules.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P1-6 §6.2 evidence stamping. Every Live late-fee write must carry a
/// resolvable rule version AND a disclosure snapshot of the terms that
/// produced it — and a fee must never be written when no rule resolves.
/// </summary>
public sealed class ActionExecutorEvidenceTests
{
    private static readonly DateOnly Today   = new(2026, 6, 15);
    private static readonly DateOnly DueDate = new(2026, 6, 1);

    private readonly INoticeTemplateService           _templates   = Substitute.For<INoticeTemplateService>();
    private readonly INoticeRenderer                  _renderer    = Substitute.For<INoticeRenderer>();
    private readonly INoticeDeliveryRepository        _deliveries  = Substitute.For<INoticeDeliveryRepository>();
    private readonly INoticeDispatcher                _dispatcher  = Substitute.For<INoticeDispatcher>();
    private readonly ITenantContactResolver           _contacts    = Substitute.For<ITenantContactResolver>();
    private readonly IRentInvoiceRepository           _invoices    = Substitute.For<IRentInvoiceRepository>();
    private readonly IRulesService                    _rules       = Substitute.For<IRulesService>();
    private readonly IAssessmentRunRepository         _runs        = Substitute.For<IAssessmentRunRepository>();
    private readonly ILateFeeAssessmentRepository     _fees        = Substitute.For<ILateFeeAssessmentRepository>();
    private readonly ILateFeeInvoiceAttachmentFetcher _attachments = Substitute.For<ILateFeeInvoiceAttachmentFetcher>();
    private readonly DueMap.Common.FeatureFlags.IFeatureFlags _flags = Substitute.For<DueMap.Common.FeatureFlags.IFeatureFlags>();

    private ActionExecutor NewSut() => new(
        _templates, _renderer, _deliveries, _dispatcher, _contacts,
        _invoices, _rules, _runs, _fees, _attachments, _flags,
        NullLogger<ActionExecutor>.Instance);

    private static Lease NewLease() => new()
    {
        Id = 42, PropertyManagerId = 7, StateId = 5, MonthlyRent = 2000m,
        StartDate = new DateOnly(2024, 1, 1)
    };

    private static PlannedAction AssessLateFeeAction() =>
        new(Kind: ActionKind.AssessLateFee, NoticeTypeCode: "assess_late_fee", FeeAmount: 100m);

    private static ResolvedRule SampleRule() => new(
        StateCode: "CA", StateRuleVersionId: 17, LocalRuleOverrideId: null,
        AssessmentDate: Today, GracePeriodDays: 5,
        LateFeeType: LateFeeType.Percent, FlatAmount: null,
        PercentOfRent: 0.05m, HardCapAmount: null,
        NoticeRequiredBeforeFee: false, NoticeAdvanceDays: null,
        SourceCitation: "CA Civ Code §1671",
        PlainSummary: "California: late fee must be reasonable.",
        SourceUrl: "https://example.gov/ca-1671");

    // ----------------------------------------------------------------------
    // A Live fee row always carries a resolvable rule version + disclosure.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Live_late_fee_is_stamped_with_rule_version_and_disclosure()
    {
        var lease = NewLease();

        _rules.ResolveRuleByStateIdAsync(lease.StateId, Today, lease.JurisdictionId, Arg.Any<CancellationToken>())
            .Returns(SampleRule());

        LateFeeAssessment? captured = null;
        _fees.RecordAsync(Arg.Do<LateFeeAssessment>(a => captured = a), Arg.Any<CancellationToken>())
            .Returns(ci => { var a = ci.Arg<LateFeeAssessment>(); a.Id = 1; return a; });
        _runs.TryRecordAsync(Arg.Any<AssessmentRun>(), Arg.Any<CancellationToken>())
            .Returns(new AssessmentRun { Id = 1, LeaseId = lease.Id });

        var result = await NewSut().ExecuteAsync(
            lease, DueDate, Today, AssessLateFeeAction(), ExecutionMode.Live, CancellationToken.None);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.NotNull(captured);

        // Resolvable rule version (the prompt's core guarantee).
        Assert.True(captured!.StateRuleVersionId > 0);
        Assert.Equal(17, captured.StateRuleVersionId);

        // Disclosure snapshot present + carries the rule version and citation,
        // so the row is provable after the rule is later versioned.
        Assert.False(string.IsNullOrWhiteSpace(captured.DisclosureSnapshot));
        Assert.Contains("\"StateRuleVersionId\":17", captured.DisclosureSnapshot, StringComparison.Ordinal);
        Assert.Contains("CA Civ Code", captured.DisclosureSnapshot, StringComparison.Ordinal);
        Assert.Contains("100", captured.DisclosureSnapshot, StringComparison.Ordinal);   // FeeAssessed
    }

    // ----------------------------------------------------------------------
    // No rule on file → no fee row is ever written (can't stamp evidence).
    // ----------------------------------------------------------------------

    [Fact]
    public async Task No_rule_means_no_fee_row_is_written()
    {
        var lease = NewLease();
        _rules.ResolveRuleByStateIdAsync(lease.StateId, Today, lease.JurisdictionId, Arg.Any<CancellationToken>())
            .Returns((ResolvedRule?)null);

        var result = await NewSut().ExecuteAsync(
            lease, DueDate, Today, AssessLateFeeAction(), ExecutionMode.Live, CancellationToken.None);

        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        await _fees.DidNotReceive().RecordAsync(Arg.Any<LateFeeAssessment>(), Arg.Any<CancellationToken>());
    }
}
