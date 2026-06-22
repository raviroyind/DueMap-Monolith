namespace DueMap.Billing.AutoSetup;

/// <summary>
/// Configures a new PM's late-fee profile + reminders end-to-end within the
/// §6 compliance guardrails, then runs the compliance scan and persists a
/// summary on the PM row. Driven by the P1-1 inferred values; deterministic,
/// no LLM in the money path.
///
/// <para>Flag-gated by <c>onboarding.auto_setup</c>. When off, <see cref="RunAsync"/>
/// is a no-op (returns an empty summary, never writes).</para>
///
/// <para><strong>Idempotent.</strong> Re-running overwrites the staged setup;
/// previously-staged fees are re-staged, the summary is replaced.</para>
///
/// <para><strong>Staging contract.</strong> Every lease AutoSetup touches lands
/// with <c>FeesStaged = true</c> — the assessment planner skips it until the
/// explicit "Go live" action (P1-4) flips the flag.</para>
/// </summary>
public interface IAutoSetupService
{
    Task<AutoSetupSummary> RunAsync(int propertyManagerId, CancellationToken ct);
}
