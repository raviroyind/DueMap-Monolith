using System.Globalization;
using DueMap.Billing.Domain;
using DueMap.Integrations.Notices;
using DueMap.Notices;
using DueMap.Notices.Domain;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;
using Microsoft.Extensions.Logging;

namespace DueMap.Billing.Services;

internal sealed partial class ActionExecutor : IActionExecutor
{
    /// <summary>
    /// Notice-type code that gets the QBO/Xero invoice PDF attached. Kept as
    /// a constant so changes propagate via compile error rather than a stale
    /// string literal in the dispatch path.
    /// </summary>
    private const string LateFeeNoticeCode = "late_fee_notice";

    private readonly INoticeTemplateService _templates;
    private readonly INoticeRenderer _renderer;
    private readonly INoticeDeliveryRepository _deliveries;
    private readonly INoticeDispatcher _dispatcher;
    private readonly ITenantContactResolver _contacts;
    private readonly IRentInvoiceRepository _invoices;
    private readonly IRulesService _rules;
    private readonly IAssessmentRunRepository _runs;
    private readonly ILateFeeAssessmentRepository _fees;
    private readonly ILateFeeInvoiceAttachmentFetcher _attachments;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(
        INoticeTemplateService templates,
        INoticeRenderer renderer,
        INoticeDeliveryRepository deliveries,
        INoticeDispatcher dispatcher,
        ITenantContactResolver contacts,
        IRentInvoiceRepository invoices,
        IRulesService rules,
        IAssessmentRunRepository runs,
        ILateFeeAssessmentRepository fees,
        ILateFeeInvoiceAttachmentFetcher attachments,
        ILogger<ActionExecutor> logger)
    {
        _templates = templates;
        _renderer = renderer;
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _contacts = contacts;
        _invoices = invoices;
        _rules = rules;
        _runs = runs;
        _fees = fees;
        _attachments = attachments;
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(
        Lease lease,
        DateOnly currentDueDate,
        DateOnly businessDate,
        PlannedAction action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(action);

        if (await _runs.HasRunAsync(lease.Id, currentDueDate, action.Kind, ct))
        {
            return new ActionExecutionResult(ActionOutcome.SkippedAlreadyDone, null);
        }

        return action.Kind switch
        {
            ActionKind.AssessLateFee => await ExecuteLateFeeAsync(lease, currentDueDate, businessDate, action, ct),
            _                        => await ExecuteSendAsync(lease, currentDueDate, businessDate, action, ct)
        };
    }

    private async Task<ActionExecutionResult> ExecuteLateFeeAsync(
        Lease lease, DateOnly currentDueDate, DateOnly businessDate, PlannedAction action, CancellationToken ct)
    {
        // Resolve the rule again here to capture provenance (version + override ids)
        // for the assessment row. The Rules cache makes this a memory hit.
        var rule = await _rules.ResolveRuleByStateIdAsync(lease.StateId, businessDate, lease.JurisdictionId, ct);
        if (rule is null)
        {
            return new ActionExecutionResult(ActionOutcome.Failed,
                $"No active state rule for lease {lease.Id} on {businessDate:yyyy-MM-dd}");
        }

        var fee = await _fees.RecordAsync(new LateFeeAssessment
        {
            LeaseId = lease.Id,
            DueDate = currentDueDate,
            AssessmentDate = businessDate,
            FeeAmount = action.FeeAmount ?? rule.ComputeFee(lease.MonthlyRent),
            MonthlyRentSnapshot = lease.MonthlyRent,
            StateRuleVersionId = rule.StateRuleVersionId,
            LocalRuleOverrideId = rule.LocalRuleOverrideId,
            Status = LateFeeAssessmentStatus.Assessed
        }, ct);

        var run = await _runs.TryRecordAsync(new AssessmentRun
        {
            LeaseId = lease.Id,
            DueDate = currentDueDate,
            AssessmentDate = businessDate,
            ActionKind = ActionKind.AssessLateFee,
            LateFeeAssessmentId = fee.Id
        }, ct);

        return run is null
            ? new ActionExecutionResult(ActionOutcome.SkippedAlreadyDone, "race: run row already exists")
            : new ActionExecutionResult(ActionOutcome.Executed, $"fee={fee.FeeAmount.ToString(CultureInfo.InvariantCulture)}");
    }

    private async Task<ActionExecutionResult> ExecuteSendAsync(
        Lease lease, DateOnly currentDueDate, DateOnly businessDate, PlannedAction action, CancellationToken ct)
    {
        var contact = await _contacts.ResolveAsync(lease.Id, ct);
        if (contact is null || (string.IsNullOrWhiteSpace(contact.Email) && string.IsNullOrWhiteSpace(contact.Phone)))
        {
            // Deliberately DON'T record the assessment_run — let the action retry
            // once accounting sync populates customer contact info.
            LogNoContact(_logger, lease.Id, action.NoticeTypeCode);
            return new ActionExecutionResult(ActionOutcome.SkippedNoContact,
                "Tenant contact not yet synced; will retry on a future tick.");
        }

        var renderable = await _templates.ResolveRenderableAsync(
            lease.PropertyManagerId, lease.StateId, action.NoticeTypeCode, businessDate, ct);
        if (renderable is null)
        {
            return new ActionExecutionResult(ActionOutcome.Failed,
                $"No template for type={action.NoticeTypeCode} state={lease.StateId}");
        }

        // Pay-by-link URL travels with the synced invoice. Null is fine — the
        // template is expected to wrap the Pay Now button in {% if pay_url %}.
        var payUrl = await _invoices.GetCurrentPayUrlAsync(lease.Id, businessDate, ct);

        var variables = BuildVariables(lease, contact, currentDueDate, payUrl);

        // Render the merged subject/body. We construct a synthetic
        // NoticeTemplateVersion carrying the renderable fields so the
        // existing renderer contract works unchanged.
        var synthetic = new NoticeTemplateVersion
        {
            Id = renderable.SystemTemplateVersionId,
            Subject = renderable.Subject,
            BodyHtml = renderable.BodyHtml,
            BodyText = renderable.BodyText,
            RequiredVars = renderable.RequiredVars
        };

        RenderedNotice rendered;
        try
        {
            rendered = await _renderer.RenderAsync(synthetic, variables, ct);
        }
        catch (MissingTemplateVariableException ex)
        {
            return new ActionExecutionResult(ActionOutcome.Failed, ex.Message);
        }

        var (channel, to) = ChooseChannel(contact);

        // Late-fee notices get the QBO/Xero-rendered invoice PDF attached so
        // the tenant has the authoritative document with the updated balance.
        // Fetcher returns null on any failure (no connection, no invoice,
        // provider 404, transient error) — we still send the email.
        IReadOnlyList<DispatchAttachment>? attachments = null;
        if (channel == DispatchChannel.Email
            && string.Equals(action.NoticeTypeCode, LateFeeNoticeCode, StringComparison.OrdinalIgnoreCase))
        {
            var pdf = await _attachments.TryFetchForCurrentPeriodAsync(
                lease.PropertyManagerId, lease.Id, currentDueDate, ct);
            if (pdf is not null) attachments = new[] { pdf };
        }

        var dispatchResult = await _dispatcher.DispatchAsync(new DispatchRequest(
            Channel: channel,
            To: to,
            ToDisplayName: contact.DisplayName,
            Subject: rendered.Subject,
            BodyHtml: rendered.BodyHtml,
            BodyText: rendered.BodyText,
            Attachments: attachments), ct);

        var delivery = await _deliveries.RecordAsync(new NoticeDelivery
        {
            PropertyManagerId = lease.PropertyManagerId,
            LeaseId = lease.Id,
            TemplateVersionId = renderable.SystemTemplateVersionId,
            PmTemplateOverrideId = renderable.PmTemplateOverrideId,
            RenderedSubject = rendered.Subject,
            RenderedBodyHtml = rendered.BodyHtml,
            RenderedBodyText = rendered.BodyText,
            Channel = channel == DispatchChannel.Email ? NoticeChannel.Email : NoticeChannel.Sms,
            SentAt = DateTime.UtcNow,
            ProviderMsgId = dispatchResult.ProviderMessageId,
            Status = dispatchResult.Status == DispatchStatus.Queued ? DeliveryStatus.Sent : DeliveryStatus.Failed
        }, ct);

        var run = await _runs.TryRecordAsync(new AssessmentRun
        {
            LeaseId = lease.Id,
            DueDate = currentDueDate,
            AssessmentDate = businessDate,
            ActionKind = action.Kind,
            NoticeDeliveryId = delivery.Id
        }, ct);

        if (dispatchResult.Status == DispatchStatus.Failed)
        {
            return new ActionExecutionResult(ActionOutcome.Failed,
                dispatchResult.FailureReason ?? "Dispatcher failed");
        }

        return run is null
            ? new ActionExecutionResult(ActionOutcome.SkippedAlreadyDone, "race: run row already exists")
            : new ActionExecutionResult(ActionOutcome.Executed,
                $"channel={channel} provider_id={dispatchResult.ProviderMessageId}");
    }

    private static (DispatchChannel Channel, string To) ChooseChannel(TenantContact contact) =>
        !string.IsNullOrWhiteSpace(contact.Email)
            ? (DispatchChannel.Email, contact.Email)
            : (DispatchChannel.Sms,   contact.Phone!);

    private static Dictionary<string, object?> BuildVariables(
        Lease lease, TenantContact contact, DateOnly currentDueDate, string? payUrl) =>
        new()
        {
            ["tenant_name"]  = contact.DisplayName ?? "Tenant",
            ["amount_owed"]  = lease.MonthlyRent,
            ["monthly_rent"] = lease.MonthlyRent,
            ["due_date"]     = currentDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["lease_id"]     = lease.Id,
            ["pay_url"]      = payUrl
        };

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Skipped {NoticeTypeCode} for lease {LeaseId}: no tenant contact synced yet")]
    static partial void LogNoContact(ILogger logger, int leaseId, string noticeTypeCode);
}
