-- =============================================================================
-- DueMap schema v13 — add Notice Preferences as a 3rd onboarding step
--
-- The original 3-step wizard was:
--   1 Connect  ·  2 Daily Close  ·  3 Notify Tenants
--
-- We're inserting "Notice Preferences" between Daily Close and Notify so a PM
-- can confirm the master notice toggles (pre-due / due-date / post-due) before
-- they introduce themselves to tenants. Final order:
--   1 Connect  ·  2 Daily Close  ·  3 Notice Preferences  ·  4 Notify Tenants
--
-- Schema impact is a single nullable timestamp column on property_managers.
-- The legacy back-fill mirrors what v12 did for status=4 PMs so existing
-- active accounts don't get re-prompted to onboard.
--
-- Idempotent.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
      AND name = N'step_notice_prefs_done_at')
BEGIN
    ALTER TABLE tenancy.property_managers
        ADD step_notice_prefs_done_at DATETIME2 NULL;
END
GO

-- Back-fill for legacy Active PMs — same pattern as v12. A PM already at
-- onboarding_status=4 predates this column entirely; stamp it with their
-- created_at so the kiosk router never sends them back into onboarding.
UPDATE tenancy.property_managers
   SET step_notice_prefs_done_at = ISNULL(step_notice_prefs_done_at, created_at)
 WHERE onboarding_status = 4;
GO
