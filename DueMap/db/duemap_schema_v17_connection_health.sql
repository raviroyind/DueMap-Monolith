-- =============================================================================
-- DueMap schema v17 — accounting-connection health (ops cockpit P0-3)
--
-- Three columns on integrations.pm_accounting_connections track whether a
-- PM's accounting OAuth is currently usable:
--   * health_status     1 = Healthy   (normal — orchestrator processes this PM)
--                       2 = Degraded  (transient HTTP errors, intermittent)
--                       3 = Broken    (auth failure — refresh token revoked,
--                                      app uninstalled, scopes downgraded;
--                                      orchestrator SKIPS this PM until the
--                                      operator/PM reconnects)
--   * last_health_check timestamp of the most recent status update
--   * paused_reason     short human string explaining the Broken state,
--                       shown on the admin Connection Health board
--
-- The orchestrator's broken-skip behaviour is gated by feature flag
-- "ops.connection_health" (default OFF). When the flag is off, existing
-- behaviour is unchanged — the columns may still be populated, but the
-- skip logic doesn't fire.
--
-- Idempotent.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'integrations.pm_accounting_connections')
      AND name = N'health_status')
BEGIN
    ALTER TABLE integrations.pm_accounting_connections
        ADD health_status TINYINT NOT NULL
            CONSTRAINT DF_pm_acct_conn_health_status DEFAULT 1;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'integrations.pm_accounting_connections')
      AND name = N'last_health_check')
BEGIN
    ALTER TABLE integrations.pm_accounting_connections
        ADD last_health_check DATETIME2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'integrations.pm_accounting_connections')
      AND name = N'paused_reason')
BEGIN
    ALTER TABLE integrations.pm_accounting_connections
        ADD paused_reason NVARCHAR(200) NULL;
END
GO

-- Index supporting the admin board's "show me all unhealthy PMs" query.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_pm_acct_conn_health'
      AND object_id = OBJECT_ID(N'integrations.pm_accounting_connections'))
BEGIN
    CREATE INDEX IX_pm_acct_conn_health
        ON integrations.pm_accounting_connections(health_status)
        INCLUDE (property_manager_id, paused_reason, last_health_check)
        WHERE health_status <> 1;
END
GO
