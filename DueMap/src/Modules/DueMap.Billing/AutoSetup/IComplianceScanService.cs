using DueMap.Rules.Domain;

namespace DueMap.Billing.AutoSetup;

/// <summary>
/// Scans an AutoSetup proposal for compliance issues. Called from
/// <see cref="IAutoSetupService"/> after the per-lease writes complete; the
/// surfaced findings drive the review-screen "What needs you" list.
///
/// <para>The scan is <strong>pure</strong>: it takes in-memory inputs and
/// returns findings. No DB reads, no DI — testable as a static class. (We
/// keep it as an interface so AutoSetupService can mock-inject in unit
/// tests, but the implementation has no fields.)</para>
/// </summary>
public interface IComplianceScanService
{
    IReadOnlyList<ComplianceFinding> Scan(IReadOnlyList<ScannedLease> leases);
}

/// <summary>One lease's AutoSetup outcome, as seen by the scanner.</summary>
public sealed record ScannedLease(
    int LeaseId,
    string? StateCode,
    ResolvedRule? Rule,
    decimal? AssignedFeePercent,
    decimal? AssignedFeeFlatAmount,
    decimal MonthlyRent);
