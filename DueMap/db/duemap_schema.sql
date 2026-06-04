-- =============================================================================
-- Late Fee SaaS — Database Schema
-- Target: SQL Server 2017+
--
-- Contains:
--   rules.*    Rules engine (states, time-versioned rules, local overrides)
--   notices.*  Notice template system and immutable delivery audit trail
--   tenancy.*  Minimal property manager + lease tables (just enough for FKs)
--
-- Conventions:
--   - snake_case names; schemas group by concern
--   - DATETIME2(7) for timestamps; DATE for calendar concepts
--   - NVARCHAR throughout (Unicode-safe by default)
--   - Filtered unique indexes enforce "one current X" invariants
--   - INSTEAD OF triggers enforce append-only / immutable content
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- =============================================================================
-- SCHEMAS
-- =============================================================================
IF SCHEMA_ID(N'rules')    IS NULL EXEC(N'CREATE SCHEMA rules    AUTHORIZATION dbo;');
IF SCHEMA_ID(N'notices')  IS NULL EXEC(N'CREATE SCHEMA notices  AUTHORIZATION dbo;');
IF SCHEMA_ID(N'tenancy')  IS NULL EXEC(N'CREATE SCHEMA tenancy  AUTHORIZATION dbo;');
GO


-- =============================================================================
-- RULES ENGINE
-- =============================================================================

-- US states and DC. Seeded below.
CREATE TABLE rules.states (
    id          INT             IDENTITY(1,1) NOT NULL,
    code        CHAR(2)         NOT NULL,
    name        NVARCHAR(80)    NOT NULL,
    created_at  DATETIME2(7)    NOT NULL CONSTRAINT df_states_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_states            PRIMARY KEY CLUSTERED (id),
    CONSTRAINT uq_states_code       UNIQUE (code),
    CONSTRAINT ck_states_code_upper CHECK (code = UPPER(code))
);
GO

-- Time-versioned state-level rules.
-- INVARIANT: New rule = new row with a new effective_date.
-- The previous version's expires_date gets set on insertion of the new one.
-- NEVER UPDATE this table's rule fields directly — even to fix a typo, supersede the row.
CREATE TABLE rules.state_rule_versions (
    id                          INT             IDENTITY(1,1) NOT NULL,
    state_id                    INT             NOT NULL,
    effective_date              DATE            NOT NULL,
    expires_date                DATE            NULL,            -- NULL = currently in effect
    grace_period_days           INT             NOT NULL,        -- legal grace before fee may be assessed
    late_fee_type               VARCHAR(20)     NOT NULL,        -- flat | percent | greater_of | lesser_of
    flat_amount                 DECIMAL(10, 2)  NULL,
    percent_of_rent             DECIMAL(5, 4)   NULL,            -- e.g. 0.0500 = 5%
    hard_cap_amount             DECIMAL(10, 2)  NULL,            -- absolute ceiling regardless of % calc
    notice_required_before_fee  BIT             NOT NULL CONSTRAINT df_srv_notice_req DEFAULT (0),
    notice_advance_days         INT             NULL,            -- if notice required, how many days before
    source_citation             NVARCHAR(500)   NOT NULL,        -- e.g. 'Cal. Civ. Code § 1671(d)'
    notes                       NVARCHAR(MAX)   NULL,
    created_at                  DATETIME2(7)    NOT NULL CONSTRAINT df_srv_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_state_rule_versions PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_srv_state           FOREIGN KEY (state_id) REFERENCES rules.states (id),
    CONSTRAINT ck_srv_dates           CHECK (expires_date IS NULL OR expires_date > effective_date),
    CONSTRAINT ck_srv_late_fee_type   CHECK (late_fee_type IN ('flat', 'percent', 'greater_of', 'lesser_of')),
    CONSTRAINT ck_srv_grace_period    CHECK (grace_period_days BETWEEN 0 AND 60),
    CONSTRAINT ck_srv_percent_range   CHECK (percent_of_rent IS NULL OR percent_of_rent BETWEEN 0 AND 1),
    CONSTRAINT ck_srv_amounts_positive CHECK (
            (flat_amount       IS NULL OR flat_amount       >= 0)
        AND (hard_cap_amount   IS NULL OR hard_cap_amount   >= 0)
    ),
    -- Type-specific required fields
    CONSTRAINT ck_srv_fee_values CHECK (
            (late_fee_type = 'flat'    AND flat_amount     IS NOT NULL)
         OR (late_fee_type = 'percent' AND percent_of_rent IS NOT NULL)
         OR (late_fee_type IN ('greater_of', 'lesser_of')
                AND flat_amount     IS NOT NULL
                AND percent_of_rent IS NOT NULL)
    ),
    CONSTRAINT ck_srv_notice_advance CHECK (
        notice_required_before_fee = 0
        OR (notice_required_before_fee = 1 AND notice_advance_days IS NOT NULL AND notice_advance_days > 0)
    )
);
GO

