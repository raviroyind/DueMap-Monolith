using DueMap.Billing.Domain;
using DueMap.Billing.Services;
using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Notices;
using DueMap.Notices;
using DueMap.Notices.Domain;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DueMap.Billing.Tests;

/// <summary>
/// P2-4 autopay-aware reminders (billing.autopay_aware). Verified by capturing
/// the variables handed to the renderer: enrolled tenants get a heads-up flag,
/// non-enrolled get the "Set up autopay" link — across both channels — and the
/// flag off leaves reminders untouched.
/// </summary>
public sealed class ActionExecutorAutopayTests
{
    private static readonly DateOnly Today   = new(2026, 6, 15);
    private static readonly DateOnly DueDate = new(2026, 6, 1);
    private const string PayUrl = "https://pay.example.com/inv/42";

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
    private readonly IFeatureFlags                    _flags       = Substitute.For<IFeatureFlags>();

    private ActionExecutor NewSut() => new(
        _templates, _renderer, _deliveries, _dispatcher, _contacts,
        _invoices, _rules, _runs, _fees, _attachments, _flags,
        NullLogger<ActionExecutor>.Instance);

    private static Lease Lease(AutopayStatus status) => new()
    {
        Id = 42, PropertyManagerId = 7, StateId = 5, MonthlyRent = 2000m,
        StartDate = new DateOnly(2024, 1, 1), AutopayStatus = status
    };

    private static PlannedAction PreDue() => new(ActionKind.SendPreDueReminder, "pre_due_reminder", null);

    // Wire the read-side mocks, run the action, and return the captured variables.
    private async Task<IReadOnlyDictionary<string, object?>> RunAndCaptureAsync(
        bool autopayAware, AutopayStatus status, string? email, string? phone)
    {
        IReadOnlyDictionary<string, object?>? captured = null;
        _flags.IsEnabledAsync("billing.autopay_aware", 7, Arg.Any<CancellationToken>()).Returns(autopayAware);
        _contacts.ResolveAsync(42, Arg.Any<CancellationToken>())
            .Returns(new TenantContact(42, email, phone, "Jane"));
        _templates.ResolveRenderableAsync(7, 5, "pre_due_reminder", Today, Arg.Any<CancellationToken>())
            .Returns(new RenderableTemplate(99, null, "Subj", "<p>h</p>", "text", "[]"));
        _invoices.GetCurrentPayUrlAsync(42, Today, Arg.Any<CancellationToken>()).Returns(PayUrl);
        _renderer.RenderAsync(Arg.Any<NoticeTemplateVersion>(),
                Arg.Do<IReadOnlyDictionary<string, object?>>(v => captured = v), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotice(99, "Subj", "<p>h</p>", "text"));

        await NewSut().ExecuteAsync(Lease(status), DueDate, Today, PreDue(), ExecutionMode.DryRun, CancellationToken.None);
        Assert.NotNull(captured);
        return captured!;
    }

    [Fact]
    public async Task Enrolled_gets_headsup_flag_not_a_setup_link()
    {
        var vars = await RunAndCaptureAsync(autopayAware: true, AutopayStatus.Enrolled, email: "jane@x.com", phone: null);
        Assert.Equal(true, vars["autopay_enrolled"]);
        Assert.Null(vars["autopay_setup_url"]);
    }

    [Fact]
    public async Task Not_enrolled_gets_setup_link_email_channel()
    {
        var vars = await RunAndCaptureAsync(autopayAware: true, AutopayStatus.None, email: "jane@x.com", phone: null);
        Assert.Equal(false, vars["autopay_enrolled"]);
        Assert.Equal(PayUrl, vars["autopay_setup_url"]);
    }

    [Fact]
    public async Task Not_enrolled_gets_setup_link_sms_channel()
    {
        // Phone-only contact => SMS channel; autopay link is channel-independent.
        var vars = await RunAndCaptureAsync(autopayAware: true, AutopayStatus.None, email: null, phone: "+14155551234");
        Assert.Equal(PayUrl, vars["autopay_setup_url"]);
    }

    [Fact]
    public async Task Flag_off_leaves_reminders_untouched()
    {
        var vars = await RunAndCaptureAsync(autopayAware: false, AutopayStatus.Enrolled, email: "jane@x.com", phone: null);
        Assert.Equal(false, vars["autopay_enrolled"]);
        Assert.Null(vars["autopay_setup_url"]);
    }
}
