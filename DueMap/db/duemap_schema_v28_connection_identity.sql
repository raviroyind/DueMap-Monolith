-- =============================================================================
-- DueMap schema v28 — accounting-connection identity + disconnect telemetry
--
-- company_name: the QBO/Xero organisation name, captured at connect time and
-- backfilled by the next sync for existing connections. Replaces the raw
-- realm id as the human-facing identity (dashboard chip, topbar, sidebar).
--
-- disconnect_reason / disconnected_at: the Disconnect flow now asks (optional,
-- free-text) why the PM is disconnecting — product telemetry and support
-- context. Reconnecting clears both.
--
-- Realm binding note: no schema change needed for the one-realm-per-workspace
-- guard — the existing row (kept on disconnect, status='disconnected')
-- already pins the realm; AccountingConnectionService now refuses to hydrate
-- a different realm/provider over it.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH('integrations.pm_accounting_connections', 'company_name') IS NULL
BEGIN
    ALTER TABLE integrations.pm_accounting_connections
        ADD company_name NVARCHAR(200) NULL;
END;

IF COL_LENGTH('integrations.pm_accounting_connections', 'disconnect_reason') IS NULL
BEGIN
    ALTER TABLE integrations.pm_accounting_connections
        ADD disconnect_reason NVARCHAR(500) NULL;
END;

IF COL_LENGTH('integrations.pm_accounting_connections', 'disconnected_at') IS NULL
BEGIN
    ALTER TABLE integrations.pm_accounting_connections
        ADD disconnected_at DATETIME2 NULL;
END;