-- Hot path: resolve "what rule applies for state S on date D".
-- Covering index so the resolution query is a single seek, no key lookup.
CREATE NONCLUSTERED INDEX ix_srv_state_effective
    ON rules.state_rule_versions (state_id, effective_date DESC)
    INCLUDE (expires_date, grace_period_days, late_fee_type, flat_amount,
             percent_of_rent, hard_cap_amount, notice_required_before_fee, notice_advance_days);
GO

-- Invariant: at most one currently-active rule per state.
CREATE UNIQUE NONCLUSTERED INDEX ux_srv_one_current_per_state
    ON rules.state_rule_versions (state_id)
    WHERE expires_date IS NULL;
GO

-- City- or county-level jurisdictions with their own rules (e.g. San Francisco, NYC).
CREATE TABLE rules.local_jurisdictions (
    id                  INT             IDENTITY(1,1) NOT NULL,
    state_id            INT             NOT NULL,
    name                NVARCHAR(120)   NOT NULL,
    jurisdiction_type   VARCHAR(20)     NOT NULL,    -- city | county | borough | parish
    fips_code           VARCHAR(10)     NULL,        -- US Census FIPS code
    created_at          DATETIME2(7)    NOT NULL CONSTRAINT df_lj_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_local_jurisdictions PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_lj_state            FOREIGN KEY (state_id) REFERENCES rules.states (id),
    CONSTRAINT ck_lj_type             CHECK (jurisdiction_type IN ('city', 'county', 'borough', 'parish')),
    CONSTRAINT uq_lj_state_name_type  UNIQUE (state_id, name, jurisdiction_type)
);
GO

CREATE NONCLUSTERED INDEX ix_lj_state ON rules.local_jurisdictions (state_id);
GO

-- Field-level overrides where local law is stricter than the state base rule.
-- A NULL column means "no override; inherit from state".
CREATE TABLE rules.local_rule_overrides (
    id                          INT             IDENTITY(1,1) NOT NULL,
    jurisdiction_id             INT             NOT NULL,
    effective_date              DATE            NOT NULL,
    expires_date                DATE            NULL,
    grace_period_days           INT             NULL,
    flat_amount                 DECIMAL(10, 2)  NULL,
    percent_of_rent             DECIMAL(5, 4)   NULL,
    hard_cap_amount             DECIMAL(10, 2)  NULL,
    notice_advance_days         INT             NULL,
    source_citation             NVARCHAR(500)   NOT NULL,
    notes                       NVARCHAR(MAX)   NULL,
    created_at                  DATETIME2(7)    NOT NULL CONSTRAINT df_lro_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_local_rule_overrides  PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_lro_jurisdiction      FOREIGN KEY (jurisdiction_id) REFERENCES rules.local_jurisdictions (id),
    CONSTRAINT ck_lro_dates             CHECK (expires_date IS NULL OR expires_date > effective_date),
    CONSTRAINT ck_lro_percent_range     CHECK (percent_of_rent IS NULL OR percent_of_rent BETWEEN 0 AND 1),
    -- At least one field must actually override something
    CONSTRAINT ck_lro_has_override CHECK (
            grace_period_days   IS NOT NULL
         OR flat_amount         IS NOT NULL
         OR percent_of_rent     IS NOT NULL
         OR hard_cap_amount     IS NOT NULL
         OR notice_advance_days IS NOT NULL
    )
);
GO

