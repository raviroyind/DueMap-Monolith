-- =============================================================================
-- DueMap schema v3 — Billing assessments & idempotency
-- Apply AFTER duemap_schema_v2_lease_settings.sql
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF SCHEMA_ID(N'billing') IS NULL EXEC(N'CREATE SCHEMA billing AUTHORIZATION dbo;');
GO


-- =============================================================================
-- LATE FEE ASSESSMENTS — the financial ledger
-- =============================================================================
-- One row per (lease, due_date) at most. Records the fee amount, the
-- provenance (which rule version + override fed the computation), and the
-- monthly rent snapshot at assessment time. A later rate change to the lease
-- does not retroactively change what was assessed.
CREATE TABLE billing.late_fee_assessments (
    id                          INT             IDENTITY(1,1) NOT NULL,
    lease_id                    INT             NOT NULL,
    due_date                    DATE            NOT NULL,
    assessment_date             DATE            NOT NULL,
    fee_amount                  DECIMAL(10, 2)  NOT NULL,
    monthly_rent_snapshot       DECIMAL(10, 2)  NOT NULL,
    state_rule_version_id       INT             NOT NULL,
    local_rule_override_id      INT             NULL,
    status                      VARCHAR(20)     NOT NULL CONSTRAINT df_lfa_status     DEFAULT ('assessed'),
    reversal_reason             NVARCHAR(500)   NULL,
    reversed_at                 DATETIME2(7)    NULL,
    created_at                  DATETIME2(7)    NOT NULL CONSTRAINT df_lfa_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_late_fee_assessments      PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_lfa_lease                 FOREIGN KEY (lease_id)               REFERENCES tenancy.leases (id),
    CONSTRAINT fk_lfa_state_rule_version    FOREIGN KEY (state_rule_version_id)  REFERENCES rules.state_rule_versions (id),
    CONSTRAINT fk_lfa_local_rule_override   FOREIGN KEY (local_rule_override_id) REFERENCES rules.local_rule_overrides (id),
    CONSTRAINT uq_lfa_lease_due             UNIQUE (lease_id, due_date),
    CONSTRAINT ck_lfa_status                CHECK (status IN ('assessed', 'reversed', 'waived')),
    CONSTRAINT ck_lfa_amount_positive       CHECK (fee_amount >= 0),
    CONSTRAINT ck_lfa_reversal_fields       CHECK (
        (status <> 'reversed') OR (reversal_reason IS NOT NULL AND reversed_at IS NOT NULL))
);
GO

CREATE NONCLUSTERED INDEX ix_lfa_lease_due ON billing.late_fee_assessments (lease_id, due_date DESC);
GO


-- =============================================================================
-- ASSESSMENT RUNS — the idempotency ledger
-- =============================================================================
-- One row per (lease, due_date, action_kind). The UNIQUE constraint is the
-- entire point: re-running today's job is a no-op because conflicting INSERTs
-- bounce off this index. Action codes mirror DueMap.Billing.Domain.ActionKind.
CREATE TABLE billing.assessment_runs (
    id                      BIGINT          IDENTITY(1,1) NOT NULL,
    lease_id                INT             NOT NULL,
    due_date                DATE            NOT NULL,
    assessment_date         DATE            NOT NULL,
    action_kind             VARCHAR(40)     NOT NULL,
    notice_delivery_id      BIGINT          NULL,         -- non-null for any "send_*" action
    late_fee_assessment_id  INT             NULL,         -- non-null for assess_late_fee
    created_at              DATETIME2(7)    NOT NULL CONSTRAINT df_ar_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_assessment_runs    PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_ar_lease           FOREIGN KEY (lease_id)               REFERENCES tenancy.leases (id),
    CONSTRAINT fk_ar_notice          FOREIGN KEY (notice_delivery_id)     REFERENCES notices.notice_deliveries (id),
    CONSTRAINT fk_ar_late_fee        FOREIGN KEY (late_fee_assessment_id) REFERENCES billing.late_fee_assessments (id),
    CONSTRAINT uq_ar_lease_due_kind  UNIQUE (lease_id, due_date, action_kind),
    CONSTRAINT ck_ar_action_kind     CHECK (action_kind IN (
        'send_pre_due_reminder',
        'send_due_date_reminder',
        'send_grace_period_reminder',
        'assess_late_fee',
        'send_late_fee_notice'))
);
GO

CREATE NONCLUSTERED INDEX ix_ar_lease_due ON billing.assessment_runs (lease_id, due_date DESC);
GO
