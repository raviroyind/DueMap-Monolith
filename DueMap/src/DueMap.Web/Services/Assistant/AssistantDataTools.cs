using System.Text.Json;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;

namespace DueMap.Web.Services.Assistant;

/// <summary>
/// Read-only data tools the assistant can call. Every method takes the PM id
/// resolved server-side from the auth principal — the model never supplies
/// it — so a crafted prompt cannot reach across workspaces. All results are
/// serialized to compact JSON for the tool_result block.
/// </summary>
public sealed class AssistantDataTools
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ILeaseReader _leases;
    private readonly IRentInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;

    public AssistantDataTools(
        ILeaseReader leases,
        IRentInvoiceRepository invoices,
        ICustomerRepository customers)
    {
        _leases = leases;
        _invoices = invoices;
        _customers = customers;
    }

    /// <summary>Dispatch a tool call by name. Unknown names return an error payload
    /// the model can read (never throws for bad model input).</summary>
    public async Task<string> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> input,
        int pmId,
        CancellationToken ct)
    {
        switch (toolName)
        {
            case "get_portfolio_summary":
                return await GetPortfolioSummaryAsync(pmId, ct);

            case "list_invoices":
            {
                var status = GetString(input, "status") ?? "overdue";
                var limit = GetInt(input, "limit") ?? 25;
                return await ListInvoicesAsync(pmId, status, limit, ct);
            }

            case "list_leases":
            {
                var expiringWithinDays = GetInt(input, "expiring_within_days");
                return await ListLeasesAsync(pmId, expiringWithinDays, ct);
            }

            case "search_tenants":
            {
                var query = GetString(input, "query");
                if (string.IsNullOrWhiteSpace(query))
                    return Error("Missing required parameter 'query'.");
                return await SearchTenantsAsync(pmId, query, ct);
            }

            case "get_lease_details":
            {
                var leaseId = GetInt(input, "lease_id");
                if (leaseId is null)
                    return Error("Missing required integer parameter 'lease_id'.");
                return await GetLeaseDetailsAsync(pmId, leaseId.Value, ct);
            }

            default:
                return Error($"Unknown tool '{toolName}'.");
        }
    }

    private async Task<string> GetPortfolioSummaryAsync(int pmId, CancellationToken ct)
    {
        var today = Today();
        var leases = await _leases.ListActiveAsync(pmId, today, ct);
        var invoices = await _invoices.ListByPropertyManagerAsync(pmId, ct);
        var customers = await _customers.ListByPropertyManagerAsync(pmId, ct);

        var open = invoices.Where(i => i.Status == RentInvoiceStatus.Open && i.Balance > 0).ToList();
        var overdue = open.Where(i => i.DueDate < today).ToList();

        return JsonSerializer.Serialize(new
        {
            asOf = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            activeLeases = leases.Count,
            leasesUnlinkedToCustomer = leases.Count(l => l.CustomerId is null),
            leasesStagedNotLive = leases.Count(l => l.FeesStaged),
            leasesOnAutopay = leases.Count(l => l.AutopayStatus == AutopayStatus.Enrolled),
            monthlyRentRoll = leases.Sum(l => l.MonthlyRent),
            customers = customers.Count(c => c.IsActive),
            openInvoices = open.Count,
            openBalance = open.Sum(i => i.Balance),
            overdueInvoices = overdue.Count,
            overdueBalance = overdue.Sum(i => i.Balance)
        }, Json);
    }

    private async Task<string> ListInvoicesAsync(int pmId, string status, int limit, CancellationToken ct)
    {
        var today = Today();
        var invoices = await _invoices.ListByPropertyManagerAsync(pmId, ct);
        var customers = (await _customers.ListByPropertyManagerAsync(pmId, ct))
            .ToDictionary(c => c.Id);

        IEnumerable<RentInvoice> filtered = status.ToLowerInvariant() switch
        {
            "overdue" => invoices.Where(i => i.Status == RentInvoiceStatus.Open && i.Balance > 0 && i.DueDate < today)
                                 .OrderBy(i => i.DueDate),
            "open"    => invoices.Where(i => i.Status == RentInvoiceStatus.Open && i.Balance > 0)
                                 .OrderBy(i => i.DueDate),
            "paid"    => invoices.Where(i => i.Status == RentInvoiceStatus.Paid)
                                 .OrderByDescending(i => i.DueDate),
            _         => invoices.OrderByDescending(i => i.DueDate)
        };

        limit = Math.Clamp(limit, 1, 100);
        var rows = filtered.Take(limit).Select(i => new
        {
            invoiceId = i.Id,
            invoiceNumber = i.ExternalDocNumber,
            customer = customers.TryGetValue(i.CustomerId, out var c) ? c.DisplayName : $"customer #{i.CustomerId}",
            customerEmail = customers.TryGetValue(i.CustomerId, out var c2) ? c2.Email : null,
            leaseId = i.LeaseId,
            dueDate = i.DueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            daysOverdue = i.DueDate < today && i.Balance > 0 && i.Status == RentInvoiceStatus.Open
                ? today.DayNumber - i.DueDate.DayNumber
                : 0,
            totalAmount = i.TotalAmount,
            balance = i.Balance,
            status = i.Status.ToString().ToLowerInvariant(),
            hasOnlinePayLink = !string.IsNullOrEmpty(i.PublicPaymentUrl)
        }).ToList();

        return JsonSerializer.Serialize(new { filter = status, count = rows.Count, invoices = rows }, Json);
    }

    private async Task<string> ListLeasesAsync(int pmId, int? expiringWithinDays, CancellationToken ct)
    {
        var today = Today();
        var leases = await _leases.ListActiveAsync(pmId, today, ct);
        var customers = (await _customers.ListByPropertyManagerAsync(pmId, ct))
            .ToDictionary(c => c.Id);

        IEnumerable<Lease> filtered = leases;
        if (expiringWithinDays is int days)
        {
            var horizon = today.AddDays(Math.Clamp(days, 1, 730));
            filtered = leases.Where(l => l.EndDate is DateOnly end && end >= today && end <= horizon)
                             .OrderBy(l => l.EndDate);
        }

        var rows = filtered.Select(l => new
        {
            leaseId = l.Id,
            unit = l.UnitLabel,
            tenant = l.CustomerId is int cid && customers.TryGetValue(cid, out var c) ? c.DisplayName : null,
            monthlyRent = l.MonthlyRent,
            startDate = l.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            endDate = l.EndDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            autopay = l.AutopayStatus.ToString().ToLowerInvariant(),
            liveForProcessing = !l.FeesStaged
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            asOf = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            expiringWithinDays,
            count = rows.Count,
            leases = rows
        }, Json);
    }

    private async Task<string> SearchTenantsAsync(int pmId, string query, CancellationToken ct)
    {
        var today = Today();
        var customers = await _customers.ListByPropertyManagerAsync(pmId, ct);
        var leases = await _leases.ListActiveAsync(pmId, today, ct);
        var leasesByCustomer = leases.Where(l => l.CustomerId is not null)
                                     .ToLookup(l => l.CustomerId!.Value);

        var matches = customers
            .Where(c => c.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (c.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(15)
            .Select(c => new
            {
                customerId = c.Id,
                name = c.DisplayName,
                email = c.Email,
                phone = c.Phone,
                active = c.IsActive,
                leases = leasesByCustomer[c.Id].Select(l => new
                {
                    leaseId = l.Id,
                    unit = l.UnitLabel,
                    monthlyRent = l.MonthlyRent,
                    endDate = l.EndDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    autopay = l.AutopayStatus.ToString().ToLowerInvariant()
                }).ToList()
            }).ToList();

        return JsonSerializer.Serialize(new { query, count = matches.Count, tenants = matches }, Json);
    }

    private async Task<string> GetLeaseDetailsAsync(int pmId, int leaseId, CancellationToken ct)
    {
        var today = Today();

        // GetByIdAsync is PM-scoped: a lease belonging to another PM comes back
        // null, so the model can't probe other workspaces by guessing ids.
        var lease = await _leases.GetByIdAsync(leaseId, pmId, ct);
        if (lease is null)
            return Error($"No lease with id {leaseId} in this portfolio.");

        Customer? customer = null;
        if (lease.CustomerId is int cid)
        {
            customer = await _customers.GetByIdAsync(cid, ct);
            if (customer is not null && customer.PropertyManagerId != pmId) customer = null;
        }

        var currentDueDate = await _invoices.GetCurrentDueDateAsync(leaseId, today, ct);
        var payUrl = await _invoices.GetCurrentPayUrlAsync(leaseId, today, ct);

        var allInvoices = await _invoices.ListByPropertyManagerAsync(pmId, ct);
        var leaseInvoices = allInvoices
            .Where(i => i.LeaseId == leaseId)
            .OrderByDescending(i => i.DueDate)
            .Take(12)
            .Select(i => new
            {
                invoiceId = i.Id,
                invoiceNumber = i.ExternalDocNumber,
                dueDate = i.DueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                totalAmount = i.TotalAmount,
                balance = i.Balance,
                status = i.Status.ToString().ToLowerInvariant()
            }).ToList();

        return JsonSerializer.Serialize(new
        {
            leaseId = lease.Id,
            unit = lease.UnitLabel,
            monthlyRent = lease.MonthlyRent,
            startDate = lease.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            endDate = lease.EndDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            liveForProcessing = !lease.FeesStaged,
            autopay = lease.AutopayStatus.ToString().ToLowerInvariant(),
            lateFee = new
            {
                type = lease.LateFeeType switch
                {
                    1 => "flat", 2 => "percent", 3 => "greater_of", 4 => "lesser_of",
                    _ => null
                },
                flatAmount = lease.LateFeeFlatAmount,
                percent = lease.LateFeePercent,
                graceDays = lease.LateFeeGraceDays,
                dailyAccrual = lease.LateFeeDailyAccrual
            },
            tenant = customer is null ? null : new
            {
                customerId = customer.Id,
                name = customer.DisplayName,
                email = customer.Email,
                phone = customer.Phone
            },
            currentDueDate = currentDueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            hasOnlinePayLink = !string.IsNullOrEmpty(payUrl),
            recentInvoices = leaseInvoices
        }, Json);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = message }, Json);

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i
            : null;
}
