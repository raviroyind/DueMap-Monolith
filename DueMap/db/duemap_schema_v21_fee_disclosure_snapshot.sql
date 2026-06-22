-- =============================================================================
-- v21  Late-fee evidence stamping (P1-6)
-- =============================================================================
-- Roadmap §6.2: when a late fee is assessed, STAMP the rule version +
-- a disclosure snapshot onto the assessment row so the historical record
-- proves which rule (and which terms) produced the fee — even after the rule
-- is later versioned. state_rule_version_id already exists; this adds the
-- human-readable snapshot.
--
-- disclosure_snapshot is a JSON blob written by ActionExecutor at assessment
-- time: state code + citation + source url + fee type/percent/flat/cap +
-- grace + plain-language summary + the computed fee and rent it applied to.
--
-- Single ALTER, guarded by IF NOT EXISTS — safe to re-run.
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT  ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'billing.late_fee_assessments')
                 AND name = 'disclosure_snapshot')
BEGIN
    ALTER TABLE billing.late_fee_assessments ADD disclosure_snapshot NVARCHAR(MAX) NULL;
END;
GO
