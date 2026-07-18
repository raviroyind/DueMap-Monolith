namespace DueMap.Integrations;

public sealed class IntegrationsOptions
{
    public const string SectionName = "Integrations";

    public string? ConnectionString { get; set; }

    public SendGridOptions  SendGrid   { get; set; } = new();
    public TwilioOptions    Twilio     { get; set; } = new();
    public QuickBooksOptions QuickBooks { get; set; } = new();
    public XeroOptions       Xero       { get; set; } = new();

    public sealed class SendGridOptions
    {
        public string? ApiKey { get; set; }
        public string  FromEmail { get; set; } = string.Empty;
        public string  FromName  { get; set; } = "DueMap";
    }

    public sealed class TwilioOptions
    {
        public string? AccountSid { get; set; }
        public string? AuthToken { get; set; }
        public string? FromNumber { get; set; }
    }

    public sealed class QuickBooksOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>
        /// OAuth scopes. OpenID scopes are included so "Sign in with Intuit"
        /// can identify the HUMAN (id_token / userinfo email) instead of the
        /// QuickBooks COMPANY's contact email — the company email is shared
        /// (sandbox: noreply@quickbooks.com) and made the signed-in identity
        /// display wrong (QA P3). One consent covers sign-in + books.
        /// </summary>
        public string Scopes { get; set; } = "openid profile email com.intuit.quickbooks.accounting";

        /// <summary>"sandbox" for Intuit's sandbox API; "production" otherwise.</summary>
        public string Environment { get; set; } = "sandbox";
    }

    public sealed class XeroOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Space-separated OAuth scopes. GRANULAR read scopes (Xero's 2026
        /// model): the broad accounting.transactions / accounting.contacts are
        /// rejected as invalid_scope on apps created from ~Mar 2026 onward.
        /// DueMap only READS from Xero — invoices, contacts, and the org name
        /// (settings) — so read-only granular scopes are the minimal correct
        /// set. openid/profile/email identify the org; offline_access gives us
        /// the refresh token. Ref: developer.xero.com/faq/granular-scopes
        /// </summary>
        public string Scopes { get; set; } = "openid profile email accounting.contacts.read accounting.invoices.read accounting.settings.read offline_access";
    }
}
