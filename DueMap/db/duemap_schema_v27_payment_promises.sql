-- =============================================================================
-- DueMap schema v27 — promise-to-pay + Today work-queue dismissals
--
-- Promise-to-pay (task #112): the most common daily event in rent collection
-- is the tenant calling "I get paid Friday, I'll pay then." A logged promise
-- PAUSES that lease's notice sequence (AssessmentPlanner early-out) through
-- the promised date. The daily run then resolves it: overdue balance cleared
-- -> Kept; still owing -> Broken, and escalation resumes the same run. Every
-- transition lands in the lease activity timeline — "tenant promised twice
-- and broke both" is exactly the documentation an eviction filing needs.
--
-- work_item_dismissals (task #113): the /today queue lets the PM dismiss a
-- row ("seen it, not actionable"). Dismissals are keyed by a deterministic
-- item key (e.g. 'promise:12', 'delivery:345', 'expiry:lease:7') so the same
-- item never resurfaces after dismissal.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'tenancy.payment_promises', N'U') IS NULL
BEGIN
    CREATE TABLE tenancy.payment_promises
    (
        id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT pk_payment_promises PRIMARY KEY,
        property_manager_id INT            NOT NULL,
        lease_id            INT            NOT NULL,
        amount              DECIMAL(18,2)  NULL,          -- NULL = "the full balance"
        promised_date       DATE           NOT NULL,      -- pay-by date; pause runs through this day
        note                NVARCHAR(500)  NULL,          -- "agreed by phone", who called, etc.
        status              TINYINT        NOT NULL CONSTRAINT df_promise_status DEFAULT 1,
                                           -- 1=Active 2=Kept 3=Broken 4=Cancelled
        created_at          DATETIME2      NOT NULL,
        resolved_at         DATETIME2      NULL,          -- when Kept/Broken/Cancelled was stamped

        CONSTRAINT fk_promise_lease FOREIGN KEY (lease_id) REFERENCES tenancy.leases(id),
        CONSTRAINT fk_promise_pm    FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers(id),
        CONSTRAINT ck_promise_status CHECK (status IN (1, 2, 3, 4))
    );

    CREATE INDEX ix_promises_lease_status ON tenancy.payment_promises (lease_id, status);
    CREATE INDEX ix_promises_pm_status    ON tenancy.payment_promises (property_manager_id, status, promised_date);
END;

IF OBJECT_ID(N'tenancy.work_item_dismissals', N'U') IS NULL
BEGIN
    CREATE TABLE tenancy.work_item_dismissals
    (
        id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT pk_work_item_dismissals PRIMARY KEY,
        property_manager_id INT            NOT NULL,
        item_key            NVARCHAR(200)  NOT NULL,
        dismissed_at        DATETIME2      NOT NULL,

        CONSTRAINT fk_dismissal_pm FOREIGN KEY (property_manager_id) REFERENCES tenancy.property_managers(id),
        CONSTRAINT uq_dismissal    UNIQUE (property_manager_id, item_key)
    );
END;