CREATE NONCLUSTERED INDEX ix_lro_jurisdiction_effective
    ON rules.local_rule_overrides (jurisdiction_id, effective_date DESC)
    INCLUDE (expires_date, grace_period_days, flat_amount, percent_of_rent, hard_cap_amount, notice_advance_days);
GO


-- =============================================================================
-- TENANCY (minimal — full model out of scope)
-- =============================================================================

-- A SaaS customer (the property management company).
CREATE TABLE tenancy.property_managers (
    id          INT             IDENTITY(1,1) NOT NULL,
    name        NVARCHAR(200)   NOT NULL,
    created_at  DATETIME2(7)    NOT NULL CONSTRAINT df_pm_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_property_managers PRIMARY KEY CLUSTERED (id)
);
GO

-- Skeleton lease — real schema would carry unit_id, tenant_id, terms, etc.
CREATE TABLE tenancy.leases (
    id                  INT             IDENTITY(1,1) NOT NULL,
    property_manager_id INT             NOT NULL,
    state_id            INT             NOT NULL,
    jurisdiction_id     INT             NULL,
    monthly_rent        DECIMAL(10, 2)  NOT NULL,
    start_date          DATE            NOT NULL,
    end_date            DATE            NULL,
    created_at          DATETIME2(7)    NOT NULL CONSTRAINT df_lease_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_leases               PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_lease_pm             FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT fk_lease_state          FOREIGN KEY (state_id)            REFERENCES rules.states (id),
    CONSTRAINT fk_lease_jurisdiction   FOREIGN KEY (jurisdiction_id)     REFERENCES rules.local_jurisdictions (id),
    CONSTRAINT ck_lease_dates          CHECK (end_date IS NULL OR end_date >= start_date),
    CONSTRAINT ck_lease_rent_positive  CHECK (monthly_rent > 0)
);
GO

CREATE NONCLUSTERED INDEX ix_lease_pm ON tenancy.leases (property_manager_id);
GO


-- =============================================================================
-- NOTICE TEMPLATE SYSTEM
-- =============================================================================

-- Catalog of notice kinds. Seeded below.
CREATE TABLE notices.notice_types (
    id              INT             IDENTITY(1,1) NOT NULL,
    code            VARCHAR(40)     NOT NULL,
    display_name    NVARCHAR(120)   NOT NULL,
    legal_priority  INT             NOT NULL,   -- ordering hint: reminder < late_notice < pay_or_quit
    created_at      DATETIME2(7)    NOT NULL CONSTRAINT df_nt_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_notice_types     PRIMARY KEY CLUSTERED (id),
    CONSTRAINT uq_notice_types_code UNIQUE (code)
);
GO

-- A template = "the notice of type X for state S (or generic if state_id IS NULL)".
-- Versioned content lives in notice_template_versions.
CREATE TABLE notices.notice_templates (
    id              INT             IDENTITY(1,1) NOT NULL,
    state_id        INT             NULL,    -- NULL = generic fallback template
    notice_type_id  INT             NOT NULL,
    is_active       BIT             NOT NULL CONSTRAINT df_ntpl_active DEFAULT (1),
    created_at      DATETIME2(7)    NOT NULL CONSTRAINT df_ntpl_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_notice_templates  PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_ntpl_state        FOREIGN KEY (state_id)       REFERENCES rules.states (id),
    CONSTRAINT fk_ntpl_type         FOREIGN KEY (notice_type_id) REFERENCES notices.notice_types (id)
);
GO

