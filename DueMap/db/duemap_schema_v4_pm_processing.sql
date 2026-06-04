-- =============================================================================
-- DueMap schema v4 — Per-PM daily processing idempotency
-- Apply AFTER duemap_schema_v3_assessments.sql
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO


-- =============================================================================
-- PM PROCESSING RUNS — one row per (PM, business_date)
-- =============================================================================
-- The recurring sweep job tries to insert a row here for each (PM, today) that
-- has at least one active lease. The UNIQUE constraint guarantees only one
-- worker actually performs today's processing for a given PM, even if multiple
-- worker processes fire the sweep at the same moment.
--
-- "business_date" is the calendar date in the PM's local time zone. The
-- timezone source will land with the accounting-defaults table; until then,
-- callers pass UTC today.
CREATE TABLE billing.pm_processing_runs (
    id                      BIGINT          IDENTITY(1,1) NOT NULL,
    property_manager_id     INT             NOT NULL,
    business_date           DATE            NOT NULL,
    started_at              DATETIME2(7)    NOT NULL,
    completed_at            DATETIME2(7)    NULL,
    status                  VARCHAR(20)     NOT NULL CONSTRAINT df_ppr_status DEFAULT ('started'),
    failure_reason          NVARCHAR(MAX)   NULL,
    leases_planned          INT             NOT NULL CONSTRAINT df_ppr_leases_planned DEFAULT (0),
    actions_executed        INT             NOT NULL CONSTRAINT df_ppr_actions_exec   DEFAULT (0),
    CONSTRAINT pk_pm_processing_runs    PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_ppr_pm                FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT uq_ppr_pm_date           UNIQUE (property_manager_id, business_date),
    CONSTRAINT ck_ppr_status            CHECK (status IN ('started', 'completed', 'failed')),
    CONSTRAINT ck_ppr_failure_when_failed CHECK (status <> 'failed' OR failure_reason IS NOT NULL)
);
GO

CREATE NONCLUSTERED INDEX ix_ppr_business_date
    ON billing.pm_processing_runs (business_date DESC, property_manager_id);
GO
