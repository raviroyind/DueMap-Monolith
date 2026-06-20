using DueMap.Common.FeatureFlags;
using Xunit;

namespace DueMap.Common.Tests;

/// <summary>
/// Coverage for the pure pieces of <see cref="FeatureFlags"/> — the JSON
/// parser and the global-OR-per-PM resolution logic. DB read path is
/// out of scope for unit tests (integration test in a later phase).
///
/// Test names use snake_case (Behaviour_under_condition) which CA1707
/// would normally complain about — relaxed in the .csproj for tests only.
/// </summary>
public class FeatureFlagsTests
{
    private static readonly int[] _expected_1_7_42 = { 1, 7, 42 };
    private static readonly int[] _expected_1_2_3  = { 1, 2, 3 };

    // -----------------------------------------------------------------
    // ParsePmIds — the JSON parser
    // -----------------------------------------------------------------

    [Fact]
    public void ParsePmIds_null_returns_empty_no_error()
    {
        var (set, err) = SqlFeatureFlags.ParsePmIds(null);

        Assert.Empty(set);
        Assert.Null(err);
    }

    [Fact]
    public void ParsePmIds_empty_string_returns_empty_no_error()
    {
        var (set, err) = SqlFeatureFlags.ParsePmIds("");

        Assert.Empty(set);
        Assert.Null(err);
    }

    [Fact]
    public void ParsePmIds_valid_int_array_returns_set()
    {
        var (set, err) = SqlFeatureFlags.ParsePmIds("[1, 7, 42]");

        Assert.Equal(_expected_1_7_42, set.OrderBy(x => x));
        Assert.Null(err);
    }

    [Fact]
    public void ParsePmIds_duplicates_are_deduplicated()
    {
        var (set, err) = SqlFeatureFlags.ParsePmIds("[1, 1, 2]");

        Assert.Equal(2, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Null(err);
    }

    [Fact]
    public void ParsePmIds_malformed_returns_empty_and_error()
    {
        // Trailing garbage — JsonDocument.Parse throws.
        var (set, err) = SqlFeatureFlags.ParsePmIds("[1, 2,");

        Assert.Empty(set);
        Assert.NotNull(err);
    }

    [Fact]
    public void ParsePmIds_root_object_not_array_returns_empty_and_error()
    {
        var (set, err) = SqlFeatureFlags.ParsePmIds("{ \"foo\": 1 }");

        Assert.Empty(set);
        Assert.Equal("root not array", err);
    }

    [Fact]
    public void ParsePmIds_skips_non_int_entries_silently()
    {
        // Mixed array — strings and bools are skipped, ints survive.
        // A strict parse here would disable the whole flag for one bad
        // entry, which is worse UX than just ignoring the entry.
        var (set, err) = SqlFeatureFlags.ParsePmIds("[1, \"abc\", 2, true, 3]");

        Assert.Equal(_expected_1_2_3, set.OrderBy(x => x));
        Assert.Null(err);   // skipped entries aren't an error
    }

    // -----------------------------------------------------------------
    // Resolve — the OR semantics
    // -----------------------------------------------------------------

    [Fact]
    public void Resolve_null_flag_returns_false()
    {
        // Defensive: a missing row should never enable.
        Assert.False(SqlFeatureFlags.Resolve(flag: null, propertyManagerId: 1));
        Assert.False(SqlFeatureFlags.Resolve(flag: null, propertyManagerId: null));
    }

    [Fact]
    public void Resolve_global_true_returns_true_regardless_of_pm()
    {
        var flag = new SqlFeatureFlags.ResolvedFlag(EnabledGlobal: true, EnabledPmIds: new HashSet<int>());

        Assert.True(SqlFeatureFlags.Resolve(flag, propertyManagerId: null));
        Assert.True(SqlFeatureFlags.Resolve(flag, propertyManagerId: 1));
        Assert.True(SqlFeatureFlags.Resolve(flag, propertyManagerId: 99999));
    }

    [Fact]
    public void Resolve_per_pm_only_returns_true_for_listed_pm()
    {
        var flag = new SqlFeatureFlags.ResolvedFlag(
            EnabledGlobal: false,
            EnabledPmIds: new HashSet<int> { 7, 42 });

        Assert.True(SqlFeatureFlags.Resolve(flag, propertyManagerId: 7));
        Assert.True(SqlFeatureFlags.Resolve(flag, propertyManagerId: 42));
        Assert.False(SqlFeatureFlags.Resolve(flag, propertyManagerId: 8));
    }

    [Fact]
    public void Resolve_per_pm_null_pm_id_returns_false()
    {
        // A startup-time / global query (no pmId) can only succeed on
        // enabled_global = true. Anything else is off.
        var flag = new SqlFeatureFlags.ResolvedFlag(
            EnabledGlobal: false,
            EnabledPmIds: new HashSet<int> { 1, 2 });

        Assert.False(SqlFeatureFlags.Resolve(flag, propertyManagerId: null));
    }

    [Fact]
    public void Resolve_empty_pm_list_with_global_off_is_off()
    {
        // A row that exists but is fully disabled — the most common
        // "ship dark, nobody opted in yet" state.
        var flag = new SqlFeatureFlags.ResolvedFlag(
            EnabledGlobal: false,
            EnabledPmIds: new HashSet<int>());

        Assert.False(SqlFeatureFlags.Resolve(flag, propertyManagerId: 1));
        Assert.False(SqlFeatureFlags.Resolve(flag, propertyManagerId: null));
    }
}
