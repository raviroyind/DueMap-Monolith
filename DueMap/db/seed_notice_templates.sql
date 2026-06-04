-- =============================================================================
-- DueMap seed — System notice templates (generic, all states)
-- One approved template version per notice_type. PMs can override via
-- pm_template_overrides; the system templates here are the fallback.
-- Idempotent: skips insertion when an active generic template already exists
-- for a given (notice_type).
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

;WITH t AS (
    SELECT
        nt.id AS notice_type_id,
        nt.code
    FROM notices.notice_types nt
    WHERE nt.code IN ('pre_due_reminder', 'due_date_reminder', 'grace_period_reminder', 'late_fee_notice')
)
INSERT INTO notices.notice_templates (state_id, notice_type_id, is_active)
SELECT NULL, t.notice_type_id, 1
FROM t
WHERE NOT EXISTS (
    SELECT 1 FROM notices.notice_templates existing
    WHERE existing.notice_type_id = t.notice_type_id
      AND existing.state_id IS NULL
      AND existing.is_active = 1
);
GO

-- Insert one approved version per system template that doesn't have one yet.
DECLARE @effective DATE = '2024-01-01';

;WITH templates AS (
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
    1,
    s.subject, s.body_html, s.body_text,
    N'["tenant_name","amount_owed","due_date","monthly_rent","lease_id"]',
    @effective, N'system', 'approved'
FROM templates t
JOIN (VALUES
    ('pre_due_reminder',
        N'Rent reminder: payment due on {{ due_date }}',
        N'<p>Hi {{ tenant_name }},</p><p>This is a friendly reminder that your rent of <strong>{{ amount_owed }}</strong> is due on <strong>{{ due_date }}</strong>.</p><p>Thanks,<br/>Your property manager</p>',
        N'Hi {{ tenant_name }}, this is a reminder that your rent of {{ amount_owed }} is due on {{ due_date }}.'),

    ('due_date_reminder',
        N'Rent is due today',
        N'<p>Hi {{ tenant_name }},</p><p>Your rent of <strong>{{ amount_owed }}</strong> is due <strong>today ({{ due_date }})</strong>. Please ensure payment is submitted before the end of the day.</p>',
        N'Hi {{ tenant_name }}, your rent of {{ amount_owed }} is due today, {{ due_date }}.'),

    ('grace_period_reminder',
        N'Rent payment past due',
        N'<p>Hi {{ tenant_name }},</p><p>We have not received your rent payment of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. You remain within the grace period; please submit payment promptly to avoid a late fee.</p>',
        N'Hi {{ tenant_name }}, we have not received your rent payment of {{ amount_owed }} (due {{ due_date }}). Please pay promptly to avoid a late fee.'),

    ('late_fee_notice',
        N'Late fee assessed on overdue rent',
        N'<p>Hi {{ tenant_name }},</p><p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong> originally due on {{ due_date }}. Please contact your property manager for the updated balance and to arrange payment.</p>',
        N'Hi {{ tenant_name }}, a late fee has been applied to your overdue rent of {{ amount_owed }} (originally due {{ due_date }}). Contact your property manager for the updated balance.')
) s (code, subject, body_html, body_text) ON s.code = t.code
WHERE NOT EXISTS (
    SELECT 1 FROM notices.notice_template_versions existing
    WHERE existing.template_id = t.template_id AND existing.status = 'approved'
);
GO
