-- =============================================================================
-- DueMap schema v7 — ASP.NET Core Identity tables
-- Apply AFTER duemap_schema_v6_customers_invoices.sql
--
-- Int keys (default EF Identity uses GUID strings; we deviate for cleaner
-- joins into tenancy.property_managers). Table names are snake_case under
-- `identity` schema; the EF Core IdentityDbContext maps to them via Fluent API.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity] AUTHORIZATION dbo;');
GO


-- =============================================================================
-- USERS — one row per registered login. Each user belongs to exactly one PM.
-- =============================================================================
CREATE TABLE [identity].users (
    id                          INT             IDENTITY(1,1) NOT NULL,
    property_manager_id         INT             NOT NULL,
    user_name                   NVARCHAR(256)   NULL,
    normalized_user_name        NVARCHAR(256)   NULL,
    email                       NVARCHAR(256)   NULL,
    normalized_email            NVARCHAR(256)   NULL,
    email_confirmed             BIT             NOT NULL CONSTRAINT df_users_email_conf DEFAULT (0),
    password_hash               NVARCHAR(MAX)   NULL,
    security_stamp              NVARCHAR(MAX)   NULL,
    concurrency_stamp           NVARCHAR(MAX)   NULL,
    phone_number                NVARCHAR(50)    NULL,
    phone_number_confirmed      BIT             NOT NULL CONSTRAINT df_users_phone_conf DEFAULT (0),
    two_factor_enabled          BIT             NOT NULL CONSTRAINT df_users_2fa        DEFAULT (0),
    lockout_end                 DATETIMEOFFSET  NULL,
    lockout_enabled             BIT             NOT NULL CONSTRAINT df_users_lockout    DEFAULT (1),
    access_failed_count         INT             NOT NULL CONSTRAINT df_users_failed     DEFAULT (0),
    created_at                  DATETIME2(7)    NOT NULL CONSTRAINT df_users_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_users PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_users_pm FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX ux_users_normalized_user_name
    ON [identity].users (normalized_user_name)
    WHERE normalized_user_name IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX ix_users_normalized_email
    ON [identity].users (normalized_email)
    WHERE normalized_email IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX ix_users_pm ON [identity].users (property_manager_id);
GO


-- =============================================================================
-- ROLES — coarse-grained (e.g. "PmAdmin"). v1 uses one implicit role.
-- =============================================================================
CREATE TABLE [identity].roles (
    id                  INT             IDENTITY(1,1) NOT NULL,
    name                NVARCHAR(256)   NULL,
    normalized_name     NVARCHAR(256)   NULL,
    concurrency_stamp   NVARCHAR(MAX)   NULL,
    CONSTRAINT pk_roles PRIMARY KEY CLUSTERED (id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX ux_roles_normalized_name
    ON [identity].roles (normalized_name)
    WHERE normalized_name IS NOT NULL;
GO


-- =============================================================================
-- USER ↔ ROLE association
-- =============================================================================
CREATE TABLE [identity].user_roles (
    user_id     INT NOT NULL,
    role_id     INT NOT NULL,
    CONSTRAINT pk_user_roles    PRIMARY KEY CLUSTERED (user_id, role_id),
    CONSTRAINT fk_user_roles_user FOREIGN KEY (user_id) REFERENCES [identity].users (id) ON DELETE CASCADE,
    CONSTRAINT fk_user_roles_role FOREIGN KEY (role_id) REFERENCES [identity].roles (id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX ix_user_roles_role ON [identity].user_roles (role_id);
GO


-- =============================================================================
-- ROLE CLAIMS
-- =============================================================================
CREATE TABLE [identity].role_claims (
    id              INT             IDENTITY(1,1) NOT NULL,
    role_id         INT             NOT NULL,
    claim_type      NVARCHAR(450)   NULL,
    claim_value     NVARCHAR(MAX)   NULL,
    CONSTRAINT pk_role_claims     PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_role_claims_role FOREIGN KEY (role_id) REFERENCES [identity].roles (id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX ix_role_claims_role ON [identity].role_claims (role_id);
GO


-- =============================================================================
-- USER CLAIMS
-- =============================================================================
CREATE TABLE [identity].user_claims (
    id              INT             IDENTITY(1,1) NOT NULL,
    user_id         INT             NOT NULL,
    claim_type      NVARCHAR(450)   NULL,
    claim_value     NVARCHAR(MAX)   NULL,
    CONSTRAINT pk_user_claims     PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_user_claims_user FOREIGN KEY (user_id) REFERENCES [identity].users (id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX ix_user_claims_user ON [identity].user_claims (user_id);
GO


-- =============================================================================
-- USER LOGINS — external auth providers (OAuth/OpenID)
-- =============================================================================
CREATE TABLE [identity].user_logins (
    login_provider          NVARCHAR(128)   NOT NULL,
    provider_key            NVARCHAR(128)   NOT NULL,
    provider_display_name   NVARCHAR(256)   NULL,
    user_id                 INT             NOT NULL,
    CONSTRAINT pk_user_logins      PRIMARY KEY CLUSTERED (login_provider, provider_key),
    CONSTRAINT fk_user_logins_user FOREIGN KEY (user_id) REFERENCES [identity].users (id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX ix_user_logins_user ON [identity].user_logins (user_id);
GO


-- =============================================================================
-- USER TOKENS — per-user provider tokens (2FA, refresh, etc.)
-- =============================================================================
CREATE TABLE [identity].user_tokens (
    user_id         INT             NOT NULL,
    login_provider  NVARCHAR(128)   NOT NULL,
    name            NVARCHAR(128)   NOT NULL,
    value           NVARCHAR(MAX)   NULL,
    CONSTRAINT pk_user_tokens      PRIMARY KEY CLUSTERED (user_id, login_provider, name),
    CONSTRAINT fk_user_tokens_user FOREIGN KEY (user_id) REFERENCES [identity].users (id) ON DELETE CASCADE
);
GO
