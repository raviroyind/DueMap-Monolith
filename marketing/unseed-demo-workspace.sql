/* ===========================================================================
   DueMap — remove the marketing demo workspace
   ---------------------------------------------------------------------------
   Deletes everything created by seed-demo-workspace.sql.

   Set @pm below to the property_manager_id the seed reported.
   Guarded: refuses to run against PM 1 (demo) or PM 15 (Xero sandbox).

   notice_deliveries is append-only, enforced by trg_nd_prevent_delete. This
   script disables that trigger for the delete and re-enables it in the same
   transaction. That protection exists because notice history is potential
   court evidence -- disabling it is defensible for a fabricated marketing
   workspace and nowhere else.
=========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @pm int = 17;          -- <<< set to the seeded PM id

IF @pm IN (1, 15)
BEGIN
    RAISERROR('Refusing to delete PM 1 (demo) or PM 15 (Xero sandbox).', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM tenancy.property_managers WHERE id = @pm)
BEGIN
    PRINT 'Nothing to do — no such property manager.';
    RETURN;
END

BEGIN TRAN;

DISABLE TRIGGER notices.trg_nd_prevent_delete ON notices.notice_deliveries;

DELETE FROM notices.notice_deliveries       WHERE property_manager_id = @pm;

ENABLE TRIGGER notices.trg_nd_prevent_delete ON notices.notice_deliveries;

DELETE a FROM billing.late_fee_assessments a
    JOIN tenancy.leases l ON l.id = a.lease_id AND l.property_manager_id = @pm;

DELETE FROM tenancy.payment_promises         WHERE property_manager_id = @pm;
DELETE FROM tenancy.rent_invoices            WHERE property_manager_id = @pm;

DELETE s FROM tenancy.lease_notice_settings s
    JOIN tenancy.leases l ON l.id = s.lease_id AND l.property_manager_id = @pm;

DELETE FROM tenancy.leases                   WHERE property_manager_id = @pm;
DELETE FROM tenancy.customers                WHERE property_manager_id = @pm;
DELETE FROM billing.pm_processing_runs       WHERE property_manager_id = @pm;
DELETE FROM integrations.pm_accounting_connections WHERE property_manager_id = @pm;
DELETE FROM tenancy.pm_notice_preferences    WHERE property_manager_id = @pm;
DELETE FROM tenancy.pm_daily_close_settings  WHERE property_manager_id = @pm;
DELETE FROM tenancy.pm_accounting_defaults   WHERE property_manager_id = @pm;
DELETE FROM tenancy.work_item_dismissals     WHERE property_manager_id = @pm;
DELETE FROM notices.pm_template_overrides    WHERE property_manager_id = @pm;

DELETE FROM [identity].[user_claims]
    WHERE user_id IN (SELECT id FROM [identity].[users] WHERE property_manager_id = @pm);
DELETE FROM [identity].[user_logins]
    WHERE user_id IN (SELECT id FROM [identity].[users] WHERE property_manager_id = @pm);
DELETE FROM [identity].[user_tokens]
    WHERE user_id IN (SELECT id FROM [identity].[users] WHERE property_manager_id = @pm);
DELETE FROM [identity].[user_roles]
    WHERE user_id IN (SELECT id FROM [identity].[users] WHERE property_manager_id = @pm);
DELETE FROM [identity].[users]               WHERE property_manager_id = @pm;

DELETE FROM tenancy.property_managers        WHERE id = @pm;

COMMIT;

PRINT 'Demo workspace removed.';
