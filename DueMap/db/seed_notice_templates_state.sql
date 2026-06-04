-- =============================================================================
-- DueMap seed — State-specific notice templates (7 launch states)
-- Apply AFTER seed_notice_templates.sql (which creates the generic fallbacks).
--
-- Scope: only the two notice types where state law materially changes the copy
--   * grace_period_reminder — must reflect the state's statutory grace period
--   * late_fee_notice       — must cite the state's late-fee statute
-- pre_due_reminder and due_date_reminder are friendly nudges; the generic
-- versions suffice nationwide. Adding state copies for those would bloat the
-- table without value.
--
-- IMPORTANT: These citations are approximations meant for local development.
-- Production launch requires lawyer-vetted copy per state. Each row carries
-- its statute citation in the body so a paralegal can audit and replace.
--
-- Idempotent: skipped if the (state, notice_type) row already exists.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @effective DATE = '2024-01-01';

-- -----------------------------------------------------------------------------
-- Step 1 — materialize one (state_id, notice_type_id) row per pair we cover.
-- The notice_templates row is just the shell; the version below carries copy.
-- -----------------------------------------------------------------------------
;WITH state_codes (code) AS (
    SELECT * FROM (VALUES ('CA'), ('TX'), ('NY'), ('FL'), ('GA'), ('NC'), ('AZ')) v(code)
),
type_codes (code) AS (
    SELECT * FROM (VALUES ('grace_period_reminder'), ('late_fee_notice')) v(code)
),
pairs AS (
    SELECT s.id AS state_id, nt.id AS notice_type_id
    FROM   state_codes sc
    JOIN   rules.states s        ON s.code = sc.code
    CROSS JOIN type_codes tc
    JOIN   notices.notice_types nt ON nt.code = tc.code
)
INSERT INTO notices.notice_templates (state_id, notice_type_id, is_active)
SELECT p.state_id, p.notice_type_id, 1
FROM   pairs p
WHERE  NOT EXISTS (
    SELECT 1 FROM notices.notice_templates existing
    WHERE  existing.state_id      = p.state_id
      AND  existing.notice_type_id = p.notice_type_id
      AND  existing.is_active = 1);
GO

-- -----------------------------------------------------------------------------
-- Step 2 — insert one approved version per template that doesn't have one.
-- Copy is keyed on (state_code, notice_code). Each row is its own opinion of
-- the state law; the citation lives inline in body_html so PMs see the
-- legal hook when they override.
-- -----------------------------------------------------------------------------
DECLARE @effective DATE = '2024-01-01';

;WITH templates AS (
    SELECT tpl.id AS template_id, s.code AS state_code, nt.code AS notice_code
    FROM   notices.notice_templates tpl
    JOIN   rules.states s          ON s.id  = tpl.state_id
    JOIN   notices.notice_types nt ON nt.id = tpl.notice_type_id
    WHERE  tpl.state_id IS NOT NULL
      AND  tpl.is_active = 1
)
INSERT INTO notices.notice_template_versions (
    template_id, version_number, subject, body_html, body_text, required_vars,
    effective_date, approved_by, status)
SELECT
    t.template_id,
    1,
    s.subject, s.body_html, s.body_text,
    N'["tenant_name","amount_owed","due_date","monthly_rent","lease_id","grace_days"]',
    @effective, N'system', 'approved'
