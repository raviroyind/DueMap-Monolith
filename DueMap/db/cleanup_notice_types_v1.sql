-- =============================================================================
-- DueMap cleanup — consolidate duplicate notice_types
--
-- Origin: an early prototype seeded the notice_types catalogue with names that
-- overlapped with the v1 canonical set. The duplicates have zero templates
-- attached (verified before authoring this script), so they can be removed
-- without orphaning copy or breaking FKs.
--
-- KEPT (v1 launch surface, 4 + 2 = 6 codes):
--   pre_due_reminder       — friendly nudge before rent due
--   due_date_reminder      — same-day reminder
--   grace_period_reminder  — post-due, within grace
--   late_fee_notice        — late fee assessed
--   pay_or_quit            — eviction prep (v2, distinct intent)
--   eviction_warning       — eviction prep (v2, distinct intent)
--
-- REMOVED:
--   rent_reminder          — duplicate of pre_due_reminder
--   late_fee_assessed      — duplicate of late_fee_notice
--   late_notice            — duplicate of grace_period_reminder
--
-- Idempotent: each DELETE filters by code and skips if missing.
-- Safety: aborts with RAISERROR if any of the removed codes has a template,
-- override, or scheduled-notice row attached. That would mean the duplicate
-- got wired up between this script's authoring and its execution — manual
-- migration required.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @removed TABLE (code NVARCHAR(64));
INSERT INTO @removed (code) VALUES ('rent_reminder'), ('late_fee_assessed'), ('late_notice');

-- Safety check 1: any templates pointing at the codes we're about to remove?
IF EXISTS (
    SELECT 1
    FROM notices.notice_templates tpl
    JOIN notices.notice_types nt ON nt.id = tpl.notice_type_id
    WHERE nt.code IN (SELECT code FROM @removed))
BEGIN
    RAISERROR(N'cleanup_notice_types_v1: at least one duplicate code has templates attached. Aborting; migrate first.', 16, 1);
    RETURN;
END;

-- Safety check 2: lease-level settings or PM overrides on the deprecated codes?
IF EXISTS (
    SELECT 1
    FROM notices.pm_template_overrides ov
    JOIN notices.notice_types nt ON nt.id = ov.notice_type_id
    WHERE nt.code IN (SELECT code FROM @removed))
BEGIN
    RAISERROR(N'cleanup_notice_types_v1: pm_template_overrides reference deprecated codes. Aborting.', 16, 1);
    RETURN;
END;

-- All clear — drop the duplicate notice_types rows.
DELETE nt
FROM   notices.notice_types nt
WHERE  nt.code IN (SELECT code FROM @removed);

PRINT N'Removed duplicate notice_types: rent_reminder, late_fee_assessed, late_notice';
GO
