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

        /// <summary>Default scopes for accounting access.</summary>
        public string Scopes { get; set; } = "com.intuit.quickbooks.accounting";

        /// <summary>"sandbox" for Intuit's sandbox API; "production" otherwise.</summary>
        public string Environment { get; set; } = "sandbox";
    }

    public sealed class XeroOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>Space-separated OAuth scopes.</summary>
        public string Scopes { get; set; } = "openid profile email accounting.transactions accounting.contacts offline_access";
    }
}
