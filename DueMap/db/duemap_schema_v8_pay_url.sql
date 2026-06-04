-- =============================================================================
-- DueMap schema v8 — public payment URL on rent invoices
--
-- Adds tenancy.rent_invoices.public_payment_url to carry the provider's
-- pay-by-link URL (QBO InvoiceLink, Xero OnlineInvoiceUrl) so the notice
-- template's Pay Now button has a real destination.
--
-- Nullable: not every PM has invoice payments enabled in QuickBooks or
-- Xero. Empty/null means "render the Pay Now button hidden" — handled by
-- a defensive {% if pay_url %} block in the template body.
--
-- Idempotent: skipped if the column already exists.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'tenancy.rent_invoices')
      AND name = N'public_payment_url')
BEGIN
    ALTER TABLE tenancy.rent_invoices
        ADD public_payment_url NVARCHAR(500) NULL;
END
GO
