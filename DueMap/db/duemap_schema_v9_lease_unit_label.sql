-- =============================================================================
-- DueMap schema v9 — unit label on leases
--
-- Adds tenancy.leases.unit_label so imported rent rolls carry the PM's own
-- unit identifier (e.g., "Apt 3B", "Unit 12", "204 N Main #5"). Free-form
-- string for v1 — Tier 2 will introduce a normalized properties + property_units
-- table once multi-property reporting becomes a real need.
--
-- Idempotent: skipped if the column already exists.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.leases')
      AND name = N'unit_label')
BEGIN
    ALTER TABLE tenancy.leases
        ADD unit_label NVARCHAR(50) NULL;
END
GO
