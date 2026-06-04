-- =============================================================================
-- DueMap schema v15 — tenant portal
--
-- Three new tables under [tenancy] for the tenant-facing portal at /t/*.
--
--   tenant_logins        One row per (PM, email). Optional password_hash for
--                        tenants who opt into a password after their first
--                        magic-link sign-in.
--   tenant_magic_links   Outstanding sign-in tokens. Tokens are NEVER stored
--                        in cleartext — only the SHA-256 hash. Single-use,
--                        ~15-min TTL.
--   tenant_sessions      Active session cookies. Cookie value is opaque
--                        random bytes; only the hash is persisted. 30-day
--                        sliding TTL; revoked rows kept for audit.
--
-- All three tables relate UP to property_managers + customers so tenant
-- scoping is enforceable at the FK level.
--
-- Idempotent.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- 1. tenant_logins ------------------------------------------------------------
IF OBJECT_ID(N'tenancy.tenant_logins', N'U') IS NULL
BEGIN
    CREATE TABLE tenancy.tenant_logins (
        id                    INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_tenant_logins PRIMARY KEY,
        property_manager_id   INT NOT NULL,
        customer_id           INT NOT NULL,
        email                 NVARCHAR(400) NOT NULL,
        password_hash         NVARCHAR(500) NULL,    -- ASP.NET Core PasswordHasher format; null = magic-link-only
        created_at            DATETIME2 NOT NULL CONSTRAINT DF_tenant_logins_created DEFAULT SYSUTCDATETIME(),
        last_login_at         DATETIME2 NULL,
        CONSTRAINT FK_tenant_logins_pm FOREIGN KEY (property_manager_id)
            REFERENCES tenancy.property_managers(id),
        CONSTRAINT FK_tenant_logins_customer FOREIGN KEY (customer_id)
            REFERENCES tenancy.customers(id)
    );

    -- One login per (PM, email). Different PMs may legitimately have the same
    -- tenant email — the sign-in flow disambiguates by listing matches when
    -- more than one PM owns the email.
    CREATE UNIQUE INDEX UX_tenant_logins_pm_email
        ON tenancy.tenant_logins(property_manager_id, email);

    -- Cross-PM lookup at sign-in time.
    CREATE INDEX IX_tenant_logins_email
        ON tenancy.tenant_logins(email)
        INCLUDE (property_manager_id, customer_id);
END
GO

-- 2. tenant_magic_links -------------------------------------------------------
IF OBJECT_ID(N'tenancy.tenant_magic_links', N'U') IS NULL
BEGIN
    CREATE TABLE tenancy.tenant_magic_links (
        id                    BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_tenant_magic_links PRIMARY KEY,
        -- SHA-256 of the raw token, hex-encoded (64 chars). Raw token only
        -- exists in transit in the email body — never on disk.
        token_hash            NVARCHAR(128) NOT NULL,
        tenant_login_id       INT NOT NULL,
        requested_at          DATETIME2 NOT NULL CONSTRAINT DF_tenant_ml_requested DEFAULT SYSUTCDATETIME(),
        expires_at            DATETIME2 NOT NULL,
        consumed_at           DATETIME2 NULL,
        request_ip            NVARCHAR(64)  NULL,    -- audit only
        consume_ip            NVARCHAR(64)  NULL,
        CONSTRAINT FK_tenant_ml_login FOREIGN KEY (tenant_login_id)
            REFERENCES tenancy.tenant_logins(id)
    );

    -- Lookup by hash on redemption. Unique to defend against the
    -- vanishingly-unlikely SHA-256 collision and reused-token races.
    CREATE UNIQUE INDEX UX_tenant_ml_hash
        ON tenancy.tenant_magic_links(token_hash);

    -- Rate-limit & abuse audit queries.
    CREATE INDEX IX_tenant_ml_login_requested
        ON tenancy.tenant_magic_links(tenant_login_id, requested_at DESC);
END
GO

-- 3. tenant_sessions ----------------------------------------------------------
IF OBJECT_ID(N'tenancy.tenant_sessions', N'U') IS NULL
BEGIN
    CREATE TABLE tenancy.tenant_sessions (
        id                    BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_tenant_sessions PRIMARY KEY,
        -- SHA-256 of the cookie value. Cookie itself is high-entropy random
        -- bytes, set HttpOnly + Secure + SameSite=Lax + Path=/t.
        cookie_hash           NVARCHAR(128) NOT NULL,
        tenant_login_id       INT NOT NULL,
        created_at            DATETIME2 NOT NULL CONSTRAINT DF_tenant_sess_created DEFAULT SYSUTCDATETIME(),
        last_seen_at          DATETIME2 NOT NULL CONSTRAINT DF_tenant_sess_seen    DEFAULT SYSUTCDATETIME(),
        expires_at            DATETIME2 NOT NULL,
        revoked_at            DATETIME2 NULL,
        created_ip            NVARCHAR(64) NULL,
        CONSTRAINT FK_tenant_sess_login FOREIGN KEY (tenant_login_id)
            REFERENCES tenancy.tenant_logins(id)
    );

    -- Cookie-hash lookup on every authenticated request. Filtered so revoked
    -- rows don't bloat the index.
    CREATE UNIQUE INDEX UX_tenant_sess_hash_active
        ON tenancy.tenant_sessions(cookie_hash)
        WHERE revoked_at IS NULL;

    -- Per-tenant session list (for a future "your active sessions" panel
    -- and for revoke-all-on-password-change flows).
    CREATE INDEX IX_tenant_sess_login_active
        ON tenancy.tenant_sessions(tenant_login_id)
        WHERE revoked_at IS NULL;
END
GO
