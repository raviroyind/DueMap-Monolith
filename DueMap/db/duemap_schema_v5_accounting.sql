-- =============================================================================
-- DueMap schema v5 — Accounting OAuth + PM defaults
-- Apply AFTER duemap_schema_v4_pm_processing.sql
-- Customer + RentInvoice tables land in v6 alongside the sync impl.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF SCHEMA_ID(N'integrations') IS NULL EXEC(N'CREATE SCHEMA integrations AUTHORIZATION dbo;');
GO


-- =============================================================================
-- PM ACCOUNTING CONNECTION — one active provider per PM
-- =============================================================================
-- Tokens are stored as base64(IDataProtector.Protect(utf8 bytes)). Reading them
-- requires the same Data Protection keyring that wrote them — Web and Worker
-- must share it.
CREATE TABLE integrations.pm_accounting_connections (
    property_manager_id         INT             NOT NULL,
    provider                    VARCHAR(20)     NOT NULL,
    realm_id                    NVARCHAR(200)   NOT NULL,        -- QB company id / Xero tenant id
    access_token_protected      NVARCHAR(MAX)   NOT NULL,
    refresh_token_protected     NVARCHAR(MAX)   NOT NULL,
    access_token_expires_at     DATETIME2(7)    NOT NULL,
    refresh_token_expires_at    DATETIME2(7)    NULL,            -- QB ~100d, Xero ~60d
    scopes                      NVARCHAR(500)   NULL,
    connected_at                DATETIME2(7)    NOT NULL,
    last_sync_at                DATETIME2(7)    NULL,
    last_sync_error             NVARCHAR(MAX)   NULL,
    status                      VARCHAR(20)     NOT NULL CONSTRAINT df_pmac_status DEFAULT ('connected'),
    updated_at                  DATETIME2(7)    NOT NULL CONSTRAINT df_pmac_updated_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_pm_accounting_connections PRIMARY KEY CLUSTERED (property_manager_id),
    CONSTRAINT fk_pmac_pm                   FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT ck_pmac_provider             CHECK (provider IN ('quickbooks', 'xero')),
    CONSTRAINT ck_pmac_status               CHECK (status   IN ('connected', 'token_expired', 'disconnected'))
);
GO


-- =============================================================================
-- OAUTH ATTEMPTS — short-lived CSRF state tokens
-- =============================================================================
-- The "state" query param echoed by the provider must match a row here that
-- (a) hasn't been consumed and (b) was issued recently. A background job
-- prunes consumed/old rows; for now the consumed_at column carries that load.
CREATE TABLE integrations.oauth_attempts (
    state                   NVARCHAR(100)   NOT NULL,
    property_manager_id     INT             NOT NULL,
    provider                VARCHAR(20)     NOT NULL,
    redirect_uri            NVARCHAR(500)   NOT NULL,
    created_at              DATETIME2(7)    NOT NULL CONSTRAINT df_oa_created_at DEFAULT (SYSUTCDATETIME()),
    consumed_at             DATETIME2(7)    NULL,
    CONSTRAINT pk_oauth_attempts PRIMARY KEY CLUSTERED (state),
    CONSTRAINT fk_oa_pm         FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT ck_oa_provider   CHECK (provider IN ('quickbooks', 'xero'))
);
GO

CREATE NONCLUSTERED INDEX ix_oa_pm_created
    ON integrations.oauth_attempts (property_manager_id, created_at DESC);
GO


-- =============================================================================
-- PM ACCOUNTING DEFAULTS — pulled from QB/Xero, also editable in DueMap
-- =============================================================================
-- timezone_id is an IANA name (e.g. "America/Los_Angeles"). The orchestrator
-- uses it to compute "midnight in the PM's local time" — once this row exists,
-- the sweep job swaps the UTC-today fallback for tz-aware processing.
CREATE TABLE tenancy.pm_accounting_defaults (
    property_manager_id                INT             NOT NULL,
    default_currency                   CHAR(3)         NOT NULL CONSTRAINT df_pad_currency DEFAULT ('USD'),
    default_payment_terms_days         INT             NOT NULL CONSTRAINT df_pad_terms    DEFAULT (0),
    default_late_fee_item_external_id  NVARCHAR(200)   NULL,
    timezone_id                        NVARCHAR(60)    NOT NULL CONSTRAINT df_pad_tz       DEFAULT ('America/New_York'),
    last_synced_at                     DATETIME2(7)    NULL,
    updated_at                         DATETIME2(7)    NOT NULL CONSTRAINT df_pad_updated_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_pm_accounting_defaults PRIMARY KEY CLUSTERED (property_manager_id),
    CONSTRAINT fk_pad_pm                 FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT ck_pad_currency_len       CHECK (LEN(default_currency) = 3),
    CONSTRAINT ck_pad_terms_range        CHECK (default_payment_terms_days BETWEEN 0 AND 365)
);
GO
