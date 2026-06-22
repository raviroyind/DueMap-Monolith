using DueMap.Tenancy.Domain;

namespace DueMap.Integrations.Accounting;

/// <summary>
/// Autopay deep links + status inference (P2-1). DueMap never collects funds —
/// it routes tenants to the accounting provider's OWN hosted pay/autopay page
/// (the same online-invoice URL DueMap already syncs) and infers enrollment
/// from payment behavior, because neither QBO nor Xero exposes a first-party
/// "enrolled?" flag via API.
///
/// Gated by <c>integrations.autopay</c>: when off, <see cref="BuildSetupUrlAsync"/>
/// returns null (no deep link) and the sync read-back is skipped.
/// </summary>
public interface IAutopayService
{
    /// <summary>
    /// The "Set up autopay" deep link for an invoice — its provider-hosted
    /// online pay page (QBO InvoiceLink / Xero OnlineInvoiceUrl), where autopay
    /// opt-in happens. Returns null when the flag is off or no pay URL exists.
    /// </summary>
    Task<string?> BuildSetupUrlAsync(AccountingProvider provider, string? invoicePayUrl, CancellationToken ct);
}

/// <summary>
/// Pure autopay-status inference (P2-1). Behavioral proxy: a lease whose
/// past-due-dated invoices are consistently paid is treated as enrolled
/// (reliable on-time payer) so it isn't nagged. No I/O — fully testable.
/// </summary>
public static class AutopayStatusInference
{
    // A lease needs at least this many past-due-dated invoices before we'll
    // call it either way; fewer is "Unknown".
    private const int MinHistory = 2;
    // Share of those invoices that must be paid to count as "enrolled".
    private const double EnrolledThreshold = 0.8;

    /// <param name="pastDueDatedInvoices">
    /// One entry per invoice whose due date has already passed:
    /// <c>IsPaid</c> = balance cleared.
    /// </param>
    public static AutopayStatus Infer(IReadOnlyList<bool> pastDueDatedInvoicesPaid)
    {
        ArgumentNullException.ThrowIfNull(pastDueDatedInvoicesPaid);
        if (pastDueDatedInvoicesPaid.Count < MinHistory) return AutopayStatus.Unknown;

        var paid = pastDueDatedInvoicesPaid.Count(p => p);
        var ratio = (double)paid / pastDueDatedInvoicesPaid.Count;
        return ratio >= EnrolledThreshold ? AutopayStatus.Enrolled : AutopayStatus.None;
    }
}
