-- =============================================================================
-- DueMap schema v14 — refresh system notice templates (v2 copy)
--
-- New copy across all four notice types:
--   * Pay Now button rendered when {{ pay_url }} is supplied
--     (synced from QBO InvoiceLink / Xero OnlineInvoiceUrl).
--   * Removes "contact your property manager" / reply-to language — the
--     emails are transactional and the From address is the platform.
--   * Adds an "if you've already paid, please disregard" line so we don't
--     spook tenants whose payment is in flight.
--   * Late-fee notice references the attached invoice PDF that the
--     dispatcher will now include with this notice type only.
--
-- Implementation: for each generic system template, insert a NEW
-- notice_template_versions row with version_number=2 status=approved.
-- The old v1 row is left in history (it's an append-only versioning table).
-- INoticeTemplateService.ResolveRenderableAsync picks the highest-version
-- approved row, so the new copy goes live automatically the next time the
-- worker dispatches.
--
-- Idempotent: skips a template that already has a version_number >= 2.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @effective DATE = '2026-01-01';

;WITH t AS (
    SELECT tpl.id AS template_id, nt.code
    FROM notices.notice_templates tpl
    JOIN notices.notice_types nt ON nt.id = tpl.notice_type_id
    WHERE tpl.state_id IS NULL AND tpl.is_active = 1
)
INSERT INTO notices.notice_template_versions (
    template_id, version_number, subject, body_html, body_text, required_vars,
    effective_date, approved_by, status)
SELECT
    t.template_id,
    2,
    s.subject, s.body_html, s.body_text,
    N'["tenant_name","amount_owed","due_date","monthly_rent","lease_id"]',
    @effective, N'system', 'approved'
FROM t
JOIN (VALUES
    -- =====================================================================
    -- Pre-due reminder
    -- =====================================================================
    ('pre_due_reminder',
        N'Rent reminder: payment due on {{ due_date }}',
        N'<p>Hi {{ tenant_name }},</p>
          <p>This is a friendly reminder that your rent of <strong>{{ amount_owed }}</strong> is due on <strong>{{ due_date }}</strong>.</p>
          {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, this is a reminder that your rent of {{ amount_owed }} is due on {{ due_date }}.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.'),

    -- =====================================================================
    -- Due-date reminder
    -- =====================================================================
    ('due_date_reminder',
        N'Rent is due today',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> is due <strong>today ({{ due_date }})</strong>. Please submit payment before the end of the day to avoid a late fee.</p>
          {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, your rent of {{ amount_owed }} is due today, {{ due_date }}.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.'),

    -- =====================================================================
    -- Grace-period reminder
    -- =====================================================================
    ('grace_period_reminder',
        N'Rent payment past due',
        N'<p>Hi {{ tenant_name }},</p>
          <p>We haven''t received your rent payment of <strong>{{ amount_owed }}</strong>, originally due on {{ due_date }}. You''re still within the grace period — please submit payment promptly to avoid a late fee.</p>
          {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, we haven''t received your rent payment of {{ amount_owed }} (due {{ due_date }}). Please pay promptly to avoid a late fee.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.'),

    -- =====================================================================
    -- Late-fee notice  (also gets a PDF invoice attachment via dispatcher)
    -- =====================================================================
    ('late_fee_notice',
        N'Late fee assessed on overdue rent',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, originally due on {{ due_date }}.</p>
          <p>A detailed invoice with the updated balance is attached to this email.</p>
          {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, a late fee has been applied to your overdue rent of {{ amount_owed }} (originally due {{ due_date }}). A detailed invoice with the updated balance is attached.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.')
) s (code, subject, body_html, body_text) ON s.code = t.code
WHERE NOT EXISTS (
    -- Idempotent: only insert v2 if it (or anything higher) doesn't already exist.
    SELECT 1 FROM notices.notice_template_versions existing
    WHERE existing.template_id = t.template_id
      AND existing.version_number >= 2
);
GO
