-- =============================================================================
-- DueMap schema v12 — multi-step onboarding
--
-- Splits onboarding into three discrete, resumable steps tracked per PM:
--   1. Connect Accounting System    -> step_connect_done_at
--   2. Daily Close Settings         -> step_close_done_at + new
--                                      pm_daily_close_settings row
--   3. Preflight Tenant Notification-> step_preflight_done_at + each notified
--                                      customer stamped with preflight_notified_at
--
-- Once all three are complete the existing onboarding_status flips to
-- Active(4) and the kiosk router lets the PM into the main app. If the PM
-- abandons mid-flow, the router resumes them at the first step whose
-- timestamp is still NULL.
--
-- Idempotent.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- 1. Step-completion timestamps on tenancy.property_managers --------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
      AND name = N'step_connect_done_at')
BEGIN
    ALTER TABLE tenancy.property_managers
        ADD step_connect_done_at DATETIME2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
      AND name = N'step_close_done_at')
BEGIN
    ALTER TABLE tenancy.property_managers
        ADD step_close_done_at DATETIME2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
      AND name = N'step_preflight_done_at')
BEGIN
    ALTER TABLE tenancy.property_managers
        ADD step_preflight_done_at DATETIME2 NULL;
END
GO

-- Back-fill: if a PM is already Active(4), all three steps must be considered
-- done (legacy users predate the multi-step flow). Stamp them with their
-- created_at so they're never re-prompted to onboard.
UPDATE tenancy.property_managers
   SET step_connect_done_at   = ISNULL(step_connect_done_at,   created_at),
       step_close_done_at     = ISNULL(step_close_done_at,     created_at),
       step_preflight_done_at = ISNULL(step_preflight_done_at, created_at)
 WHERE onboarding_status = 4;
GO

-- 2. Per-PM daily-close settings table ----------------------------------------
IF OBJECT_ID(N'tenancy.pm_daily_close_settings', N'U') IS NULL
BEGIN
    CREATE TABLE tenancy.pm_daily_close_settings (
        property_manager_id   INT          NOT NULL
            CONSTRAINT PK_pm_daily_close_settings PRIMARY KEY,
        enabled               BIT          NOT NULL
            CONSTRAINT DF_pm_close_enabled DEFAULT 1,
        -- Hour-of-day (0-23) at which the close email goes out, in the PM's
        -- own timezone. 7 == 07:00 local. The worker's per-PM scheduler
        -- already runs at local midnight so the email itself is queued
        -- shortly after that and dispatched at the configured local hour.
        send_hour_local       INT          NOT NULL
            CONSTRAINT DF_pm_close_send_hour DEFAULT 7,
        -- IANA tz id. Mirrors property_managers.time_zone_id by default
        -- (the onboarding step writes both together).
        time_zone_id          NVARCHAR(64) NOT NULL
            CONSTRAINT DF_pm_close_tz DEFAULT N'UTC',
        -- Override the PM's primary email when set. Useful for sending the
        -- close to a bookkeeper rather than the PM personally.
        recipient_override    NVARCHAR(256) NULL,
        -- Comma-separated CC list. Capped at 1k to keep the schema sane;
        -- we'll split + validate in the service layer.
        cc_list               NVARCHAR(1024) NULL,
        updated_at            DATETIME2     NOT NULL
            CONSTRAINT DF_pm_close_updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_pm_close_pm FOREIGN KEY (property_manager_id)
            REFERENCES tenancy.property_managers(id)
    );
END
GO

-- 3. Per-customer preflight-sent flag -----------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.customers')
      AND name = N'preflight_notified_at')
BEGIN
    ALTER TABLE tenancy.customers
        ADD preflight_notified_at DATETIME2 NULL;
END
GO

-- Helpful index for the preflight grid: PM filters customers by whether the
-- preflight has been sent, and frequently sorts by name within that.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_customers_pm_preflight'
      AND object_id = OBJECT_ID(N'tenancy.customers'))
BEGIN
    CREATE INDEX IX_customers_pm_preflight
        ON tenancy.customers(property_manager_id, preflight_notified_at)
        INCLUDE (display_name, email);
END
GO
