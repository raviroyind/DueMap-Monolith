-- =============================================================================
-- DueMap schema v25 — autopay-aware reminder copy (P2-4 follow-up)
--
-- P2-4 made the ActionExecutor pass two variables on reminder sends:
--   * autopay_enrolled  (bool) — tenant is on autopay / reliably pays
--   * autopay_setup_url  (str) — the hosted "set up autopay" page (= pay_url)
-- ...but no template referenced them, so the behavior was invisible. This adds
-- version 3 of the three REMINDER templates with conditional blocks:
--   * enrolled  -> a soft "you're on autopay, no action needed" heads-up
--   * not enrolled (url present) -> a "Set up autopay" button
-- The late-fee notice is intentionally excluded (not a reminder).
--
-- Versioning: insert version_number=3 (approved). ResolveRenderableAsync picks
-- the highest approved version, so the copy goes live on the next dispatch.
-- required_vars is unchanged — the autopay vars are optional ({% if %} guards
-- them and the executor always supplies the keys), so rendering never fails
-- when the autopay flag is off.
--
-- Idempotent: skips templates that already have version_number >= 3.
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
      AND nt.code IN ('pre_due_reminder', 'due_date_reminder', 'grace_period_reminder')
)
INSERT INTO notices.notice_template_versions (
    template_id, version_number, subject, body_html, body_text, required_vars,
    effective_date, approved_by, status)
SELECT
    t.template_id,
    3,
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
          {% if autopay_enrolled %}<p style="color:#16a34a;font-size:13px;margin-top:8px;">You''re set up on autopay — no action needed. This is just a heads-up.</p>{% endif %}
          {% if autopay_setup_url %}<p style="margin:16px 0;"><a href="{{ autopay_setup_url }}" style="display:inline-block;padding:10px 22px;background:#0ea5e9;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Set up autopay</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, this is a reminder that your rent of {{ amount_owed }} is due on {{ due_date }}.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}{% if autopay_enrolled %} You''re on autopay — no action needed.{% endif %}{% if autopay_setup_url %} Set up autopay: {{ autopay_setup_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.'),

    -- =====================================================================
    -- Due-date reminder
    -- =====================================================================
    ('due_date_reminder',
        N'Rent is due today',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> is due <strong>today ({{ due_date }})</strong>. Please submit payment before the end of the day to avoid a late fee.</p>
          {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
          {% if autopay_enrolled %}<p style="color:#16a34a;font-size:13px;margin-top:8px;">You''re set up on autopay — no action needed. This is just a heads-up.</p>{% endif %}
          {% if autopay_setup_url %}<p style="margin:16px 0;"><a href="{{ autopay_setup_url }}" style="display:inline-block;padding:10px 22px;background:#0ea5e9;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Set up autopay</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, your rent of {{ amount_owed }} is due today, {{ due_date }}.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}{% if autopay_enrolled %} You''re on autopay — no action needed.{% endif %}{% if autopay_setup_url %} Set up autopay: {{ autopay_setup_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.'),

    -- =====================================================================
    -- Grace-period reminder
    -- =====================================================================
    ('grace_period_reminder',
        N'Rent payment past due',
        N'<p>Hi {{ tenant_name }},</p>
          <p>We haven''t received your rent payment of <strong>{{ amount_owed }}</strong>, originally due on {{ due_date }}. You''re still within the grace period — please submit payment promptly to avoid a late fee.</p>
          {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
          {% if autopay_enrolled %}<p style="color:#16a34a;font-size:13px;margin-top:8px;">You''re set up on autopay — the payment should process automatically.</p>{% endif %}
          {% if autopay_setup_url %}<p style="margin:16px 0;"><a href="{{ autopay_setup_url }}" style="display:inline-block;padding:10px 22px;background:#0ea5e9;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Set up autopay</a></p>{% endif %}
          <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
        N'Hi {{ tenant_name }}, we haven''t received your rent payment of {{ amount_owed }} (due {{ due_date }}). Please pay promptly to avoid a late fee.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}{% if autopay_setup_url %} Set up autopay: {{ autopay_setup_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.')
) s (code, subject, body_html, body_text) ON s.code = t.code
WHERE NOT EXISTS (
    SELECT 1 FROM notices.notice_template_versions existing
    WHERE existing.template_id = t.template_id
      AND existing.version_number >= 3
);
GO
