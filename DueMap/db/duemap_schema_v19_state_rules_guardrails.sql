-- =============================================================================
-- v19  state_rules guardrail columns (P1-2)
-- =============================================================================
-- Roadmap §6.1 prescribes a `state_rules` registry with absolute ceilings,
-- a legal grace-day floor, and citation metadata so the Rules engine can
-- enforce compliance INDEPENDENT of the rule's own recommended fee math.
--
-- We extend the existing `rules.state_rule_versions` table in place rather
-- than forking to a new table — same row shape minus the rename, no seed
-- migration, and existing assessment audit pointers (state_rule_version_id)
-- stay valid forever. Functionally equivalent to §6.1's `state_rules`.
--
-- Append-only convention: all ALTERs guarded with IF NOT EXISTS. Existing
-- rows get sensible defaults via UPDATE so behaviour is unchanged on day
-- one (the roadmap's "Seed the table from current hardcoded logic so
-- behavior is unchanged" promise).
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;

-- ----------------------------------------------------------------------------
-- max_percent — absolute ceiling on a percent-of-rent late fee. NULL = no %
-- cap (some states only cap by flat amount). Distinct from
-- `percent_of_rent` (which is the state's *recommended* fee, not the ceiling).
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'max_percent')
BEGIN
    ALTER TABLE rules.state_rule_versions ADD max_percent DECIMAL(5,2) NULL;
END;

-- ----------------------------------------------------------------------------
-- max_flat_amount — absolute ceiling on a flat-dollar late fee.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'max_flat_amount')
BEGIN
    ALTER TABLE rules.state_rule_versions ADD max_flat_amount DECIMAL(10,2) NULL;
END;

-- ----------------------------------------------------------------------------
-- min_grace_days — legal floor. EffectivePolicyService floors the PM/lease
-- requested grace to this value; existing `grace_period_days` keeps its
-- meaning as the state's recommended grace. NOT NULL DEFAULT 0 so a row
-- with no explicit floor doesn't accidentally enforce 5+ days.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'min_grace_days')
BEGIN
    ALTER TABLE rules.state_rule_versions
        ADD min_grace_days TINYINT NOT NULL CONSTRAINT DF_state_rule_versions_min_grace_days DEFAULT (0);
END;

-- ----------------------------------------------------------------------------
-- daily_accrual_ok — whether the state allows compounding daily after the
-- grace period instead of a single charge. Today the engine doesn't accrue
-- daily; this is reserved for AutoSetup (P1-3) and reports.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'daily_accrual_ok')
BEGIN
    ALTER TABLE rules.state_rule_versions
        ADD daily_accrual_ok BIT NOT NULL CONSTRAINT DF_state_rule_versions_daily_accrual_ok DEFAULT (0);
END;

-- ----------------------------------------------------------------------------
-- requires_written_disclosure — most jurisdictions require the late-fee
-- terms to be in writing before any fee can be assessed. The onboarding
-- preflight intro email IS that disclosure for v1.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'requires_written_disclosure')
BEGIN
    ALTER TABLE rules.state_rule_versions
        ADD requires_written_disclosure BIT NOT NULL CONSTRAINT DF_state_rule_versions_req_disclosure DEFAULT (1);
END;

-- ----------------------------------------------------------------------------
-- standard_kind — 1=hard-cap statute (the law gives a number), 2=
-- "reasonableness" jurisdiction (no statute, case law says "reasonable" —
-- AutoSetup uses safe_default_pct and flags it for PM review).
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'standard_kind')
BEGIN
    ALTER TABLE rules.state_rule_versions
        ADD standard_kind TINYINT NOT NULL CONSTRAINT DF_state_rule_versions_standard_kind DEFAULT (1);
END;

-- ----------------------------------------------------------------------------
-- safe_default_pct — used when standard_kind = 2; e.g. 5.00 for "5% is the
-- widely-accepted reasonable late fee in reasonableness states."
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'safe_default_pct')
BEGIN
    ALTER TABLE rules.state_rule_versions ADD safe_default_pct DECIMAL(5,2) NULL;
END;

-- ----------------------------------------------------------------------------
-- plain_summary — what the PM sees in the rule editor / compliance pack.
-- NULL allowed for back-compat; new rows should populate.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'plain_summary')
BEGIN
    ALTER TABLE rules.state_rule_versions ADD plain_summary NVARCHAR(600) NULL;
END;

-- ----------------------------------------------------------------------------
-- source_url — link to the statute / official source. Citation already exists.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'rules.state_rule_versions')
                 AND name = 'source_url')
BEGIN
    ALTER TABLE rules.state_rule_versions ADD source_url NVARCHAR(400) NULL;
END;

-- ----------------------------------------------------------------------------
-- Back-compat: backfill min_grace_days from grace_period_days so the
-- existing 7 launch-state seeds keep their floor intact. Skips rows where
-- min_grace_days is already non-zero (so re-runs are no-ops).
-- ----------------------------------------------------------------------------
UPDATE rules.state_rule_versions
SET    min_grace_days = grace_period_days
WHERE  min_grace_days = 0
  AND  grace_period_days > 0;
