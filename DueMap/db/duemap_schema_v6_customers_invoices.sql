-- =============================================================================
-- DueMap schema v6 — Synced customers + rent invoices
-- Apply AFTER duemap_schema_v5_accounting.sql
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO


-- =============================================================================
-- CUSTOMERS (a.k.a. tenants — synced from QB Customer or Xero Contact)
-- =============================================================================
-- Once synced, this row IS the tenant for DueMap's purposes. The external_*
-- columns are provenance, not foreign keys to the accounting system.
CREATE TABLE tenancy.customers (
    id                  INT             IDENTITY(1,1) NOT NULL,
    property_manager_id INT             NOT NULL,
    external_provider   VARCHAR(20)     NOT NULL,
    external_id         NVARCHAR(200)   NOT NULL,
    display_name        NVARCHAR(400)   NOT NULL,
    email               NVARCHAR(400)   NULL,
    phone               NVARCHAR(50)    NULL,
    is_active           BIT             NOT NULL CONSTRAINT df_cust_active     DEFAULT (1),
    last_synced_at      DATETIME2(7)    NOT NULL,
    created_at          DATETIME2(7)    NOT NULL CONSTRAINT df_cust_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at          DATETIME2(7)    NOT NULL CONSTRAINT df_cust_updated_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_customers              PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_cust_pm                FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT uq_cust_pm_provider_ext   UNIQUE (property_manager_id, external_provider, external_id),
    CONSTRAINT ck_cust_provider          CHECK (external_provider IN ('quickbooks', 'xero'))
);
GO

CREATE NONCLUSTERED INDEX ix_cust_pm_active ON tenancy.customers (property_manager_id, is_active);
GO


-- =============================================================================
-- RENT INVOICES (synced from QB Invoice or Xero Invoice)
-- =============================================================================
-- One row per (PM, provider, external_id). The lease_id link is set by the
-- sync orchestrator when exactly one active lease exists for the customer;
-- otherwise NULL, awaiting manual PM mapping.
CREATE TABLE tenancy.rent_invoices (
    id                      INT             IDENTITY(1,1) NOT NULL,
    property_manager_id     INT             NOT NULL,
    customer_id             INT             NOT NULL,
    lease_id                INT             NULL,
    external_provider       VARCHAR(20)     NOT NULL,
    external_id             NVARCHAR(200)   NOT NULL,
    external_doc_number     NVARCHAR(100)   NULL,
    issue_date              DATE            NULL,
    due_date                DATE            NOT NULL,
    total_amount            DECIMAL(10, 2)  NOT NULL,
    balance                 DECIMAL(10, 2)  NOT NULL,
    currency                CHAR(3)         NOT NULL,
    status                  VARCHAR(20)     NOT NULL,
    last_synced_at          DATETIME2(7)    NOT NULL,
    created_at              DATETIME2(7)    NOT NULL CONSTRAINT df_inv_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at              DATETIME2(7)    NOT NULL CONSTRAINT df_inv_updated_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_rent_invoices             PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_inv_pm                    FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT fk_inv_customer              FOREIGN KEY (customer_id)         REFERENCES tenancy.customers (id),
    CONSTRAINT fk_inv_lease                 FOREIGN KEY (lease_id)            REFERENCES tenancy.leases (id),
    CONSTRAINT uq_inv_pm_provider_ext       UNIQUE (property_manager_id, external_provider, external_id),
    CONSTRAINT ck_inv_provider              CHECK (external_provider IN ('quickbooks', 'xero')),
    CONSTRAINT ck_inv_status                CHECK (status IN ('open', 'paid', 'voided')),
    CONSTRAINT ck_inv_amounts               CHECK (total_amount >= 0 AND balance >= 0),
    CONSTRAINT ck_inv_currency_len          CHECK (LEN(currency) = 3)
);
GO

CREATE NONCLUSTERED INDEX ix_inv_customer_status
    ON tenancy.rent_invoices (customer_id, status) INCLUDE (due_date, balance);
GO

CREATE NONCLUSTERED INDEX ix_inv_lease_status_due
    ON tenancy.rent_invoices (lease_id, status, due_date DESC)
    WHERE lease_id IS NOT NULL;
GO


-- =============================================================================
-- LINK CUSTOMER → LEASE
-- =============================================================================
-- Nullable while the sync is still establishing matches; FK fires the moment
-- a value is set. A lease "belongs to" exactly one customer (the primary
-- billing party); multi-tenant residential leases are out of scope for v1.
ALTER TABLE tenancy.leases
    ADD customer_id INT NULL
        CONSTRAINT fk_lease_customer FOREIGN KEY REFERENCES tenancy.customers (id);
GO

CREATE NONCLUSTERED INDEX ix_lease_customer
    ON tenancy.leases (customer_id)
    WHERE customer_id IS NOT NULL;
GO