-- At most one active template per (state, notice_type).
CREATE UNIQUE NONCLUSTERED INDEX ux_ntpl_state_type_active
    ON notices.notice_templates (state_id, notice_type_id)
    WHERE is_active = 1 AND state_id IS NOT NULL;
GO

-- And at most one active generic (state_id IS NULL) template per notice_type.
CREATE UNIQUE NONCLUSTERED INDEX ux_ntpl_generic_type_active
    ON notices.notice_templates (notice_type_id)
    WHERE is_active = 1 AND state_id IS NULL;
GO

-- Versioned template content. Becomes immutable once referenced by a delivery.
CREATE TABLE notices.notice_template_versions (
    id                  INT             IDENTITY(1,1) NOT NULL,
    template_id         INT             NOT NULL,
    version_number      INT             NOT NULL,
    subject             NVARCHAR(400)   NOT NULL,
    body_html           NVARCHAR(MAX)   NOT NULL,
    body_text           NVARCHAR(MAX)   NOT NULL,
    required_vars       NVARCHAR(MAX)   NOT NULL,    -- JSON array, e.g. ["tenant_name","amount_owed"]
    effective_date      DATE            NOT NULL,
    approved_by         NVARCHAR(200)   NULL,        -- legal reviewer
    status              VARCHAR(20)     NOT NULL CONSTRAINT df_ntv_status DEFAULT ('draft'),
    created_at          DATETIME2(7)    NOT NULL CONSTRAINT df_ntv_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_notice_template_versions  PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_ntv_template              FOREIGN KEY (template_id) REFERENCES notices.notice_templates (id),
    CONSTRAINT uq_ntv_template_version      UNIQUE (template_id, version_number),
    CONSTRAINT ck_ntv_required_vars_json    CHECK (ISJSON(required_vars) = 1),
    CONSTRAINT ck_ntv_status                CHECK (status IN ('draft', 'approved', 'superseded'))
);
GO

CREATE NONCLUSTERED INDEX ix_ntv_template_effective
    ON notices.notice_template_versions (template_id, effective_date DESC)
    INCLUDE (status);
GO


-- =============================================================================
-- DELIVERY AUDIT TRAIL
-- =============================================================================

-- The legal record of every notice sent.
-- Content columns are IMMUTABLE — triggers below enforce this.
-- Only status / delivery-tracking fields may be updated (provider webhooks).
CREATE TABLE notices.notice_deliveries (
    id                      BIGINT          IDENTITY(1,1) NOT NULL,
    property_manager_id     INT             NOT NULL,        -- denormalized for tenant-scoped indexing
    lease_id                INT             NOT NULL,
    template_version_id     INT             NOT NULL,
    rendered_subject        NVARCHAR(400)   NOT NULL,        -- snapshot of what was sent
    rendered_body_html      NVARCHAR(MAX)   NOT NULL,
    rendered_body_text      NVARCHAR(MAX)   NOT NULL,
    channel                 VARCHAR(20)     NOT NULL,
    sent_at                 DATETIME2(7)    NOT NULL,
    provider_msg_id         NVARCHAR(200)   NULL,            -- SendGrid / Twilio message id
    status                  VARCHAR(20)     NOT NULL,
    delivered_at            DATETIME2(7)    NULL,
    opened_at               DATETIME2(7)    NULL,
    bounced_at              DATETIME2(7)    NULL,
    created_at              DATETIME2(7)    NOT NULL CONSTRAINT df_nd_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_notice_deliveries       PRIMARY KEY CLUSTERED (id),
    CONSTRAINT fk_nd_pm                   FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers (id),
    CONSTRAINT fk_nd_lease                FOREIGN KEY (lease_id)            REFERENCES tenancy.leases (id),
    CONSTRAINT fk_nd_template_version     FOREIGN KEY (template_version_id) REFERENCES notices.notice_template_versions (id),
    CONSTRAINT ck_nd_channel              CHECK (channel IN ('email', 'sms', 'certified_mail', 'in_app')),
    CONSTRAINT ck_nd_status               CHECK (status  IN ('queued', 'sent', 'delivered', 'bounced', 'failed'))
);
GO

