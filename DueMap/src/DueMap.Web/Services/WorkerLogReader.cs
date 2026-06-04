using Microsoft.Data.SqlClient;

namespace DueMap.Web.Services;

/// <summary>
/// Read-only query surface over <c>logs.events</c> — the Serilog SQL sink
/// in the Worker process writes rows; the Web admin dashboard reads them.
/// We deliberately use raw ADO.NET rather than introducing a fourth EF
/// DbContext for what's a flat audit table with no relations.
///
/// SECURITY: this reader does NOT enforce tenant scoping — it returns rows
/// for every PM. The only caller is <c>/admin/worker-logs</c>, which is
/// currently gated by <see cref="DueMap.Web.Security.AdminBasicAuthMiddleware"/>
/// (dev-only Basic Auth). When we add a real platform-admin role, the gate
/// moves to <c>[Authorize(Roles = "Admin")]</c>; if we ever expose a
/// per-PM "your logs" view, force <c>PmId</c> from <c>PmContext</c> here
/// rather than trusting the query record.
/// </summary>
public sealed class WorkerLogReader
{
    private readonly string _connectionString;

    public WorkerLogReader(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");
    }

    public async Task<IReadOnlyList<WorkerLogEntry>> QueryAsync(WorkerLogQuery q, CancellationToken ct)
    {
        // Build the WHERE clause incrementally. SqlParameter is used everywhere
        // so the search text + level lists can't smuggle in SQL.
        var sql = """
            SELECT TOP (@take)
                   id, timestamp, level, message, exception, pm_id, run_id,
                   business_date, source_context, properties
            FROM   logs.events
            WHERE  timestamp >= @from
              AND  timestamp <  @to
            """;

        var args = new List<SqlParameter>
        {
            new("@take", q.MaxRows),
            new("@from", q.FromUtc),
            new("@to",   q.ToUtc),
        };

        if (q.MinimumLevel is not null)
        {
            // Levels are stored as strings; ordering by severity is via a CASE.
            sql += " AND (CASE level WHEN 'Verbose' THEN 0 WHEN 'Debug' THEN 1 " +
                   " WHEN 'Information' THEN 2 WHEN 'Warning' THEN 3 " +
                   " WHEN 'Error' THEN 4 WHEN 'Fatal' THEN 5 ELSE 2 END) >= @minLevel";
            args.Add(new SqlParameter("@minLevel", LevelRank(q.MinimumLevel)));
        }
        if (q.PmId is int pmId)
        {
            sql += " AND pm_id = @pmId";
            args.Add(new SqlParameter("@pmId", pmId));
        }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            sql += " AND (message LIKE @search OR exception LIKE @search OR source_context LIKE @search)";
            args.Add(new SqlParameter("@search", $"%{q.Search.Trim()}%"));
        }

        sql += " ORDER BY timestamp DESC";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(args.ToArray());

        var results = new List<WorkerLogEntry>(capacity: q.MaxRows);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new WorkerLogEntry(
                Id:             reader.GetInt64(0),
                Timestamp:      DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                Level:          reader.GetString(2),
                Message:        reader.IsDBNull(3) ? null : reader.GetString(3),
                Exception:      reader.IsDBNull(4) ? null : reader.GetString(4),
                PmId:           reader.IsDBNull(5) ? null : reader.GetInt32(5),
                RunId:          reader.IsDBNull(6) ? null : reader.GetInt64(6),
                BusinessDate:   reader.IsDBNull(7) ? null : DateOnly.FromDateTime(reader.GetDateTime(7)),
                SourceContext:  reader.IsDBNull(8) ? null : reader.GetString(8),
                PropertiesJson: reader.IsDBNull(9) ? null : reader.GetString(9)));
        }
        return results;
    }

    private static int LevelRank(string level) => level switch
    {
        "Verbose"     => 0,
        "Debug"       => 1,
        "Information" => 2,
        "Warning"     => 3,
        "Error"       => 4,
        "Fatal"       => 5,
        _             => 2,
    };
}

public sealed record WorkerLogQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    string? MinimumLevel = null,
    int? PmId = null,
    string? Search = null,
    int MaxRows = 500);

public sealed record WorkerLogEntry(
    long Id,
    DateTime Timestamp,
    string Level,
    string? Message,
    string? Exception,
    int? PmId,
    long? RunId,
    DateOnly? BusinessDate,
    string? SourceContext,
    string? PropertiesJson);
