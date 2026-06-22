using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DueMap.Integrations.Accounting.Sync;

/// <summary>
/// Xero v2 client. Auth is bearer + the required <c>Xero-Tenant-Id</c> header
/// carrying the realm. Delta is via <c>If-Modified-Since</c>.
/// </summary>
internal sealed class XeroAccountingClient : IAccountingDataClient
{
    private const string Base = "https://api.xero.com/api.xro/2.0";

    private readonly HttpClient _http;

    public AccountingProvider Provider => AccountingProvider.Xero;

    public XeroAccountingClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<SyncedCustomer>> ListCustomersAsync(
        string accessToken, string realmId, DateTime? modifiedSinceUtc, CancellationToken ct)
    {
        // Xero "Contacts" are the customer record set. Filter to customer-like
        // contacts (IsCustomer = true). Real-world: Xero treats anyone you can
        // invoice as a contact; this filter keeps suppliers/employees out.
        var response = await GetAsync<XeroContactsResponse>(accessToken, realmId,
            "/Contacts?where=IsCustomer==true", modifiedSinceUtc, ct);

        return response.Contacts?.Select(MapContact).ToArray() ?? Array.Empty<SyncedCustomer>();
    }

    public async Task<IReadOnlyList<SyncedInvoice>> ListInvoicesAsync(
        string accessToken, string realmId, DateTime? modifiedSinceUtc, CancellationToken ct)
    {
        // ACCREC = accounts receivable, i.e. invoices we billed customers.
        var response = await GetAsync<XeroInvoicesResponse>(accessToken, realmId,
            "/Invoices?where=Type==\"ACCREC\"", modifiedSinceUtc, ct);

        var invoices = response.Invoices?.Select(MapInvoice).ToArray() ?? Array.Empty<SyncedInvoice>();

        // OnlineInvoiceUrl lives on a separate endpoint (one call per invoice).
        // Only fetch for OPEN invoices — paid/voided ones never get a reminder
        // with a Pay Now button, so the extra round-trip would be wasted. This
        // also keeps us well under Xero's 60 calls/minute rate limit on
        // typical-sized portfolios.
        var enriched = new List<SyncedInvoice>(invoices.Length);
        foreach (var inv in invoices)
        {
            if (inv.Status == SyncedInvoiceStatus.Open)
            {
                var url = await TryFetchOnlineUrlAsync(accessToken, realmId, inv.ExternalId, ct);
                enriched.Add(inv with { PublicPaymentUrl = url });
            }
            else
            {
                enriched.Add(inv);
            }
        }
        return enriched;
    }

    public async Task<AccountingCompanyInfo> GetCompanyInfoAsync(
        string accessToken, string realmId, CancellationToken ct)
    {
        // GET /Organisation returns an array; the connected tenant is at [0].
        // Xero conveniently includes a Windows-style Timezone string that we
        // can map to IANA in the suggester layer.
        var response = await GetAsync<XeroOrganisationResponse>(
            accessToken, realmId, "/Organisation", modifiedSinceUtc: null, ct);

        var org = response.Organisations?.FirstOrDefault()
            ?? throw new InvalidOperationException("Xero returned no Organisation rows.");

        return new AccountingCompanyInfo(
            CompanyName:   org.Name ?? "(unnamed organisation)",
            CountryCode:   string.IsNullOrWhiteSpace(org.CountryCode) ? null : org.CountryCode.ToUpperInvariant(),
            RegionCode:    null,    // Xero doesn't expose a sub-division here
            RawTimeZoneId: org.Timezone,
            PrimaryEmail:  null);   // Xero exposes contact email per-organisation differently; out of scope for sign-in v1
    }

    public async Task<byte[]?> GetInvoicePdfAsync(
        string accessToken, string realmId, string invoiceExternalId, CancellationToken ct)
    {
        // Xero renders an invoice as PDF when Accept: application/pdf is set
        // on the GET /Invoices/{id} call. 404 -> null; other failures throw so
        // the caller distinguishes "no PDF" from "transient API trouble."
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{Base}/Invoices/{Uri.EscapeDataString(invoiceExternalId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Xero-Tenant-Id", realmId);
        request.Headers.Accept.Clear();
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
                $"Xero invoice PDF returned {(int)response.StatusCode}: {body}");
        }
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<string?> TryFetchOnlineUrlAsync(string accessToken, string realmId, string invoiceId, CancellationToken ct)
    {
        try
        {
            var resp = await GetAsync<XeroOnlineInvoiceResponse>(accessToken, realmId,
                $"/Invoices/{Uri.EscapeDataString(invoiceId)}/OnlineInvoice", modifiedSinceUtc: null, ct);
            return resp.OnlineInvoices?.FirstOrDefault()?.OnlineInvoiceUrl;
        }
        catch (InvalidOperationException)
        {
            // Online invoices aren't enabled for this org, or the endpoint
            // returned an error. Sync continues without the URL; the Pay Now
            // button just won't render for this row (Scriban {% if pay_url %}).
            return null;
        }
    }

    private async Task<T> GetAsync<T>(string accessToken, string realmId, string pathAndQuery, DateTime? modifiedSinceUtc, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}{pathAndQuery}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Xero-Tenant-Id", realmId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (modifiedSinceUtc is DateTime t)
        {
            request.Headers.IfModifiedSince = new DateTimeOffset(t.ToUniversalTime());
        }

        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            // No changes since the supplied timestamp. Synthesize an empty payload.
            return Activator.CreateInstance<T>()
                ?? throw new InvalidOperationException("Could not synthesize empty response.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Xero API returned {(int)response.StatusCode}: {body}");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Xero response was empty.");
    }

