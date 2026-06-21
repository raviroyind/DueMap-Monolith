using DueMap.Billing.Domain;
using DueMap.Billing.Services;
using DueMap.Integrations.Notices;
using DueMap.Notices;
using DueMap.Notices.Domain;
using DueMap.Rules;
using DueMap.Rules.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P0-2 acceptance tests: <see cref="ExecutionMode.DryRun"/> produces a
/// preview row for every action the planner would have run AND writes
/// nothing. The "zero writes" guarantee is asserted via NSubstitute —
/// every repository / dispatcher call site is verified DidNotReceive.
///
/// A third "Live still works" test guards against a future refactor
/// accidentally short-circuiting the Live path while making DryRun
/// changes — the same set of side-effects we just asserted *don't*
/// happen in DryRun must still happen in Live.
/// </summary>
public sealed class ActionExecutorDryRunTests
{
    private static readonly DateOnly Today    = new(2026, 6, 15);
    private static readonly DateOnly DueDate  = new(2026, 6, 1);

    private readonly INoticeTemplateService              _templates  = Substitute.For<INoticeTemplateService>();
    private readonly INoticeRenderer                     _renderer   = Substitute.For<INoticeRenderer>();
    private readonly INoticeDeliveryRepository           _deliveries = Substitute.For<INoticeDeliveryRepository>();
    private readonly INoticeDispatcher                   _dispatcher = Substitute.For<INoticeDispatcher>();
    private readonly ITenantContactResolver              _contacts   = Substitute.For<ITenantContactResolver>();
    private readonly IRentInvoiceRepository              _invoices   = Substitute.For<IRentInvoiceRepository>();
    private readonly IRulesService                       _rules      = Substitute.For<IRulesService>();
    private readonly IAssessmentRunRepository            _runs       = Substitute.For<IAssessmentRunRepository>();
    private readonly ILateFeeAssessmentRepository        _fees       = Substitute.For<ILateFeeAssessmentRepository>();
    private readonly ILateFeeInvoiceAttachmentFetcher    _attachments = Substitute.For<ILateFeeInvoiceAttachmentFetcher>();
    private readonly DueMap.Common.FeatureFlags.IFeatureFlags _flags = Substitute.For<DueMap.Common.FeatureFlags.IFeatureFlags>();

    private ActionExecutor NewSut() => new(
        _templates, _renderer, _deliveries, _dispatcher, _contacts,
        _invoices, _rules, _runs, _fees, _attachments, _flags,
        NullLogger<ActionExecutor>.Instance);

    private static Lease NewLease() => new()
    {
        Id = 42,
        PropertyManagerId = 7,
        StateId = 5,
        MonthlyRent = 2000m,
        StartDate = new DateOnly(2024, 1, 1)
    };

    private static PlannedAction PreDueAction() => new(
        Kind: ActionKind.SendPreDueReminder,
        NoticeTypeCode: "pre_due_reminder",
        FeeAmount: null);

    private static PlannedAction AssessLateFeeAction() => new(
        Kind: ActionKind.AssessLateFee,
        NoticeTypeCode: "assess_late_fee",
        FeeAmount: 100m);

    private static ResolvedRule SampleRule() => new(
        StateCode: "CA", StateRuleVersionId: 17, LocalRuleOverrideId: null,
        AssessmentDate: Today, GracePeriodDays: 5,
        LateFeeType: LateFeeType.Percent, FlatAmount: null,
        PercentOfRent: 0.05m, HardCapAmount: null,
        NoticeRequiredBeforeFee: false, NoticeAdvanceDays: null,
        SourceCitation: "CA Civ Code §1671");

    // ------------------------------------------------------------------
    // DryRun for a notice send (pre-due reminder)
    // ------------------------------------------------------------------