CREATE NONCLUSTERED INDEX ix_nd_pm_sent_at
    ON notices.notice_deliveries (property_manager_id, sent_at DESC);
GO

CREATE NONCLUSTERED INDEX ix_nd_lease_sent_at
    ON notices.notice_deliveries (lease_id, sent_at DESC);
GO

CREATE NONCLUSTERED INDEX ix_nd_template_version
    ON notices.notice_deliveries (template_version_id);
GO


-- =============================================================================
-- IMMUTABILITY TRIGGERS
-- =============================================================================

-- Allow updates to delivery-tracking fields; block changes to content/identity.
CREATE OR ALTER TRIGGER notices.trg_nd_prevent_content_mutation
ON notices.notice_deliveries
INSTEAD OF UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN deleted d ON i.id = d.id
        WHERE i.rendered_subject    <> d.rendered_subject
           OR i.rendered_body_html  <> d.rendered_body_html
           OR i.rendered_body_text  <> d.rendered_body_text
           OR i.lease_id            <> d.lease_id
           OR i.property_manager_id <> d.property_manager_id
           OR i.template_version_id <> d.template_version_id
           OR i.channel             <> d.channel
           OR i.sent_at             <> d.sent_at
    )
    BEGIN
        THROW 51001,
            'notice_deliveries content fields are immutable. Only status, delivered_at, opened_at, bounced_at, and provider_msg_id may be updated.',
            1;
    END

    UPDATE n
       SET status          = i.status,
           delivered_at    = i.delivered_at,
           opened_at       = i.opened_at,
           bounced_at      = i.bounced_at,
           provider_msg_id = i.provider_msg_id
      FROM notices.notice_deliveries n
INNER JOIN inserted i ON n.id = i.id;
END;
GO

-- notice_deliveries is append-only at the data layer.
CREATE OR ALTER TRIGGER notices.trg_nd_prevent_delete
ON notices.notice_deliveries
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51002, 'notice_deliveries is append-only. DELETE is not permitted.', 1;
END;
GO

-- Template version content freezes once any delivery references it.
-- Status, approved_by, and effective_date may still be edited (draft -> approved -> superseded).
CREATE OR ALTER TRIGGER notices.trg_ntv_prevent_used_mutation
ON notices.notice_template_versions
INSTEAD OF UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN deleted  d  ON i.id = d.id
        WHERE EXISTS (SELECT 1 FROM notices.notice_deliveries nd WHERE nd.template_version_id = i.id)
          AND (   i.subject       <> d.subject
               OR i.body_html     <> d.body_html
               OR i.body_text     <> d.body_text
               OR i.required_vars <> d.required_vars
               OR i.template_id   <> d.template_id
               OR i.version_number <> d.version_number)
    )
    BEGIN
        THROW 51003,
            'Template version content cannot be modified once it has been used in a delivery. Create a new version instead.',
            1;
    END

    UPDATE n
       SET template_id    = i.template_id,
           version_number = i.version_number,
           subject        = i.subject,
           body_html      = i.body_html,
           body_text      = i.body_text,
           required_vars  = i.required_vars,
           effective_date = i.effective_date,
           approved_by    = i.approved_by,
           status         = i.status
      FROM notices.notice_template_versions n
INNER JOIN inserted i ON n.id = i.id;
END;
GO


-- =============================================================================
-- SEED DATA
-- =============================================================================

