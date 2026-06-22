using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DueMap.Integrations.Accounting.Sync;

/// <summary>
/// QuickBooks Online v3 client. Uses the Query endpoint with QBO's SQL-like
/// syntax. Sandbox base URL is used when <c>Integrations:QuickBooks:Environment</c>
/// is "sandbox" (default).
/// </summary>
internal sealed class QuickBooksAccountingClient : IAccountingDataClient
{
    private const string ProdBase    = "https://quickbooks.api.intuit.com";
    private const string SandboxBase = "https://sandbox-quickbooks.api.intuit.com";
    private const string MinorVersion = "70";

    private readonly HttpClient _http;
    private readonly IntegrationsOptions.QuickBooksOptions _opts;

    public AccountingProvider Provider => AccountingProvider.QuickBooks;

    public QuickBooksAccountingClient(HttpClient http, IOptions<IntegrationsOptions> options)
    {
        _http = http;
        _opts = options.Value.QuickBooks;
    }

    public async Task<IReadOnlyList<SyncedCustomer>> ListCustomersAsync(
        string accessToken, string realmId, DateTime? modifiedSinceUtc, CancellationToken ct)
    {
        var where = modifiedSinceUtc is DateTime t
            ? $" WHERE MetaData.LastUpdatedTime >= '{FormatIso(t)}'"
            : string.Empty;

        var query = $"SELECT Id, DisplayName, PrimaryEmailAddr, PrimaryPhone, Active FROM Customer{where} MAXRESULTS 1000";
        var response = await GetQueryAsync<QbCustomerResponse>(accessToken, realmId, query, ct);

        return response.QueryResponse.Customer?.Select(MapCustomer).ToArray()
            ?? Array.Empty<SyncedCustomer>();
    }

    public async Task<IReadOnlyList<SyncedInvoice>> ListInvoicesAsync(
        string accessToken, string realmId, DateTime? modifiedSinceUtc, CancellationToken ct)
    {
        var where = modifiedSinceUtc is DateTime t
            ? $" WHERE MetaData.LastUpdatedTime >= '{FormatIso(t)}'"
            : string.Empty;

        // NOTE: QBO's query endpoint rejects InvoiceLink as a selectable column
        //   ("QueryValidationError: Property InvoiceLink not found for Entity Invoice")
        // even though it's a documented field on the Invoice entity. The field
        // IS returned by SELECT * — so we read the whole row and let the JSON
        // deserialiser pick up InvoiceLink when it's present. Costs us slightly
        // bigger payloads in exchange for a working sync + still getting the
        // public payment URL (task #62).
        var query = $"SELECT * FROM Invoice{where} MAXRESULTS 1000";
        var response = await GetQueryAsync<QbInvoiceResponse>(accessToken, realmId, query, ct);

        return response.QueryResponse.Invoice?.Select(MapInvoice).ToArray()
            ?? Array.Empty<SyncedInvoice>();
    }

    public async Task<AccountingCompanyInfo> GetCompanyInfoAsync(
        string accessToken, string realmId, CancellationToken ct)
    {
        // CompanyInfo endpoint: GET /v3/company/{realmId}/companyinfo/{realmId}
        // QBO doesn't return an IANA tz here, but it gives us Country and the
        // legal-address state — enough to make a reasonable guess in the
        // suggester layer.
        var baseUrl = string.Equals(_opts.Environment, "production", StringComparison.OrdinalIgnoreCase)
            ? ProdBase : SandboxBase;
        var url = $"{baseUrl}/v3/company/{Uri.EscapeDataString(realmId)}/companyinfo/{Uri.EscapeDataString(realmId)}" +
                  $"?minorversion={MinorVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"QuickBooks CompanyInfo returned {(int)response.StatusCode}: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<QbCompanyInfoResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("QuickBooks CompanyInfo response was empty.");

        var c = payload.CompanyInfo;
        return new AccountingCompanyInfo(
            CompanyName:   c.CompanyName ?? c.LegalName ?? "(unnamed company)",
            CountryCode:   NormalizeCountry(c.Country),
            RegionCode:    c.LegalAddr?.CountrySubDivisionCode,
            RawTimeZoneId: null,                              // QBO doesn't expose a tz id on CompanyInfo
            PrimaryEmail:  c.Email?.Address);                 // contact email — present for most companies
    }

