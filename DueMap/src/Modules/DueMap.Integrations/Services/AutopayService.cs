using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Accounting;

namespace DueMap.Integrations.Services;

/// <summary>
/// Default <see cref="IAutopayService"/>. The "deep link" is the invoice's
/// provider-hosted online pay page (already synced as
/// <c>RentInvoice.PublicPaymentUrl</c>) — that's where the tenant opts into
/// autopay on the provider's own rails. DueMap never collects funds.
/// </summary>
internal sealed class AutopayService : IAutopayService
{
    private const string AutopayFlag = "integrations.autopay";

    private readonly IFeatureFlags _flags;

    public AutopayService(IFeatureFlags flags) => _flags = flags;

    public async Task<string?> BuildSetupUrlAsync(AccountingProvider provider, string? invoicePayUrl, CancellationToken ct)
    {
        if (!await _flags.IsEnabledAsync(AutopayFlag, propertyManagerId: null, ct))
        {
            return null;
        }

        // Both QBO (InvoiceLink) and Xero (OnlineInvoiceUrl) expose autopay
        // opt-in on the hosted invoice page; we return that URL as-is. The
        // provider switch is the seam for any future per-provider tweak.
        return provider switch
        {
            AccountingProvider.QuickBooks => NullIfBlank(invoicePayUrl),
            AccountingProvider.Xero       => NullIfBlank(invoicePayUrl),
            _                             => null
        };
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