    [Fact]
    public async Task DryRun_pre_due_reminder_produces_preview_and_writes_nothing()
    {
        var lease = NewLease();
        var action = PreDueAction();

        // Read-side mocks — DryRun is allowed to call these (they don't write).
        _contacts.ResolveAsync(lease.Id, Arg.Any<CancellationToken>())
            .Returns(new TenantContact(lease.Id, "jane@example.com", null, "Jane Smith"));

        _templates.ResolveRenderableAsync(
                lease.PropertyManagerId, lease.StateId, action.NoticeTypeCode, Today, Arg.Any<CancellationToken>())
            .Returns(new RenderableTemplate(
                SystemTemplateVersionId: 99,
                PmTemplateOverrideId: null,
                Subject: "Rent reminder",
                BodyHtml: "<p>html body</p>",
                BodyText: "Hi Jane, this is line 1.\nAnd this is line 2.",
                RequiredVars: "[\"tenant_name\"]"));

        _renderer.RenderAsync(Arg.Any<NoticeTemplateVersion>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotice(
                TemplateVersionId: 99,
                Subject: "Rent reminder",
                BodyHtml: "<p>html body</p>",
                BodyText: "Hi Jane, this is line 1.\nAnd this is line 2."));

        _invoices.GetCurrentPayUrlAsync(lease.Id, Today, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // ---- ACT ----
        var sut = NewSut();
        var result = await sut.ExecuteAsync(lease, DueDate, Today, action, ExecutionMode.DryRun, CancellationToken.None);

        // ---- ASSERT preview shape ----
        Assert.NotNull(result.Preview);
        Assert.Equal(lease.Id, result.Preview!.LeaseId);
        Assert.Equal("Jane Smith", result.Preview.TenantDisplayName);
        Assert.Equal("pre_due_reminder", result.Preview.ActionKindCode);
        Assert.Equal("Email", result.Preview.Channel);
        Assert.Equal("jane@example.com", result.Preview.ToAddress);
        Assert.Equal(DueDate, result.Preview.DueDate);
        Assert.Equal("Rent reminder", result.Preview.RenderedSubject);
        Assert.Equal("Hi Jane, this is line 1.", result.Preview.RenderedFirstLine);
        Assert.Contains("template v=99", result.Preview.Provenance);
        Assert.Null(result.Preview.Amount);

        // ---- ASSERT zero writes / zero dispatch ----
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<DispatchRequest>(), Arg.Any<CancellationToken>());
        await _deliveries.DidNotReceive().RecordAsync(Arg.Any<NoticeDelivery>(), Arg.Any<CancellationToken>());
        await _runs.DidNotReceive().TryRecordAsync(Arg.Any<AssessmentRun>(), Arg.Any<CancellationToken>());
        // DryRun also skips the PDF fetch (external call) — it's irrelevant for preview.
        await _attachments.DidNotReceive().TryFetchForCurrentPeriodAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        // Idempotency check (HasRunAsync) is also skipped in DryRun so the
        // preview reflects "as if from a clean slate."
        await _runs.DidNotReceive().HasRunAsync(
            Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<ActionKind>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // DryRun for a late-fee assessment
    // ------------------------------------------------------------------

    [Fact]
    public async Task DryRun_assess_late_fee_produces_preview_with_amount_and_writes_nothing()
    {
        var lease = NewLease();
        var action = AssessLateFeeAction();

        _rules.ResolveRuleByStateIdAsync(lease.StateId, Today, lease.JurisdictionId, Arg.Any<CancellationToken>())
            .Returns(SampleRule());

        // ---- ACT ----
        var sut = NewSut();
        var result = await sut.ExecuteAsync(lease, DueDate, Today, action, ExecutionMode.DryRun, CancellationToken.None);

        // ---- ASSERT preview shape ----
        Assert.NotNull(result.Preview);
        Assert.Equal(lease.Id, result.Preview!.LeaseId);
        Assert.Equal("assess_late_fee", result.Preview.ActionKindCode);
        Assert.Equal("", result.Preview.Channel);            // no dispatch on fee path
        Assert.Null(result.Preview.ToAddress);
        Assert.Equal(100m, result.Preview.Amount);           // pre-set on the PlannedAction
        Assert.Null(result.Preview.RenderedSubject);
        Assert.Null(result.Preview.RenderedFirstLine);
        Assert.Contains("state rule v=17", result.Preview.Provenance);

        // ---- ASSERT zero writes ----
        await _fees.DidNotReceive().RecordAsync(Arg.Any<LateFeeAssessment>(), Arg.Any<CancellationToken>());
        await _runs.DidNotReceive().TryRecordAsync(Arg.Any<AssessmentRun>(), Arg.Any<CancellationToken>());
        await _runs.DidNotReceive().HasRunAsync(
            Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<ActionKind>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Regression guard: Live mode still calls the write paths.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Live_assess_late_fee_still_records_fee_and_run()
    {
        var lease = NewLease();
        var action = AssessLateFeeAction();

        _rules.ResolveRuleByStateIdAsync(lease.StateId, Today, lease.JurisdictionId, Arg.Any<CancellationToken>())
            .Returns(SampleRule());

        // Idempotency check returns false (no run yet) so the Live path proceeds.
        _runs.HasRunAsync(lease.Id, DueDate, action.Kind, Arg.Any<CancellationToken>())
            .Returns(false);

        var recordedFee = new LateFeeAssessment
        {
            Id = 1, LeaseId = lease.Id, DueDate = DueDate, AssessmentDate = Today, FeeAmount = 100m
        };
        _fees.RecordAsync(Arg.Any<LateFeeAssessment>(), Arg.Any<CancellationToken>()).Returns(recordedFee);

        _runs.TryRecordAsync(Arg.Any<AssessmentRun>(), Arg.Any<CancellationToken>())
            .Returns(new AssessmentRun { Id = 1, LeaseId = lease.Id });

        // ---- ACT ----
        var sut = NewSut();
        var result = await sut.ExecuteAsync(lease, DueDate, Today, action, ExecutionMode.Live, CancellationToken.None);

        // ---- ASSERT writes happened, no preview ----
        Assert.Null(result.Preview);
        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        await _fees.Received(1).RecordAsync(Arg.Any<LateFeeAssessment>(), Arg.Any<CancellationToken>());
        await _runs.Received(1).TryRecordAsync(Arg.Any<AssessmentRun>(), Arg.Any<CancellationToken>());
    }
}