    private static SyncedCustomer MapContact(XeroContact c) => new(
        ExternalId: c.ContactID,
        DisplayName: c.Name ?? "(no name)",
        Email: c.EmailAddress,
        Phone: c.Phones?.FirstOrDefault(p => string.Equals(p.PhoneType, "DEFAULT", StringComparison.OrdinalIgnoreCase))?.PhoneNumber
             ?? c.Phones?.FirstOrDefault()?.PhoneNumber,
        IsActive: !string.Equals(c.ContactStatus, "ARCHIVED", StringComparison.OrdinalIgnoreCase),
        // v18 (P1-1) — billing state. Xero exposes addresses by AddressType
        // (POBOX, STREET); we take the first address with a non-empty Region
        // and normalise to 2-letter uppercase. Anything weird → null.
        BillingState: NormaliseState(c.Addresses?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Region))?.Region));

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

    private static SyncedInvoice MapInvoice(XeroInvoice i) => new(
        ExternalId: i.InvoiceID,
        CustomerExternalId: i.Contact.ContactID,
        ExternalDocNumber: i.InvoiceNumber,
        IssueDate: ParseDate(i.DateString),
        DueDate: ParseDate(i.DueDateString) ?? ParseDate(i.DateString) ?? DateOnly.FromDateTime(DateTime.UtcNow),
        TotalAmount: i.Total ?? 0m,
        Balance: i.AmountDue ?? 0m,
        Currency: i.CurrencyCode ?? "USD",
        Status: MapStatus(i.Status),
        PublicPaymentUrl: null);   // enriched later in ListInvoicesAsync for Open invoices only

    private static SyncedInvoiceStatus MapStatus(string? raw) => raw?.ToUpperInvariant() switch
    {
        "PAID"     => SyncedInvoiceStatus.Paid,
        "VOIDED"   => SyncedInvoiceStatus.Voided,
        "DELETED"  => SyncedInvoiceStatus.Voided,
        _          => SyncedInvoiceStatus.Open  // DRAFT, SUBMITTED, AUTHORISED
    };

    private static DateOnly? ParseDate(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }
        return null;
    }

    private sealed record XeroContactsResponse(
        [property: JsonPropertyName("Contacts")] List<XeroContact>? Contacts);
    private sealed record XeroContact(
        [property: JsonPropertyName("ContactID")]     string ContactID,
        [property: JsonPropertyName("Name")]          string? Name,
        [property: JsonPropertyName("EmailAddress")]  string? EmailAddress,
        [property: JsonPropertyName("Phones")]        List<XeroPhone>? Phones,
        [property: JsonPropertyName("ContactStatus")] string? ContactStatus,
        // v18 (P1-1) — address surface for billing-state inference.
        [property: JsonPropertyName("Addresses")]     List<XeroAddress>? Addresses);

    private sealed record XeroAddress(
        [property: JsonPropertyName("AddressType")] string? AddressType,
        [property: JsonPropertyName("Region")]      string? Region);
    private sealed record XeroPhone(
        [property: JsonPropertyName("PhoneType")]   string? PhoneType,
        [property: JsonPropertyName("PhoneNumber")] string? PhoneNumber);

    private sealed record XeroInvoicesResponse(
        [property: JsonPropertyName("Invoices")] List<XeroInvoice>? Invoices);
    private sealed record XeroInvoice(
        [property: JsonPropertyName("InvoiceID")]     string InvoiceID,
        [property: JsonPropertyName("InvoiceNumber")] string? InvoiceNumber,
        [property: JsonPropertyName("DateString")]    string? DateString,
        [property: JsonPropertyName("DueDateString")] string? DueDateString,
        [property: JsonPropertyName("Total")]         decimal? Total,
        [property: JsonPropertyName("AmountDue")]     decimal? AmountDue,
        [property: JsonPropertyName("CurrencyCode")]  string? CurrencyCode,
        [property: JsonPropertyName("Status")]        string? Status,
        [property: JsonPropertyName("Contact")]       XeroContactRef Contact);
    private sealed record XeroContactRef(
        [property: JsonPropertyName("ContactID")] string ContactID);

    private sealed record XeroOnlineInvoiceResponse(
        [property: JsonPropertyName("OnlineInvoices")] List<XeroOnlineInvoice>? OnlineInvoices);
    private sealed record XeroOnlineInvoice(
        [property: JsonPropertyName("OnlineInvoiceUrl")] string? OnlineInvoiceUrl);

    private sealed record XeroOrganisationResponse(
        [property: JsonPropertyName("Organisations")] List<XeroOrganisation>? Organisations);
    private sealed record XeroOrganisation(
        [property: JsonPropertyName("Name")]        string? Name,
        [property: JsonPropertyName("CountryCode")] string? CountryCode,
        [property: JsonPropertyName("Timezone")]    string? Timezone);
}