INSERT INTO rules.states (code, name) VALUES
    ('AL', N'Alabama'),        ('AK', N'Alaska'),         ('AZ', N'Arizona'),         ('AR', N'Arkansas'),
    ('CA', N'California'),     ('CO', N'Colorado'),       ('CT', N'Connecticut'),     ('DE', N'Delaware'),
    ('DC', N'District of Columbia'),
    ('FL', N'Florida'),        ('GA', N'Georgia'),        ('HI', N'Hawaii'),          ('ID', N'Idaho'),
    ('IL', N'Illinois'),       ('IN', N'Indiana'),        ('IA', N'Iowa'),            ('KS', N'Kansas'),
    ('KY', N'Kentucky'),       ('LA', N'Louisiana'),      ('ME', N'Maine'),           ('MD', N'Maryland'),
    ('MA', N'Massachusetts'),  ('MI', N'Michigan'),       ('MN', N'Minnesota'),       ('MS', N'Mississippi'),
    ('MO', N'Missouri'),       ('MT', N'Montana'),        ('NE', N'Nebraska'),        ('NV', N'Nevada'),
    ('NH', N'New Hampshire'),  ('NJ', N'New Jersey'),     ('NM', N'New Mexico'),      ('NY', N'New York'),
    ('NC', N'North Carolina'), ('ND', N'North Dakota'),   ('OH', N'Ohio'),            ('OK', N'Oklahoma'),
    ('OR', N'Oregon'),         ('PA', N'Pennsylvania'),   ('RI', N'Rhode Island'),    ('SC', N'South Carolina'),
    ('SD', N'South Dakota'),   ('TN', N'Tennessee'),      ('TX', N'Texas'),           ('UT', N'Utah'),
    ('VT', N'Vermont'),        ('VA', N'Virginia'),       ('WA', N'Washington'),      ('WV', N'West Virginia'),
    ('WI', N'Wisconsin'),      ('WY', N'Wyoming');
GO

INSERT INTO notices.notice_types (code, display_name, legal_priority) VALUES
    ('rent_reminder',     N'Rent payment reminder',      10),
    ('late_notice',       N'Late payment notice',        20),
    ('late_fee_assessed', N'Late fee assessment notice', 30),
    ('pay_or_quit',       N'Pay or quit notice',         40),
    ('eviction_warning',  N'Eviction warning',           50);
GO


-- =============================================================================
-- EXAMPLE: rule resolution query for SQL Server
-- (Move to a stored procedure / Dapper query in real code)
-- =============================================================================
/*
DECLARE @state_id        INT  = (SELECT id FROM rules.states WHERE code = 'CA');
DECLARE @jurisdiction_id INT  = NULL;
DECLARE @as_of           DATE = SYSUTCDATETIME();

WITH base AS (
    SELECT TOP (1) *
      FROM rules.state_rule_versions
     WHERE state_id = @state_id
       AND effective_date <= @as_of
       AND (expires_date IS NULL OR expires_date > @as_of)
     ORDER BY effective_date DESC
),
ovr AS (
    SELECT TOP (1) *
      FROM rules.local_rule_overrides
     WHERE jurisdiction_id = @jurisdiction_id
       AND effective_date <= @as_of
       AND (expires_date IS NULL OR expires_date > @as_of)
     ORDER BY effective_date DESC
)
SELECT
    base.state_id,
    -- stricter-of merge: smaller grace, smaller cap; explicit override wins for fees
    COALESCE(ovr.grace_period_days,   base.grace_period_days)   AS grace_period_days,
    COALESCE(ovr.flat_amount,         base.flat_amount)         AS flat_amount,
    COALESCE(ovr.percent_of_rent,     base.percent_of_rent)     AS percent_of_rent,
    COALESCE(ovr.hard_cap_amount,     base.hard_cap_amount)     AS hard_cap_amount,
    COALESCE(ovr.notice_advance_days, base.notice_advance_days) AS notice_advance_days,
    base.late_fee_type,
    base.notice_required_before_fee
FROM base
LEFT JOIN ovr ON 1 = 1;
*/
