-- =============================================================================
-- v22  SMS opt-out suppressions (P2-2)
-- =============================================================================
-- STOP/START handling for the SMS channel. A phone that texts STOP lands here;
-- TwilioSmsSender checks this table before every send and never texts an
-- opted-out number (texting an opted-out recipient is illegal in the US).
-- START/UNSTOP removes the row.
--
-- Keyed by normalized phone (digits only). pm_id is nullable for now — v1
-- shares one Twilio number across PMs, so STOP is effectively global to our
-- number; the column is here for the future per-PM-number case.
--
-- Lives in the integrations schema (the module that owns SMS dispatch), not
-- notices, so the sender enforces it without a new cross-module dependency.
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables
               WHERE object_id = OBJECT_ID(N'integrations.sms_suppressions'))
BEGIN
    CREATE TABLE integrations.sms_suppressions (
        id          INT IDENTITY(1,1) PRIMARY KEY,
        phone       VARCHAR(20)   NOT NULL,   -- normalized digits (E.164 without punctuation)
        pm_id       INT           NULL,
        reason      NVARCHAR(100) NOT NULL,
        created_at  DATETIME2(3)  NOT NULL CONSTRAINT DF_sms_suppressions_created_at DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE INDEX UX_sms_suppressions_phone
        ON integrations.sms_suppressions(phone);
END;
GO
