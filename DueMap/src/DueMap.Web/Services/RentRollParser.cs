using ClosedXML.Excel;
using System.Globalization;

namespace DueMap.Web.Services;

/// <summary>
/// Parses an uploaded Excel (.xlsx) or CSV stream into a generic table the
/// rent-roll import flow can map. We avoid binding to specific column names
/// during parse — the UI presents detected headers to the PM, who confirms
/// the mapping before commit. This keeps us tolerant of every flavor of
/// rent-roll spreadsheet PMs hand us.
/// </summary>
public sealed class RentRollParser
{
    /// <summary>
    /// Read the first worksheet's header row + data rows into a tabular shape.
    /// Empty rows are skipped. Returns at most <paramref name="maxRows"/> rows
    /// of data for the preview pass; callers that want everything pass int.MaxValue.
    /// </summary>
    public ParsedTable Parse(Stream stream, string fileName, int maxRows = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        // ClosedXML reads .xlsx natively. CSV is supported via OpenWorkbook only
        // for .xlsx so we branch — CSV gets a hand-rolled parse since it's
        // trivial and avoids dragging in a second library.
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCsv(stream, maxRows);
        }
        return ParseXlsx(stream, maxRows);
    }

    private static ParsedTable ParseXlsx(Stream stream, int maxRows)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook has no worksheets.");

        var usedRange = sheet.RangeUsed();
        if (usedRange is null)
        {
            return new ParsedTable(Array.Empty<string>(), Array.Empty<string[]>());
        }

        var rows = usedRange.RowsUsed().ToList();
        if (rows.Count == 0)
        {
            return new ParsedTable(Array.Empty<string>(), Array.Empty<string[]>());
        }

        // First non-empty row = header. Anything above it (e.g. a "Rent Roll
        // for April 2026" title row) is ignored.
        var headerRow = rows[0];
        var headers = headerRow.Cells().Select(c => c.GetString().Trim()).ToArray();

        var data = new List<string[]>();
        for (int i = 1; i < rows.Count && data.Count < maxRows; i++)
        {
            var cells = rows[i].Cells(1, headers.Length).Select(c => c.GetString().Trim()).ToArray();
            // Skip rows where every cell is empty.
            if (cells.All(string.IsNullOrEmpty)) continue;
            data.Add(cells);
        }

        return new ParsedTable(headers, data);
    }

    private static ParsedTable ParseCsv(Stream stream, int maxRows)
    {
        using var reader = new StreamReader(stream);
        var headers = Array.Empty<string>();
        var data = new List<string[]>();
        var first = true;

        while (!reader.EndOfStream && data.Count < maxRows)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (first)
            {
                headers = fields.Select(f => f.Trim()).ToArray();
                first = false;
            }
            else
            {
                data.Add(fields.Select(f => f.Trim()).ToArray());
            }
        }
        return new ParsedTable(headers, data);
    }

    // Lightweight CSV row split — handles double-quoted fields with embedded
    // commas. Not a full RFC 4180 parser (no escaping of CRLF inside quotes,
    // no escaping of quotes themselves) but covers the 95% case for rent rolls
    // exported from Excel.
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        fields.Add(current.ToString());
        return fields;
    }

    /// <summary>
    /// Best-effort guess at which DueMap field a header column represents.
    /// Returns null when nothing recognizable matches — the user has to pick
    /// from the dropdown. Comparison is case-insensitive and tolerant of
    /// common separators ("Monthly Rent", "monthly_rent", "Rent$", etc).
    /// </summary>
    public static RentRollField? GuessField(string header)
    {
        var normalised = new string((header ?? "").ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();

        if (string.IsNullOrEmpty(normalised)) return null;

        return normalised switch
        {
            var s when s.Contains("tenant") || s.Contains("name") || s == "resident" => RentRollField.TenantName,
            var s when s.Contains("unit") || s.Contains("apt") || s.Contains("apartment") || s.Contains("suite") => RentRollField.UnitLabel,
            var s when s.Contains("rent") || s == "amount" || s == "monthly" => RentRollField.MonthlyRent,
            var s when s.Contains("start") || s == "move in" || s == "movein" || s == "lease start" || s == "begin" => RentRollField.StartDate,
            var s when s.Contains("end") || s == "move out" || s == "moveout" || s == "lease end" => RentRollField.EndDate,
            var s when s == "state" || s == "st" => RentRollField.State,
            var s when s.Contains("email") => RentRollField.Email,
            _ => null
        };
    }

    /// <summary>
    /// Parses a cell value into a typed target. Returns null when the cell is
    /// blank OR can't be coerced — callers decide whether that's an error.
    /// </summary>
    public static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Strip $, commas, spaces — rent rolls love to format these.
        var stripped = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        return decimal.TryParse(stripped, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Try common US formats first, then fall back to invariant parse.
        var formats = new[] { "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "M/d/yy", "MMM d, yyyy", "MMMM d, yyyy" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return DateOnly.FromDateTime(dt);
        }
        return null;
    }
}

public sealed record ParsedTable(IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows);

public enum RentRollField
{
    Skip,            // explicit "don't import this column"
    TenantName,
    UnitLabel,
    MonthlyRent,
    StartDate,
    EndDate,
    State,           // 2-letter state code (CA, TX, etc.)
    Email            // for fuzzy-matching against synced QB customers
}
