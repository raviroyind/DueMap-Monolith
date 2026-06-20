-- =============================================================================
-- DueMap schema v16 — feature flags (ops cockpit P0-1)
--
-- Shared by Web + Worker. Each row is one boolean toggle, optionally scoped to
-- a per-PM allow-list. The runtime semantics live in IFeatureFlags
-- (DueMap.Common.FeatureFlags); this file is the storage shape only.
--
-- Fail-safe contract: a missing row, a malformed enabled_pm_ids JSON, or any
-- read failure means "off." Callers never have to guard with try/catch.
--
-- Migration philosophy:
--   * New schema [ops] for operator-facing tables (feature flags, future
--     ops cockpit data). Keeps tenancy/notices/billing tables tenant-domain.
--   * No data seeded here. Future prompts (P0-2 dry-run, P0-3 self-heal, etc.)
--     will INSERT their flags as they're built. Until then the table is
--     empty and every flag query returns false — which is exactly the
--     "ship dark" default we want.
--
-- Idempotent.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'ops')
BEGIN
    EXEC(N'CREATE SCHEMA ops');
END
GO

IF OBJECT_ID(N'ops.feature_flags', N'U') IS NULL
BEGIN
    CREATE TABLE ops.feature_flags (
        [key]            NVARCHAR(80)  NOT NULL
            CONSTRAINT PK_feature_flags PRIMARY KEY,
        -- When 1, the flag is on for every PM regardless of enabled_pm_ids.
        enabled_global   BIT           NOT NULL
            CONSTRAINT DF_feature_flags_global DEFAULT 0,
        -- JSON array of property_manager ids (ints), e.g. "[1,7,42]".
        -- Used only when enabled_global = 0. NULL or empty/malformed = "no PMs."
        -- We deliberately use NVARCHAR(MAX) rather than the SQL Server JSON type
        -- so the table is portable to environments without native JSON parsing.
        enabled_pm_ids   NVARCHAR(MAX) NULL,
        -- Short free-text note explaining what the flag gates. Shown in the
        -- future ops cockpit admin UI; not read at runtime.
        note             NVARCHAR(400) NULL,
        updated_at       DATETIME2     NOT NULL
            CONSTRAINT DF_feature_flags_updated DEFAULT SYSUTCDATETIME()
    );
END
GO
