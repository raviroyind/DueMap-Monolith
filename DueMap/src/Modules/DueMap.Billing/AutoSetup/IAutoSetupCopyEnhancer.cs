namespace DueMap.Billing.AutoSetup;

/// <summary>
/// Optional LLM seam (P1-3). When the <c>onboarding.ai_copy</c> feature
/// flag is on, AutoSetupService passes the deterministic summary through
/// this interface to get a friendlier plain-English narrative. The output
/// is shown to the PM and is fully editable.
///
/// <para><strong>NEVER in the money path.</strong> The LLM cannot change a
/// fee, a grace period, a state, or a percentage. Only the rendered text
/// shown alongside the data on the review screen. The data values are
/// fixed by the deterministic <c>AutoSetupService</c> pass; this
/// interface receives them read-only.</para>
///
/// <para>The default registration is <see cref="NullAutoSetupCopyEnhancer"/>,
/// which returns the input summary unmodified. To enable an LLM-backed
/// implementation, register it in the host DI before
/// <c>BillingModule.RegisterServices</c> runs.</para>
/// </summary>
public interface IAutoSetupCopyEnhancer
{
    Task<string> RenderSummaryNarrativeAsync(AutoSetupSummary summary, CancellationToken ct);
}

/// <summary>Default no-op. Returns a deterministic plain summary.</summary>
internal sealed class NullAutoSetupCopyEnhancer : IAutoSetupCopyEnhancer
{
    public Task<string> RenderSummaryNarrativeAsync(AutoSetupSummary summary, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var narrative =
            $"We staged a late-fee profile on {summary.LeasesProcessed} lease{(summary.LeasesProcessed == 1 ? "" : "s")} " +
            $"and flagged {summary.Findings.Count} thing{(summary.Findings.Count == 1 ? "" : "s")} for your review.";
        return Task.FromResult(narrative);
    }
}
