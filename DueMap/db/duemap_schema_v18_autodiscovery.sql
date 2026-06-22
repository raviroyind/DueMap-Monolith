-- =============================================================================
-- v18  Auto-discovery (P1-1)
-- =============================================================================
-- Adds the columns DiscoveryService writes to (per-lease inferred values + the
-- one-tap confirm stamp) plus the billing-state column on customers so the
-- engine has something to read on real syncs.
--
-- Append-only convention: all ALTERs are guarded with NOT EXISTS so re-running
-- this script on a partially-applied DB is a no-op. Schema bootstrapper
-- (DevSchemaBootstrap) applies these idempotently in Dev.
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;

-- ----------------------------------------------------------------------------
-- tenancy.leases — inferred_* + discovery_confirmed_at
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'inferred_rent_amount')
BEGIN
    ALTER TABLE tenancy.leases
        ADD inferred_rent_amount DECIMAL(18,2) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'inferred_due_day')
BEGIN
    ALTER TABLE tenancy.leases
        ADD inferred_due_day TINYINT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'inferred_state')
BEGIN
    ALTER TABLE tenancy.leases
        ADD inferred_state CHAR(2) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'inferred_at')
BEGIN
    ALTER TABLE tenancy.leases
        ADD inferred_at DATETIME2(3) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.leases')
                 AND name = 'discovery_confirmed_at')
BEGIN
    ALTER TABLE tenancy.leases
        ADD discovery_confirmed_at DATETIME2(3) NULL;
END;

-- ----------------------------------------------------------------------------
-- tenancy.customers — billing_state
--
-- The QBO/Xero sync populates this from the customer's billing address
-- (QBO BillAddr.CountrySubDivisionCode / Xero Address.Region) so the
-- DiscoveryService can map a lease to its state without a manual entry.
-- Two-letter, uppercase by convention; NULL when the address has no region.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'tenancy.customers')
                 AND name = 'billing_state')
BEGIN
    ALTER TABLE tenancy.customers
        ADD billing_state CHAR(2) NULL;
END;
