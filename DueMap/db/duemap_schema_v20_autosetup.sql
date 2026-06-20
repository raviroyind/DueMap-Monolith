-- =============================================================================
-- v20  AutoSetup (P1-3) — staged late-fee profile + PM auto-setup summary
-- =============================================================================
-- AutoSetup writes a per-lease late-fee profile + flips `fees_staged = 1` so
-- the assessment planner SKIPS the lease until the PM clicks "Go live."
-- Existing leases default to fees_staged = 0 so behaviour is unchanged for
-- already-live PMs; the AutoSetup writer explicitly sets 1 on each lease it
-- touches.
--
-- Idempotent: append-only ALTERs, guarded with IF NOT EXISTS.
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;

-- ----------------------------------------------------------------------------
-- tenancy.leases — late-fee profile + staging flag
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'late_fee_type')
BEGIN
    ALTER TABLE tenancy.leases ADD late_fee_type TINYINT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'late_fee_percent')
BEGIN
    ALTER TABLE tenancy.leases ADD late_fee_percent DECIMAL(5,2) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'late_fee_flat_amount')
BEGIN
    ALTER TABLE tenancy.leases ADD late_fee_flat_amount DECIMAL(10,2) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'late_fee_grace_days')
BEGIN
    ALTER TABLE tenancy.leases ADD late_fee_grace_days TINYINT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'late_fee_daily_accrual')
BEGIN
    ALTER TABLE tenancy.leases ADD late_fee_daily_accrual BIT NULL;
END;

-- fees_staged: NOT NULL, default 0 so back-compat is preserved. AutoSetup
-- writes 1 on the leases it processes; "Go live" (P1-4) flips them back to 0.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'fees_staged')
BEGIN
    ALTER TABLE tenancy.leases
        ADD fees_staged BIT NOT NULL CONSTRAINT DF_leases_fees_staged DEFAULT (0);
END;

-- ----------------------------------------------------------------------------
-- tenancy.property_managers — auto-setup summary + stamp
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
                 AND name = 'auto_setup_summary')
BEGIN
    -- JSON serialisation of AutoSetupSummary — leases touched, findings,
    -- timing. Surfaced verbatim on the review screen (P1-4).
    ALTER TABLE tenancy.property_managers ADD auto_setup_summary NVARCHAR(MAX) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.property_managers')
                 AND name = 'auto_setup_done_at')
BEGIN
    ALTER TABLE tenancy.property_managers ADD auto_setup_done_at DATETIME2(3) NULL;
END;
