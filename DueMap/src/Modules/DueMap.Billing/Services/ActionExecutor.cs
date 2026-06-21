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
        ExecutionMode mode = ExecutionMode.Live,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(action);

        // Idempotency check is a Live-mode concern — DryRun deliberately shows
        // "what would happen if not yet done" so the reviewer sees the day's
        // intent against a clean slate.
        if (mode == ExecutionMode.Live
            && await _runs.HasRunAsync(lease.Id, currentDueDate, action.Kind, ct))
        {
            return new ActionExecutionResult(ActionOutcome.SkippedAlreadyDone, null);
        }

        return action.Kind switch
        {
            ActionKind.AssessLateFee => await ExecuteLateFeeAsync(lease, currentDueDate, businessDate, action, mode, ct),
            _                        => await ExecuteSendAsync(lease, currentDueDate, businessDate, action, mode, ct)
        };
    }

    private async Task<ActionExecutionResult> ExecuteLateFeeAsync(
        Lease lease, DateOnly currentDueDate, DateOnly businessDate, PlannedAction action,
        ExecutionMode mode, CancellationToken ct)
    {
        // Resolve the rule again here to capture provenance (version + override ids)
        // for the assessment row. The Rules cache makes this a memory hit.
        // Read-only — safe to do in DryRun too.
        var rule = await _rules.ResolveRuleByStateIdAsync(lease.StateId, businessDate, lease.JurisdictionId, ct);
        if (rule is null)
        {
            return new ActionExecutionResult(ActionOutcome.Failed,
                $"No active state rule for lease {lease.Id} on {businessDate:yyyy-MM-dd}");
        }

        var feeAmount = action.FeeAmount ?? rule.ComputeFee(lease.MonthlyRent);

        // ---- DryRun: build a preview row, no writes -----------------------
        if (mode == ExecutionMode.DryRun)
        {
            var preview = new PlannedActionPreview(
                LeaseId:            lease.Id,
                TenantDisplayName:  null,                                 // not resolved on fee path
                ActionKindCode:     "assess_late_fee",
                Channel:            "",
                ToAddress:          null,
                Amount:             feeAmount,
                DueDate:            currentDueDate,
                Provenance:         $"state rule v={rule.StateRuleVersionId}"
                                    + (rule.LocalRuleOverrideId is int oid ? $" override={oid}" : ""),
                RenderedSubject:    null,
                RenderedFirstLine:  null);
            return new ActionExecutionResult(ActionOutcome.Executed, $"fee={feeAmount.ToString(CultureInfo.InvariantCulture)}", preview);
        }

        // ---- Live: write the assessment + run row -------------------------
        // P1-6 §6.2: stamp a disclosure snapshot alongside the rule version so
        // the row proves WHICH terms produced the fee, provable even after the
        // rule is later versioned.
        var disclosure = LateFeeDisclosure.From(rule, lease.MonthlyRent, feeAmount).ToJson();

        var fee = await _fees.RecordAsync(new LateFeeAssessment
        {
            LeaseId = lease.Id,
            DueDate = currentDueDate,
            AssessmentDate = businessDate,
            FeeAmount = feeAmount,
            MonthlyRentSnapshot = lease.MonthlyRent,
            StateRuleVersionId = rule.StateRuleVersionId,
            LocalRuleOverrideId = rule.LocalRuleOverrideId,
            DisclosureSnapshot = disclosure,
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
        Lease lease, DateOnly currentDueDate, DateOnly businessDate, PlannedAction action,
        ExecutionMode mode, CancellationToken ct)
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

        // ---- DryRun: build a preview row, no dispatch, no writes ----------
        if (mode == ExecutionMode.DryRun)
        {
            var firstLine = FirstLine(rendered.BodyText);
            var preview = new PlannedActionPreview(
                LeaseId:            lease.Id,
                TenantDisplayName:  contact.DisplayName,
                ActionKindCode:     action.NoticeTypeCode,
                Channel:            channel.ToString(),
                ToAddress:          to,
                Amount:             null,
                DueDate:            currentDueDate,
                Provenance:         $"template v={renderable.SystemTemplateVersionId}"
                                    + (renderable.PmTemplateOverrideId is int oid ? $" override={oid}" : ""),
                RenderedSubject:    rendered.Subject,
                RenderedFirstLine:  firstLine);
            return new ActionExecutionResult(ActionOutcome.Executed, $"preview channel={channel}", preview);
        }

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

    /// <summary>
    /// Short excerpt of a rendered plain-text body for the DryRun preview.
    /// We want enough to verify the body is sane in the admin view without
    /// dumping the whole 2 KB notice into JSON. ~140 chars matches an SMS
    /// for visual parity with that channel.
    /// </summary>
    private static string FirstLine(string body)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;
        var firstBreak = body.IndexOf('\n');
        var line = firstBreak < 0 ? body : body[..firstBreak];
        line = line.Trim();
        return line.Length > 140 ? line[..140] + "…" : line;
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
