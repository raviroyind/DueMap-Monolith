using System.Globalization;

namespace DueMap.Billing.AutoSetup;

/// <summary>
/// Pure implementation. One pass per lease — checks state coverage, rule
/// availability, post-clamp ceiling, then emits the matching findings.
/// </summary>
internal sealed class ComplianceScanService : IComplianceScanService
{
    public IReadOnlyList<ComplianceFinding> Scan(IReadOnlyList<ScannedLease> leases)
    {
        ArgumentNullException.ThrowIfNull(leases);

        var findings = new List<ComplianceFinding>(capacity: leases.Count);
        foreach (var lease in leases)
        {
            // 1. No state info at all → AutoSetup used the safe default but the
            //    PM must confirm.
            if (string.IsNullOrEmpty(lease.StateCode))
            {
                findings.Add(new ComplianceFinding(
                    lease.LeaseId,
                    ComplianceFindingKind.AmbiguousState,
                    "We couldn't determine this lease's state from your accounting data. We applied a safe default; please confirm before going live.",
                    CurrentValue: null,
                    CompliantFix: "Set the lease's state on the Tenants screen."));
                continue;
            }

            // 2. State known but no rule on file → conservative skip + flag.
            if (lease.Rule is null)
            {
                findings.Add(new ComplianceFinding(
                    lease.LeaseId,
                    ComplianceFindingKind.NoStateRuleFound,
                    $"We don't have a rule on file for {lease.StateCode}. No fees were staged for this lease.",
                    CurrentValue: null,
                    CompliantFix: "Contact support — we'll add the state rule and re-run AutoSetup."));
                continue;
            }

            // 3. Reasonableness jurisdiction → flag so the PM knows the default
            //    is a conservative guess rather than a statutory number.
            if (lease.Rule.StandardKind == 2)
            {
                var safePct = lease.Rule.SafeDefaultPct ?? 5m;
                findings.Add(new ComplianceFinding(
                    lease.LeaseId,
                    ComplianceFindingKind.ReasonablenessJurisdiction,
                    $"{lease.StateCode} doesn't fix a statutory late-fee number — courts ask whether the fee is \"reasonable.\" We staged the widely-accepted {safePct.ToString("0.##", CultureInfo.InvariantCulture)}%. Adjust if your lease specifies something different.",
                    CurrentValue: $"{safePct.ToString("0.##", CultureInfo.InvariantCulture)}%",
                    CompliantFix: "Review the per-lease fee on the Tenants screen."));
            }

            // 4. Defensive: did the assigned fee somehow exceed the cap?
            //    Should never happen after the AutoSetup clamp, but worth
            //    a belt-and-braces check so a logic bug never goes live.
            if (lease.AssignedFeePercent is decimal pct &&
                lease.Rule.StateMaxPercent is decimal maxPct &&
                pct > maxPct)
            {
                findings.Add(new ComplianceFinding(
                    lease.LeaseId,
                    ComplianceFindingKind.FeeExceedsCap,
                    $"Internal error: assigned percent ({pct.ToString("0.##", CultureInfo.InvariantCulture)}%) exceeds state cap ({maxPct.ToString("0.##", CultureInfo.InvariantCulture)}%). Fee was NOT staged.",
                    CurrentValue: $"{pct.ToString("0.##", CultureInfo.InvariantCulture)}%",
                    CompliantFix: $"Cap is {maxPct.ToString("0.##", CultureInfo.InvariantCulture)}% — retry AutoSetup."));
            }
            if (lease.AssignedFeeFlatAmount is decimal flat &&
                lease.Rule.StateMaxFlatAmount is decimal maxFlat &&
                flat > maxFlat)
            {
                findings.Add(new ComplianceFinding(
                    lease.LeaseId,
                    ComplianceFindingKind.FeeExceedsCap,
                    $"Internal error: assigned flat fee (${flat.ToString("0.00", CultureInfo.InvariantCulture)}) exceeds state cap (${maxFlat.ToString("0.00", CultureInfo.InvariantCulture)}). Fee was NOT staged.",
                    CurrentValue: $"${flat.ToString("0.00", CultureInfo.InvariantCulture)}",
                    CompliantFix: $"Cap is ${maxFlat.ToString("0.00", CultureInfo.InvariantCulture)} — retry AutoSetup."));
            }
        }
        return findings;
    }
}
