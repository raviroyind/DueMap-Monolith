-- =============================================================================
-- DueMap schema v11 — structured worker logs
--
-- Creates schema [logs] and table logs.events that the Worker process writes
-- to via Serilog.Sinks.MSSqlServer. Each log line carries the standard Serilog
-- columns plus three domain-specific ones populated from LogContext properties:
--   pm_id          (which PM the line is about, when in a per-PM context)
--   run_id         (which pm_processing_runs row, when inside an orchestrator run)
--   business_date  (the PM's local date that was being processed)
--
-- Indexes target the admin-dashboard query shapes:
--   * by timestamp descending (default sort)
--   * by pm_id + timestamp (drill into one PM)
--   * by run_id (drill into one nightly run)
--
-- Idempotent.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'logs')
BEGIN
    EXEC(N'CREATE SCHEMA logs');
END
GO

IF OBJECT_ID(N'logs.events', N'U') IS NULL
BEGIN
    CREATE TABLE logs.events (
        id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_logs_events PRIMARY KEY,
        timestamp       DATETIME2(3) NOT NULL,
        level           NVARCHAR(20) NOT NULL,
        message         NVARCHAR(MAX) NULL,
        exception       NVARCHAR(MAX) NULL,
        properties      NVARCHAR(MAX) NULL,        -- JSON of all enriched properties
        log_event       NVARCHAR(MAX) NULL,        -- full serialised event for forensics
        pm_id           INT          NULL,
        run_id          BIGINT       NULL,
        business_date   DATE         NULL,
        source_context  NVARCHAR(400) NULL          -- which logger emitted it (class name)
    );

    CREATE INDEX IX_logs_events_timestamp
        ON logs.events(timestamp DESC);

    CREATE INDEX IX_logs_events_pm_ts
        ON logs.events(pm_id, timestamp DESC)
        WHERE pm_id IS NOT NULL;

    CREATE INDEX IX_logs_events_run
        ON logs.events(run_id)
        WHERE run_id IS NOT NULL;

    CREATE INDEX IX_logs_events_level_ts
        ON logs.events(level, timestamp DESC);
END
GO
