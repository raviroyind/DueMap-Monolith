-- =============================================================================
-- DueMap schema v2 — Lease notice settings & PM template overrides
-- Apply AFTER duemap_schema.sql
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- =============================================================================
-- PM-LEVEL NOTICE PREFERENCES (master toggles + defaults)
-- =============================================================================
-- Three product-facing "kinds" the PM toggles in the UI:
--   pre_due   -> pre_due_reminder notices
--   due_date  -> due_date_reminder notices
--   post_due  -> grace_period_reminder + late_fee_notice
--
-- INVARIANT: when *_master_enabled = 0, no notice of that kind goes out for ANY
-- lease under this PM regardless of per-lease settings. Master toggle is a kill
-- switch, not a default.
CREATE TABLE tenancy.pm_notice_preferences (
    property_manager_id              INT             NOT NULL,
    pre_due_master_enabled           BIT             NOT NULL CONSTRAINT df_pnp_pre_master   DEFAULT (1),
    pre_due_default_days_before      INT             NOT NULL CONSTRAINT df_pnp_pre_days     DEFAULT (3),
    due_date_master_enabled          BIT             NOT NULL CONSTRAINT df_pnp_due_master   DEFAULT (1),
    post_due_master_enabled          BIT             NOT NULL CONSTRAINT df_pnp_post_master  DEFAULT (1),
    post_due_default_mode            VARCHAR(20)     NOT NULL CONSTRAINT df_pnp_post_mode    DEFAULT ('grace_period'),
    post_due_default_grace_days      INT             NOT NULL CONSTRAINT df_pnp_post_grace   DEFAULT (5),
    updated_at                       DATETIME2(7)    NOT NULL CONSTRAINT df_pnp_updated_at   DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_pm_notice_preferences PRIMARY KEY CLUSTERED (property_manager_id),
    CONSTRAINT fk_pnp_pm                FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT ck_pnp_post_mode         CHECK (post_due_default_mode IN ('grace_period', 'immediate_late_fee')),
    CONSTRAINT ck_pnp_pre_days_range    CHECK (pre_due_default_days_before BETWEEN 1 AND 60),
    CONSTRAINT ck_pnp_grace_days_range  CHECK (post_due_default_grace_days BETWEEN 0 AND 60)
);
GO


-- =============================================================================
-- PER-LEASE NOTICE SETTINGS
-- =============================================================================
-- The *_enabled flags default to 1 (opt-out model). NULL value columns mean
-- "inherit the PM default". The grace floor against state law is applied at
-- assessment time in the Billing module — this table stores the PM's *request*.
CREATE TABLE tenancy.lease_notice_settings (
    lease_id                INT             NOT NULL,
    pre_due_enabled         BIT             NOT NULL CONSTRAINT df_lns_pre_enabled  DEFAULT (1),
    pre_due_days_before     INT             NULL,
    due_date_enabled        BIT             NOT NULL CONSTRAINT df_lns_due_enabled  DEFAULT (1),
    post_due_enabled        BIT             NOT NULL CONSTRAINT df_lns_post_enabled DEFAULT (1),
    post_due_mode           VARCHAR(20)     NULL,
    post_due_grace_days     INT             NULL,
    updated_at              DATETIME2(7)    NOT NULL CONSTRAINT df_lns_updated_at   DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_lease_notice_settings PRIMARY KEY CLUSTERED (lease_id),
    CONSTRAINT fk_lns_lease             FOREIGN KEY (lease_id) REFERENCES tenancy.leases (id),
    CONSTRAINT ck_lns_post_mode         CHECK (post_due_mode IS NULL OR post_due_mode IN ('grace_period', 'immediate_late_fee')),
    CONSTRAINT ck_lns_pre_days_range    CHECK (pre_due_days_before IS NULL OR pre_due_days_before BETWEEN 1 AND 60),
    CONSTRAINT ck_lns_grace_days_range  CHECK (post_due_grace_days IS NULL OR post_due_grace_days BETWEEN 0 AND 60)
);
GO


-- =============================================================================
-- PM TEMPLATE OVERRIDES — custom copy per PM (+ optional state)
-- =============================================================================
-- Resolution order at render time:
--   1. PM override for (PM, type, lease.state_id)        — state-specific copy
--   2. PM override for (PM, type, state_id IS NULL)      — generic PM copy
--   3. system notice_template_versions (state)           — system state template
--   4. system notice_template_versions (generic)         — system generic fallback
--
-- The system template_version still feeds required_vars at render time — PMs
-- can change the wording but cannot drop legally-required placeholders.
CREATE TABLE notices.pm_template_overrides (
    id                  INT             IDENTITY(1,1) NOT NULL,
    property_manager_id INT             NOT NULL,
    notice_type_id      INT             NOT NULL,
    state_id            INT             NULL,                  -- NULL = applies to all states for this PM
    subject             NVARCHAR(400)   NOT NULL,
    body_html           NVARCHAR(MAX)   NOT NULL,
    body_text           NVARCHAR(MAX)   NOT NULL,
    is_active           BIT             NOT NULL CONSTRAINT df_pto_active     DEFAULT (1),
    updated_at          DATETIME2(7)    NOT NULL CONSTRAINT df_pto_updated_at DEFAULT (SYSUTCDATETIME()),
    created_at          DATETIME2(7)    NOT NULL CONSTRAINT df_pto_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_pm_template_overrides PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_pto_pm    FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT fk_pto_type  FOREIGN KEY (notice_type_id)      REFERENCES notices.notice_types (id),
    CONSTRAINT fk_pto_state FOREIGN KEY (state_id)            REFERENCES rules.states (id)
);
GO

-- At most one active override per (PM, type, state).
CREATE UNIQUE NONCLUSTERED INDEX ux_pto_active_pm_type_state
    ON notices.pm_template_overrides (property_manager_id, notice_type_id, state_id)
    WHERE is_active = 1 AND state_id IS NOT NULL;
GO

-- And at most one active generic (state_id IS NULL) per (PM, type).
CREATE UNIQUE NONCLUSTERED INDEX ux_pto_active_pm_type_generic
    ON notices.pm_template_overrides (property_manager_id, notice_type_id)
    WHERE is_active = 1 AND state_id IS NULL;
GO


-- =============================================================================
-- DELIVERY AUDIT — track which PM override fed the render
-- =============================================================================
ALTER TABLE notices.notice_deliveries
    ADD pm_template_override_id INT NULL
        CONSTRAINT fk_nd_pm_override FOREIGN KEY REFERENCES notices.pm_template_overrides (id);
GO


-- =============================================================================
-- SEED — the four product-level notice type codes (idempotent)
-- =============================================================================
MERGE notices.notice_types AS tgt
USING (VALUES
    ('pre_due_reminder',       N'Pre-due reminder',        100),
    ('due_date_reminder',      N'Due-date reminder',       200),
    ('grace_period_reminder',  N'Grace-period reminder',   300),
    ('late_fee_notice',        N'Late fee notice',         400)
) AS src (code, display_name, legal_priority)
ON tgt.code = src.code
WHEN NOT MATCHED THEN
    INSERT (code, display_name, legal_priority)
    VALUES (src.code, src.display_name, src.legal_priority);
GO