FROM templates t
JOIN (VALUES
    -- =========================================================================
    -- CALIFORNIA  (Civ. Code §1947 — rent due, reasonable late-fee doctrine)
    -- =========================================================================
    ('CA', 'grace_period_reminder',
        N'Rent past due — please pay within the grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>We have not received your rent of <strong>{{ amount_owed }}</strong>, originally due {{ due_date }}. California courts treat rent as due on the date stated in your lease (Civ. Code §1947); the grace period of <strong>{{ grace_days }} days</strong> reflects your lease terms.</p>
          <p>Please submit payment promptly to avoid a late fee.</p>',
        N'Hi {{ tenant_name }}, your rent of {{ amount_owed }} (due {{ due_date }}) is past due. Please pay within the {{ grace_days }}-day grace period to avoid a late fee. (CA Civ. Code §1947)'),

    ('CA', 'late_fee_notice',
        N'Late fee assessed on overdue rent (California)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, originally due {{ due_date }}. California law (Civ. Code §1671) requires late fees to be a reasonable estimate of actual damages — the amount applied reflects the figure stated in your lease.</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee has been applied to your rent (originally due {{ due_date }}). Amount reflects your lease per CA Civ. Code §1671. Contact your property manager for the updated balance.'),

    -- =========================================================================
    -- TEXAS  (Prop. Code §92.019 — late-fee cap of 12%/10% of rent)
    -- =========================================================================
    ('TX', 'grace_period_reminder',
        N'Rent past due — Texas grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> was due {{ due_date }}. Texas Property Code §92.019 permits a late fee after the second full day rent is unpaid; please submit payment within the <strong>{{ grace_days }}-day grace period</strong> to avoid the fee.</p>',
        N'Hi {{ tenant_name }}, your rent ({{ amount_owed }}, due {{ due_date }}) is past due. Pay within the {{ grace_days }}-day grace period to avoid the late fee. (TX Prop. Code §92.019)'),

    ('TX', 'late_fee_notice',
        N'Late fee assessed on overdue rent (Texas)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. Texas Property Code §92.019 caps the fee at 12% of rent (4-unit dwellings or fewer) or 10% (5+ units). The amount applied is within these limits.</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee was applied per TX Prop. Code §92.019 (capped at 12%/10% of rent). Contact your property manager for the updated balance.'),

    -- =========================================================================
    -- NEW YORK  (RPL §238-a — 5-day mandatory grace + $50/5% cap)
    -- =========================================================================
    ('NY', 'grace_period_reminder',
        N'Rent past due — New York 5-day grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>We have not received your rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. New York Real Property Law §238-a guarantees a five-day grace period before any late fee may be assessed. Please submit payment within this window to avoid the fee.</p>',
        N'Hi {{ tenant_name }}, your rent of {{ amount_owed }} (due {{ due_date }}) is past due. NY RPL §238-a provides a 5-day grace period — please pay within this window to avoid the late fee.'),

    ('NY', 'late_fee_notice',
        N'Late fee assessed on overdue rent (New York)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. Under New York Real Property Law §238-a, late fees are capped at <strong>$50 or 5% of monthly rent, whichever is less</strong>. The fee applied is within this limit.</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee was applied per NY RPL §238-a (max $50 or 5% of rent, whichever less). Contact your property manager for the updated balance.'),

    -- =========================================================================
    -- FLORIDA  (no statutory grace; lease controls; Fla. Stat. §83.45 framework)
    -- =========================================================================
    ('FL', 'grace_period_reminder',
        N'Rent past due — please review your lease grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> was due {{ due_date }}. Florida law does not set a statewide grace period; your lease specifies <strong>{{ grace_days }} days</strong>. Please submit payment within this window to avoid a late fee.</p>',
        N'Hi {{ tenant_name }}, your rent ({{ amount_owed }}, due {{ due_date }}) is past due. Florida grace period is per-lease ({{ grace_days }} days). Please pay to avoid the late fee.'),

    ('FL', 'late_fee_notice',
        N'Late fee assessed on overdue rent (Florida)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. The fee amount is governed by your lease terms (Florida does not statutorily cap residential late fees, but Fla. Stat. §83.45 requires them to be set in the lease).</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee was applied per your lease (FL Stat. §83.45). Contact your property manager for the updated balance.'),

    -- =========================================================================
    -- GEORGIA  (no statutory grace; OCGA §44-7-50 governs payment)
    -- =========================================================================
    ('GA', 'grace_period_reminder',
        N'Rent past due — please review your lease grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> was due {{ due_date }}. Georgia law does not establish a statewide grace period; your lease specifies <strong>{{ grace_days }} days</strong>. Please submit payment within this window to avoid a late fee.</p>',
        N'Hi {{ tenant_name }}, your rent ({{ amount_owed }}, due {{ due_date }}) is past due. Georgia grace period is per-lease ({{ grace_days }} days). Please pay to avoid the late fee.'),

    ('GA', 'late_fee_notice',
        N'Late fee assessed on overdue rent (Georgia)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. The fee is governed by your lease terms; Georgia (OCGA §44-7) does not statutorily cap residential late fees.</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee was applied per your lease (GA OCGA §44-7). Contact your property manager for the updated balance.'),

    -- =========================================================================
    -- NORTH CAROLINA  (NCGS §42-46 — 5-day grace + $15/5% cap)
    -- =========================================================================
    ('NC', 'grace_period_reminder',
        N'Rent past due — North Carolina 5-day grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> was due {{ due_date }}. North Carolina General Statute §42-46 guarantees a five-day grace period before any late fee may be assessed. Please submit payment within this window to avoid the fee.</p>',
        N'Hi {{ tenant_name }}, your rent of {{ amount_owed }} (due {{ due_date }}) is past due. NC §42-46 provides a 5-day grace period — please pay within this window.'),

    ('NC', 'late_fee_notice',
        N'Late fee assessed on overdue rent (North Carolina)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. Under North Carolina General Statute §42-46, late fees on monthly rent are capped at <strong>$15 or 5% of the monthly rent, whichever is greater</strong>.</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee was applied per NC §42-46 (max $15 or 5% of rent, whichever greater). Contact your property manager for the updated balance.'),

    -- =========================================================================
    -- ARIZONA  (ARS §33-1368 — no statutory cap; reasonable lease terms)
    -- =========================================================================
    ('AZ', 'grace_period_reminder',
        N'Rent past due — please review your lease grace period',
        N'<p>Hi {{ tenant_name }},</p>
          <p>Your rent of <strong>{{ amount_owed }}</strong> was due {{ due_date }}. Arizona Residential Landlord and Tenant Act (ARS §33-1368) does not mandate a statewide grace period; your lease specifies <strong>{{ grace_days }} days</strong>. Please submit payment within this window to avoid a late fee.</p>',
        N'Hi {{ tenant_name }}, your rent ({{ amount_owed }}, due {{ due_date }}) is past due. Arizona grace period is per-lease ({{ grace_days }} days). Please pay to avoid the late fee.'),

    ('AZ', 'late_fee_notice',
        N'Late fee assessed on overdue rent (Arizona)',
        N'<p>Hi {{ tenant_name }},</p>
          <p>A late fee has been applied to your overdue rent of <strong>{{ amount_owed }}</strong>, due {{ due_date }}. Arizona (ARS §33-1368) requires late fees to be reasonable and stated in the lease; the amount applied reflects those lease terms.</p>
          <p>Please contact your property manager for the updated balance.</p>',
        N'Hi {{ tenant_name }}, a late fee was applied per AZ ARS §33-1368 and your lease terms. Contact your property manager for the updated balance.')
) s (state_code, notice_code, subject, body_html, body_text)
    ON s.state_code = t.state_code AND s.notice_code = t.notice_code
WHERE NOT EXISTS (
    SELECT 1 FROM notices.notice_template_versions existing
    WHERE  existing.template_id = t.template_id
      AND  existing.status = 'approved');
GO
