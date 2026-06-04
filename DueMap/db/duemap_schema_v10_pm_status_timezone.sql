-- =============================================================================
-- DueMap schema v10 — PM onboarding status + timezone
--
-- Adds two columns to tenancy.property_managers:
--   * onboarding_status (int, default 1)
--       1 = Registered            (PM exists, no accounting connection)
--       2 = Connected             (OAuth tokens persisted)
--       3 = Synced                (first successful customer/invoice pull)
--       4 = Active                (ready to process — sweep job will run)
--     Status only advances; never regresses on a routine update.
--
--   * time_zone_id (nvarchar(64), default 'UTC')
--     IANA tz identifier (e.g. "America/Los_Angeles"). Worker resolves each
--     PM's local midnight from this. Default 'UTC' keeps existing data safe.
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
      AND name = N'onboarding_status')
BEGIN
    ALTER TABLE tenancy.property_managers
        ADD onboarding_status INT NOT NULL CONSTRAINT DF_pm_onboarding_status DEFAULT 1;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
      AND name = N'time_zone_id')
BEGIN
    ALTER TABLE tenancy.property_managers
        ADD time_zone_id NVARCHAR(64) NOT NULL CONSTRAINT DF_pm_timezone DEFAULT N'UTC';
END
GO
