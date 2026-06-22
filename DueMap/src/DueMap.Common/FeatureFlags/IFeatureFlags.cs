namespace DueMap.Common.FeatureFlags;

/// <summary>
/// Resolves a feature-flag key for an optional property-manager scope.
///
/// **Contract: fail-safe.** A missing flag, a malformed
/// <c>enabled_pm_ids</c> JSON, a DB error, or any unexpected failure
/// returns <c>false</c> — callers never need to wrap calls in try/catch
/// and a broken cockpit DB can never accidentally turn on a dark feature.
/// Failures are logged at Warning so they don't disappear silently.
///
/// **Per-PM resolution:**
/// <list type="bullet">
///   <item><c>enabled_global = 1</c> → on for everyone, regardless of <paramref name="propertyManagerId"/>.</item>
///   <item>otherwise, on iff <paramref name="propertyManagerId"/> is in <c>enabled_pm_ids</c> JSON array.</item>
/// </list>
///
/// **Caching:** implementations cache the parsed flag row in-process with
/// a short TTL (~60s). That's the right trade-off for our scale —
/// fast enough for the worker's per-action loop, slow enough that flipping
/// a flag in the DB takes effect within a minute without a deploy.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>
    /// True if <paramref name="key"/> is enabled. If <paramref name="propertyManagerId"/>
    /// is null, only <c>enabled_global</c> can return true (global queries from
    /// startup paths, ops tools, etc.).
    /// </summary>
    Task<bool> IsEnabledAsync(string key, int? propertyManagerId = null, CancellationToken ct = default);
}
