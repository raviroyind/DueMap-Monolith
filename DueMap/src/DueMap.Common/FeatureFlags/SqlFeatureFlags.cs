using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DueMap.Common.FeatureFlags;

/// <summary>
/// SQL-backed feature-flag resolver. Each public call:
/// <list type="number">
///   <item>checks the in-memory cache for <c>(key)</c></item>
///   <item>on miss, reads the single row from <c>ops.feature_flags</c></item>
///   <item>parses + caches for <see cref="CacheTtl"/></item>
///   <item>applies <c>enabled_global OR pmId ∈ enabled_pm_ids</c></item>
/// </list>
///
/// Cache key is the flag <c>key</c> (not the (key, pmId) pair) because the
/// row is small and the per-PM check is a hash-set lookup against the
/// already-parsed allow-list — cheap on the hot path, simpler invalidation.
/// </summary>
internal sealed partial class SqlFeatureFlags : IFeatureFlags
{
    /// <summary>
    /// How long a parsed flag row stays cached. Short enough that flipping
    /// a flag in the DB takes effect within ~a minute without restart;
    /// long enough that a worker tight loop doesn't hammer the DB.
    /// </summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SqlFeatureFlags> _logger;

    public SqlFeatureFlags(string connectionString, IMemoryCache cache, ILogger<SqlFeatureFlags> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string key, int? propertyManagerId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        try
        {
            var resolved = await _cache.GetOrCreateAsync(CacheKey(key), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await LoadAsync(key, ct);
            });

            return Resolve(resolved, propertyManagerId);
        }
        catch (Exception ex)
        {
            // Final-line fail-safe. Anything weirder than DB error
            // (e.g. cache provider failure, threading anomaly) lands here.
            LogFlagResolveFailed(_logger, ex, key);
            return false;
        }
    }

    // ------------------------------------------------------------------
    // DB read + JSON parse
    // ------------------------------------------------------------------

    private async Task<ResolvedFlag?> LoadAsync(string key, CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "SELECT enabled_global, enabled_pm_ids FROM ops.feature_flags WHERE [key] = @key", conn);
            cmd.Parameters.AddWithValue("@key", key);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                // Unknown flag — cache the "not found" too so we don't
                // re-hit the DB every call for a typo.
                return new ResolvedFlag(false, new HashSet<int>());
            }

            var global = reader.GetBoolean(0);
            var json = reader.IsDBNull(1) ? null : reader.GetString(1);
            var (pmIds, parseError) = ParsePmIds(json);
            if (parseError is not null) LogMalformedJson(_logger, key, parseError);
            return new ResolvedFlag(global, pmIds);
        }
        catch (SqlException ex)
        {
            // DB unreachable / table missing / permission denied. Logged once
            // per cache miss (so at most 1/min/flag during an outage).
            LogFlagDbFailed(_logger, ex, key);
            return new ResolvedFlag(false, new HashSet<int>());
        }
    }

    /// <summary>
    /// Pure parser for the <c>enabled_pm_ids</c> JSON column. Returns the parsed
    /// set, plus an optional error string the caller can log. Failure modes
    /// (malformed JSON, root not array) yield an EMPTY set + non-null error —
    /// fail-safe at the storage boundary. Non-int array entries are silently
    /// skipped (one bad entry shouldn't disable the whole flag).
    /// Internal so DueMap.Common.Tests can drive it directly.
    /// </summary>
    internal static (HashSet<int> PmIds, string? Error) ParsePmIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (new HashSet<int>(), null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (new HashSet<int>(), "root not array");
            }

            var set = new HashSet<int>(capacity: doc.RootElement.GetArrayLength());
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                {
                    set.Add(id);
                }
            }
            return (set, null);
        }
        catch (JsonException ex)
        {
            return (new HashSet<int>(), ex.Message);
        }
    }

    /// <summary>
    /// Pure resolution logic given an already-loaded flag row. Mirrored as
    /// internal so tests can validate the OR semantics without a DB.
    /// </summary>
    internal static bool Resolve(ResolvedFlag? flag, int? propertyManagerId)
    {
        if (flag is null) return false;
        if (flag.EnabledGlobal) return true;
        if (propertyManagerId is null) return false;
        return flag.EnabledPmIds.Contains(propertyManagerId.Value);
    }
    private static string CacheKey(string flag) => $"ff:{flag}";

    /// <summary>
    /// Parsed flag row, ready for OR-resolution. Internal so tests can
    /// construct one and drive <see cref="Resolve"/> directly.
    /// </summary>
    internal sealed record ResolvedFlag(bool EnabledGlobal, HashSet<int> EnabledPmIds);

    // ------------------------------------------------------------------
    // Logging (LoggerMessage source generator — zero-alloc, stable EventIds)
    // ------------------------------------------------------------------

    [LoggerMessage(EventId = 9201, Level = LogLevel.Warning,
        Message = "Feature flag DB read failed for key={Key}. Treating as off.")]
    static partial void LogFlagDbFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Warning,
        Message = "Feature flag {Key} has malformed enabled_pm_ids JSON: {Reason}. Treating as no per-PM enrollment.")]
    static partial void LogMalformedJson(ILogger logger, string key, string reason);

    [LoggerMessage(EventId = 9203, Level = LogLevel.Warning,
        Message = "Feature flag resolve failed for key={Key}. Treating as off.")]
    static partial void LogFlagResolveFailed(ILogger logger, Exception ex, string key);
}