    // QBO returns either ISO codes ("US") or full names ("United States").
    // Normalise to ISO so the suggester only has to handle one shape.
    private static string? NormalizeCountry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.Length == 2) return raw.ToUpperInvariant();
        return raw.Trim().ToUpperInvariant() switch
        {
            "UNITED STATES" or "USA" or "U.S." or "U.S.A."   => "US",
            "UNITED KINGDOM" or "GREAT BRITAIN"              => "GB",
            "AUSTRALIA"                                       => "AU",
            "CANADA"                                          => "CA",
            "INDIA"                                           => "IN",
            "GERMANY"                                         => "DE",
            _                                                 => null
        };
    }

    public async Task<byte[]?> GetInvoicePdfAsync(
        string accessToken, string realmId, string invoiceExternalId, CancellationToken ct)
    {
        // QBO renders the invoice PDF on demand:
        //   GET /v3/company/{realmId}/invoice/{id}/pdf
        // Response is application/pdf. 404 maps to null (invoice deleted / not
        // PDF-able); other non-success codes throw so the caller can decide.
        var baseUrl = string.Equals(_opts.Environment, "production", StringComparison.OrdinalIgnoreCase)
            ? ProdBase : SandboxBase;
        var url = $"{baseUrl}/v3/company/{Uri.EscapeDataString(realmId)}/invoice/{Uri.EscapeDataString(invoiceExternalId)}/pdf";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));

        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"QuickBooks invoice PDF returned {(int)response.StatusCode}: {body}");
        }
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<T> GetQueryAsync<T>(string accessToken, string realmId, string query, CancellationToken ct)
    {
        var baseUrl = string.Equals(_opts.Environment, "production", StringComparison.OrdinalIgnoreCase)
            ? ProdBase : SandboxBase;

        var url = $"{baseUrl}/v3/company/{Uri.EscapeDataString(realmId)}/query" +
                  $"?query={Uri.EscapeDataString(query)}&minorversion={MinorVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"QuickBooks API returned {(int)response.StatusCode}: {body}");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("QuickBooks response was empty.");
    }

    private static SyncedCustomer MapCustomer(QbCustomer c) => new(
        ExternalId: c.Id,
        DisplayName: c.DisplayName ?? "(no name)",
        Email: c.PrimaryEmailAddr?.Address,
        Phone: c.PrimaryPhone?.FreeFormNumber,
        IsActive: c.Active ?? true,
        BillingState: NormaliseState(c.BillAddr?.CountrySubDivisionCode));

    /// <summary>
    /// QBO stores state as <c>CountrySubDivisionCode</c>, but the value is
    /// free-form text — sandbox companies often have "California" or "ca ".
    /// We accept anything that uppercases to exactly 2 alpha chars; anything
    /// else becomes NULL so the DiscoveryService treats it as ambiguous.
    /// </summary>
    private static string? NormaliseState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length != 2) return null;
        var upper = trimmed.ToUpperInvariant();
        for (var i = 0; i < 2; i++)
        {
            if (upper[i] < 'A' || upper[i] > 'Z') return null;
        }
        return upper;
    }

    private static SyncedInvoice MapInvoice(QbInvoice i)
    {
        // QB doesn't expose 'voided'/'paid' as a single status; derive it.
        // Balance == 0 with TotalAmt > 0 → paid; otherwise open. Voided invoices
        // need a separate query (Status = 'Void') in a later refinement.
        var status = (i.Balance ?? i.TotalAmt) <= 0m && (i.TotalAmt ?? 0m) > 0m
            ? SyncedInvoiceStatus.Paid
            : SyncedInvoiceStatus.Open;

        return new SyncedInvoice(
            ExternalId: i.Id,
            CustomerExternalId: i.CustomerRef.Value,
            ExternalDocNumber: i.DocNumber,
            IssueDate: ParseDate(i.TxnDate),
            DueDate: ParseDate(i.DueDate) ?? ParseDate(i.TxnDate) ?? DateOnly.FromDateTime(DateTime.UtcNow),
            TotalAmount: i.TotalAmt ?? 0m,
            Balance: i.Balance ?? i.TotalAmt ?? 0m,
            Currency: i.CurrencyRef?.Value ?? "USD",
            Status: status,
            // InvoiceLink is populated only when the QBO company has
            // "Enable online payments" turned on. Empty string normalises to
            // null so the renderer's {% if pay_url %} check works either way.
            PublicPaymentUrl: string.IsNullOrWhiteSpace(i.InvoiceLink) ? null : i.InvoiceLink);
    }

    private static DateOnly? ParseDate(string? iso) =>
        string.IsNullOrEmpty(iso) ? null
        : DateOnly.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string FormatIso(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private sealed record QbCustomerResponse(
        [property: JsonPropertyName("QueryResponse")] QbCustomerResponseInner QueryResponse);
    private sealed record QbCustomerResponseInner(
        [property: JsonPropertyName("Customer")] List<QbCustomer>? Customer);

    private sealed record QbCustomer(
        [property: JsonPropertyName("Id")]                string Id,
        [property: JsonPropertyName("DisplayName")]       string? DisplayName,
        [property: JsonPropertyName("PrimaryEmailAddr")]  QbEmail? PrimaryEmailAddr,
        [property: JsonPropertyName("PrimaryPhone")]      QbPhone? PrimaryPhone,
        [property: JsonPropertyName("Active")]            bool? Active,
        // v18 (P1-1) — billing address for state inference.
        [property: JsonPropertyName("BillAddr")]          QbAddr? BillAddr);
    private sealed record QbEmail([property: JsonPropertyName("Address")] string? Address);
    private sealed record QbPhone([property: JsonPropertyName("FreeFormNumber")] string? FreeFormNumber);

    private sealed record QbInvoiceResponse(
        [property: JsonPropertyName("QueryResponse")] QbInvoiceResponseInner QueryResponse);
    private sealed record QbInvoiceResponseInner(
        [property: JsonPropertyName("Invoice")] List<QbInvoice>? Invoice);

    private sealed record QbInvoice(
        [property: JsonPropertyName("Id")]          string Id,
        [property: JsonPropertyName("DocNumber")]   string? DocNumber,
        [property: JsonPropertyName("TxnDate")]     string? TxnDate,
        [property: JsonPropertyName("DueDate")]     string? DueDate,
        [property: JsonPropertyName("TotalAmt")]    decimal? TotalAmt,
        [property: JsonPropertyName("Balance")]     decimal? Balance,
        [property: JsonPropertyName("CurrencyRef")] QbRef? CurrencyRef,
        [property: JsonPropertyName("CustomerRef")] QbRef CustomerRef,
        [property: JsonPropertyName("InvoiceLink")] string? InvoiceLink);
    private sealed record QbRef(
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("name")]  string? Name);

    private sealed record QbCompanyInfoResponse(
        [property: JsonPropertyName("CompanyInfo")] QbCompanyInfo CompanyInfo);
    private sealed record QbCompanyInfo(
        [property: JsonPropertyName("CompanyName")] string? CompanyName,
        [property: JsonPropertyName("LegalName")]   string? LegalName,
        [property: JsonPropertyName("Country")]     string? Country,
        [property: JsonPropertyName("LegalAddr")]   QbAddr?  LegalAddr,
        [property: JsonPropertyName("Email")]       QbEmailAddr? Email);
    private sealed record QbAddr(
        [property: JsonPropertyName("CountrySubDivisionCode")] string? CountrySubDivisionCode);
    private sealed record QbEmailAddr(
        [property: JsonPropertyName("Address")] string? Address);
}
