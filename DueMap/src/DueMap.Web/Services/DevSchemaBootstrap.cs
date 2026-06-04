using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace DueMap.Web.Services;

/// <summary>
/// Development convenience: scans <c>db/</c> for <c>duemap_schema_v*.sql</c>
/// files and runs anything not yet applied. Keeps the local dev loop
/// frictionless when we ship a new schema migration — no more "Invalid column
/// name" errors because someone forgot to run sqlcmd.
///
/// <para><b>NEVER call this in production.</b> Real environments should run
/// migrations explicitly via a deploy pipeline so the change is reviewed and
/// auditable. Wired only when ASPNETCORE_ENVIRONMENT == "Development".</para>
///
/// Idempotence: each shipped script already has its own <c>IF NOT EXISTS</c>
/// guard, so running them all on every startup is safe. We additionally track
/// applied scripts in a small <c>dbo.__schema_versions</c> table so re-runs
/// finish in milliseconds instead of re-issuing every DDL.
/// </summary>
public sealed partial class DevSchemaBootstrap
{
    private readonly string _connectionString;
    private readonly string _scriptsDir;
    private readonly ILogger<DevSchemaBootstrap> _logger;

    public DevSchemaBootstrap(IConfiguration config, IHostEnvironment env, ILogger<DevSchemaBootstrap> logger)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default missing.");
        // db/ sits beside src/ in the repo. ContentRootPath is the project dir
        // (src/DueMap.Web), so jump up two levels.
        _scriptsDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "db"));
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_scriptsDir))
        {
            LogScriptsDirMissing(_logger, _scriptsDir);
            return;
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await EnsureVersionTableAsync(conn, ct);
        var applied = await GetAppliedAsync(conn, ct);

        // Lex order on filenames lines up with v1, v2, v3… so the natural sort
        // applies migrations in the order they were authored.
        var scripts = Directory.GetFiles(_scriptsDir, "duemap_schema_v*.sql")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in scripts)
        {
            var fileName = Path.GetFileName(path);
            if (applied.Contains(fileName)) continue;

            LogApplyingMigration(_logger, fileName);
            var sql = await File.ReadAllTextAsync(path, ct);
            // ExecuteScriptAsync now swallows "already exists" errors PER BATCH
            // so a script like duemap_schema.sql with many CREATE TABLE batches
            // can baseline some batches while still applying any new ones. If
            // a real (non-baseline) error happens it still bubbles up.
            var baselineCount = await ExecuteScriptAsync(conn, sql, fileName, ct);
            if (baselineCount > 0)
            {
                LogBaselineAccepted(_logger, fileName, baselineCount);
            }
            await MarkAppliedAsync(conn, fileName, ct);
        }
    }

    // SQL Server error numbers that signal "the thing this script tried to
    // create already exists" — exactly what we want to treat as a successful
    // baseline rather than a real failure.
    //
    //   2714  — "There is already an object named '%.*ls' in the database."
    //   1779  — "Table already has a primary key defined on it."
    //   1913  — "The operation failed because an index or statistics already exists."
    //   2705  — "Column names in each table must be unique."
    //   4922  — "ALTER TABLE ALTER COLUMN ... failed because ... is being referenced."
    //            (not a duplicate; do NOT swallow this one)
    private static bool IsAlreadyExistsError(SqlException ex)
    {
        foreach (SqlError error in ex.Errors)
        {
            if (error.Number is 2714 or 1779 or 1913 or 2705) return true;
        }
        return false;
    }

    private static async Task EnsureVersionTableAsync(SqlConnection conn, CancellationToken ct)
    {
        const string ddl = @"
            IF OBJECT_ID(N'dbo.__schema_versions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.__schema_versions (
                    script_name NVARCHAR(200) NOT NULL PRIMARY KEY,
                    applied_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END";
        await using var cmd = new SqlCommand(ddl, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<HashSet<string>> GetAppliedAsync(SqlConnection conn, CancellationToken ct)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand("SELECT script_name FROM dbo.__schema_versions", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            applied.Add(reader.GetString(0));
        }
        return applied;
    }

    private static async Task MarkAppliedAsync(SqlConnection conn, string fileName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "INSERT INTO dbo.__schema_versions (script_name) VALUES (@n)", conn);
        cmd.Parameters.AddWithValue("@n", fileName);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // SqlCommand can't run a script containing GO batches as one statement.
    // Split on GO (whole-line) and run each batch separately. Returns the
    // number of batches we baselined (i.e. skipped because the object already
    // existed) — useful for logging but not load-bearing.
    private static async Task<int> ExecuteScriptAsync(SqlConnection conn, string sql, string fileName, CancellationToken ct)
    {
        var batches = GoSplitter().Split(sql);
        var baselineCount = 0;
        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            await using var cmd = new SqlCommand(trimmed, conn) { CommandTimeout = 60 };
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (SqlException ex) when (IsAlreadyExistsError(ex))
            {
                // This batch's effect is already in the DB. Skip it; other
                // batches in the same script may still add new things.
                baselineCount++;
            }
        }
        return baselineCount;
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSplitter();

    [LoggerMessage(EventId = 9101, Level = LogLevel.Warning,
        Message = "DevSchemaBootstrap: scripts directory not found at '{Path}'. Skipping.")]
    static partial void LogScriptsDirMissing(ILogger logger, string path);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Information,
        Message = "DevSchemaBootstrap: applying migration {FileName}")]
    static partial void LogApplyingMigration(ILogger logger, string fileName);

    [LoggerMessage(EventId = 9103, Level = LogLevel.Information,
        Message = "DevSchemaBootstrap: {FileName} — {BaselineCount} batch(es) already in DB; stamping as applied")]
    static partial void LogBaselineAccepted(ILogger logger, string fileName, int baselineCount);
}
