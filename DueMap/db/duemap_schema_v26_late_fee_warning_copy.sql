-- =============================================================================
-- DueMap schema v26 — late-fee notice becomes a WARNING (not a receipt)
--
-- Product decision (Option C, "notify-only"): DueMap never posts a late fee to
-- QuickBooks or Xero — it only warns the tenant that one may apply, and records
-- an internal assessment as the PM's audit/evidence trail. The old copy said
-- "a late fee has been applied ... updated balance attached", which implied a
-- charge DueMap doesn't actually make. This rewrites the late_fee_notice to a
-- warning: "a late fee MAY be applied per your lease and your state's rules."
--
-- Versioning: insert version_number=3 (approved) of the generic late_fee_notice.
-- ResolveRenderableAsync picks the highest approved version, so the new copy
-- goes live on the next dispatch. required_vars unchanged.
--
-- Idempotent: skips if late_fee_notice already has version_number >= 3.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @effective DATE = '2026-01-01';

;WITH t AS (
    SELECT tpl.id AS template_id
    FROM notices.notice_templates tpl
    JOIN notices.notice_types nt ON nt.id = tpl.notice_type_id
    WHERE tpl.state_id IS NULL AND tpl.is_active = 1
      AND nt.code = 'late_fee_notice'
)
INSERT INTO notices.notice_template_versions (
    template_id, version_number, subject, body_html, body_text, required_vars,
    effective_date, approved_by, status)
SELECT
    t.template_id,
    3,
    N'Important: your rent is overdue and a late fee may apply',
    N'<p>Hi {{ tenant_name }},</p>
      <p>We haven''t received your rent payment of <strong>{{ amount_owed }}</strong>, which was due on <strong>{{ due_date }}</strong>.</p>
      <p>Because it''s now past due, <strong>a late fee may be applied to your account</strong> in line with your lease agreement and your state''s rules. Please pay as soon as possible to avoid additional charges.</p>
      <p>Your most recent invoice is attached for your records.</p>
      {% if pay_url %}<p style="margin:24px 0;"><a href="{{ pay_url }}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;">Pay Now</a></p>{% endif %}
      <p style="color:#6b7280;font-size:13px;margin-top:24px;">If you''ve already submitted payment, please disregard this notice.</p>',
    N'Hi {{ tenant_name }}, we haven''t received your rent payment of {{ amount_owed }} (due {{ due_date }}). Because it''s now past due, a late fee may be applied to your account per your lease and your state''s rules. Please pay as soon as possible to avoid additional charges. Your most recent invoice is attached.{% if pay_url %} Pay online: {{ pay_url }}{% endif %}

If you''ve already submitted payment, please disregard this notice.',
    N'["tenant_name","amount_owed","due_date","monthly_rent","lease_id"]',
    @effective, N'system', 'approved'
FROM t
WHERE NOT EXISTS (
    SELECT 1 FROM notices.notice_template_versions existing
    WHERE existing.template_id = t.template_id
      AND existing.version_number >= 3
);
GO
