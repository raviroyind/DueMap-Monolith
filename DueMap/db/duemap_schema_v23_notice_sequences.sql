-- =============================================================================
-- v23  Reminder sequences (P2-3)
-- =============================================================================
-- Configurable multi-touch reminder cadence per PM (e.g. -3d email, due-day
-- SMS, +1d grace SMS). The SequenceResolver reads the active row; the
-- AssessmentPlanner emits one action per step that's due on a given day, each
-- carrying a step_key so the idempotency ledger fires every step exactly once.
--
-- billing.notice_sequences holds the definition (steps as JSON). The roadmap
-- placed this in the notices schema; we keep it in billing because the planner
-- (Billing) owns it and it pairs with the assessment_runs idempotency change.
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables
               WHERE object_id = OBJECT_ID(N'billing.notice_sequences'))
BEGIN
    CREATE TABLE billing.notice_sequences (
        id          INT IDENTITY(1,1) PRIMARY KEY,
        pm_id       INT           NOT NULL,
        name        NVARCHAR(80)  NOT NULL,
        steps_json  NVARCHAR(MAX) NOT NULL,   -- [{"OffsetDays":-3,"Channel":"email","NoticeTypeCode":"pre_due_reminder"}, ...]
        is_active   BIT           NOT NULL CONSTRAINT DF_notice_sequences_is_active DEFAULT (1),
        created_at  DATETIME2(3)  NOT NULL CONSTRAINT DF_notice_sequences_created_at DEFAULT (SYSUTCDATETIME()),
        updated_at  DATETIME2(3)  NOT NULL CONSTRAINT DF_notice_sequences_updated_at DEFAULT (SYSUTCDATETIME())
    );

    -- At most one active sequence per PM (filtered unique).
    CREATE UNIQUE INDEX UX_notice_sequences_pm_active
        ON billing.notice_sequences(pm_id) WHERE is_active = 1;
END;
GO

-- ---- assessment_runs: add step_key + widen the idempotency key ----
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'billing.assessment_runs')
                 AND name = 'step_key')
BEGIN
    ALTER TABLE billing.assessment_runs ADD step_key VARCHAR(40) NULL;
END;
GO

-- Swap the unique key to include step_key. Existing rows have step_key = NULL,
-- and SQL Server treats NULLs as equal in a UNIQUE index, so their uniqueness
-- on (lease, due, kind) is preserved exactly. Sequenced rows set a step_key and
-- become independently unique.
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'uq_ar_lease_due_kind')
BEGIN
    ALTER TABLE billing.assessment_runs DROP CONSTRAINT uq_ar_lease_due_kind;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'uq_ar_lease_due_kind_step')
BEGIN
    ALTER TABLE billing.assessment_runs
        ADD CONSTRAINT uq_ar_lease_due_kind_step UNIQUE (lease_id, due_date, action_kind, step_key);
END;
GO
