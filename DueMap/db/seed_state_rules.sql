-- =============================================================================
-- DueMap seed — Initial state rules (CA, TX, NY, FL, GA, NC, AZ)
-- Apply AFTER all v1–v7 schema files.
--
-- IMPORTANT: These are approximations meant for local development and demo.
-- Real launch requires lawyer-vetted figures per state and ongoing maintenance
-- as statutes change. Each row carries its citation so a paralegal can audit.
-- Idempotent: MERGE on (state_id) with effective_date IS NULL.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @effective DATE = '2024-01-01';

-- Ensure all seven states are seeded (they're in v1 already, but be safe).
MERGE rules.states AS tgt
USING (VALUES
    ('CA', N'California'),
    ('TX', N'Texas'),
    ('NY', N'New York'),
    ('FL', N'Florida'),
    ('GA', N'Georgia'),
    ('NC', N'North Carolina'),
    ('AZ', N'Arizona')
) AS src (code, name)
ON tgt.code = src.code
WHEN NOT MATCHED THEN INSERT (code, name) VALUES (src.code, src.name);
GO

DECLARE @effective DATE = '2024-01-01';

-- Insert state rule versions only if no current row exists for the state.
;WITH src AS (
    SELECT code, grace_days, late_fee_type, flat_amount, percent_of_rent, hard_cap_amount,
           notice_required_before_fee, notice_advance_days, citation, notes
    FROM (VALUES
        -- California: 3-day grace common in practice; statutory cap ~6% / $50
        ('CA',  3, 'lesser_of',  50.00,  0.0600,  50.00, 0, NULL,
            N'Cal. Civ. Code § 1671(d) (approx.) — late fees must be reasonable',
            N'CA fee must be a reasonable estimate of damages.'),
        -- Texas: 2-day grace; no statutory cap (must be in lease); 10% practical
        ('TX',  2, 'percent',    NULL,   0.1000,  NULL,  0, NULL,
            N'Tex. Prop. Code § 92.019 — reasonable estimate of damages',
            N'TX: no statutory cap. Common practice 5–10%.'),
        -- New York: 5-day grace; max $50 or 5% of rent (lesser)
        ('NY',  5, 'lesser_of',  50.00,  0.0500,  50.00, 1, 5,
            N'N.Y. Real Prop. Law § 238-a',
            N'Notice must precede the fee by at least 5 days.'),
        -- Florida: no mandatory grace; whatever is in lease
        ('FL',  0, 'percent',    NULL,   0.0500,  NULL,  0, NULL,
            N'Fla. Stat. § 83.55 — must be in lease; "reasonable"',
            N'FL: no statutory grace; defer to lease terms.'),
        -- Georgia: no mandatory grace; no cap
        ('GA',  0, 'percent',    NULL,   0.0500,  NULL,  0, NULL,
            N'O.C.G.A. § 44-7-50 et seq.',
            N'GA: no statutory grace or cap; lease controls.'),
        -- North Carolina: 5-day grace; max $15 or 5% greater
        ('NC',  5, 'greater_of', 15.00,  0.0500,  NULL,  0, NULL,
            N'N.C. Gen. Stat. § 42-46',
            N'NC fee = greater of $15 or 5% of rent.'),
        -- Arizona: no mandatory grace; 5% practical
        ('AZ',  0, 'percent',    NULL,   0.0500,  NULL,  0, NULL,
            N'A.R.S. § 33-1368(B)',
            N'AZ: late fee must be in lease; "reasonable".')
    ) v (code, grace_days, late_fee_type, flat_amount, percent_of_rent, hard_cap_amount,
         notice_required_before_fee, notice_advance_days, citation, notes)
)
INSERT INTO rules.state_rule_versions (
    state_id, effective_date, grace_period_days, late_fee_type,
    flat_amount, percent_of_rent, hard_cap_amount,
    notice_required_before_fee, notice_advance_days, source_citation, notes)
SELECT
    s.id, @effective, src.grace_days, src.late_fee_type,
    src.flat_amount, src.percent_of_rent, src.hard_cap_amount,
    src.notice_required_before_fee, src.notice_advance_days, src.citation, src.notes
FROM src
JOIN rules.states s ON s.code = src.code
WHERE NOT EXISTS (
    SELECT 1 FROM rules.state_rule_versions v
    WHERE v.state_id = s.id AND v.expires_date IS NULL
);
GO
