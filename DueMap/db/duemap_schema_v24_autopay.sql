-- =============================================================================
-- v24  Autopay status (P2-1)
-- =============================================================================
-- DueMap drives tenants to the accounting provider's OWN hosted autopay (money
-- never touches DueMap). Neither QBO nor Xero exposes a first-party
-- "enrolled?" flag via API (see the autopay-capabilities spike), so
-- autopay_status is an inferred proxy written during sync from invoice payment
-- behavior: a lease whose past-due-dated invoices are consistently paid is
-- treated as "enrolled" (reliable payer) and won't be nagged (P2-4).
--
--   autopay_status: 0 unknown, 1 none, 2 enrolled
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases') AND name = 'autopay_status')
BEGIN
    ALTER TABLE tenancy.leases
        ADD autopay_status TINYINT NOT NULL CONSTRAINT DF_leases_autopay_status DEFAULT (0);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases') AND name = 'autopay_checked_at')
BEGIN
    ALTER TABLE tenancy.leases ADD autopay_checked_at DATETIME2(3) NULL;
END;
GO
