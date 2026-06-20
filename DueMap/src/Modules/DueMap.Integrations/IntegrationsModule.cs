using DueMap.Common.Modularity;
using DueMap.Integrations.Accounting;
using DueMap.Integrations.Accounting.OAuth;
using DueMap.Integrations.Accounting.Services;
using DueMap.Integrations.Accounting.Sync;
using DueMap.Integrations.Notices;
using DueMap.Integrations.Persistence;
using DueMap.Integrations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SendGrid;
using Twilio.Clients;

namespace DueMap.Integrations;

public sealed class IntegrationsModule : IModule
{
    public string Name => "Integrations";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IntegrationsOptions>()
            .Bind(configuration.GetSection(IntegrationsOptions.SectionName));

        // ---- Notice dispatch (SendGrid + Twilio) ----
        // SendGridClient's constructor throws on null OR empty OR whitespace
        // apiKey — so we can't pass `?? string.Empty`. When the key isn't
        // configured (dev environment without secrets) we substitute a syntactically
        // valid placeholder so DI resolution succeeds; SendGridEmailSender then
        // detects the missing config at SendAsync time and routes through the
        // dev console-logger fallback instead of attempting a live send.
        services.AddSingleton<ISendGridClient>(sp =>
        {
            var apiKey = sp.GetRequiredService<IOptions<IntegrationsOptions>>().Value.SendGrid.ApiKey;
            return new SendGridClient(string.IsNullOrWhiteSpace(apiKey) ? "SG.not-configured" : apiKey);
        });

        services.AddHttpClient();
        services.AddSingleton<ITwilioRestClient>(sp =>
        {
            // Same dev-safety pattern as ISendGridClient above — Twilio's
            // TwilioRestClient also throws on empty/whitespace credentials.
            // Real send is gated by TwilioSmsSender, which checks the From
            // number before attempting delivery.
            var twilio = sp.GetRequiredService<IOptions<IntegrationsOptions>>().Value.Twilio;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("twilio");
            var sid   = string.IsNullOrWhiteSpace(twilio.AccountSid) ? "ACnotconfigured0000000000000000000" : twilio.AccountSid;
            var token = string.IsNullOrWhiteSpace(twilio.AuthToken)  ? "notconfigured"                       : twilio.AuthToken;
            return new TwilioRestClient(sid, token,
                httpClient: new Twilio.Http.SystemNetHttpClient(http));
        });

        services.AddScoped<IEmailSender, SendGridEmailSender>();
        services.AddScoped<ISmsSender, TwilioSmsSender>();
        services.AddScoped<INoticeDispatcher, NoticeDispatcher>();

        // Pulls the authoritative invoice PDF from QBO/Xero for the current
        // rent period — attached to late_fee_notice emails by ActionExecutor.
        services.AddScoped<ILateFeeInvoiceAttachmentFetcher, LateFeeInvoiceAttachmentFetcher>();

        // ---- Accounting OAuth + connections ----
        var connectionString = configuration.GetSection(IntegrationsOptions.SectionName)["ConnectionString"]
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Integrations module: no connection string. Set ConnectionStrings:Default or Integrations:ConnectionString.");

        // DbContext registered TWICE intentionally:
        //   * IDbContextFactory<IntegrationsDbContext> for services that need
        //     a fresh per-call context (AccountingConnectionService — it's hit
        //     concurrently from MainLayout's gate check AND the page lifecycle,
        //     and a scoped instance can't survive that race).
        //   * Scoped IntegrationsDbContext for repos still wired the old way
        //     (PmAccountingSyncService, OAuth providers) until they're migrated.
        // Both share one connection-string config. Calling site picks which
        // pattern fits — DbContext factory for read-then-dispose, scoped for
        // multi-step Worker-only flows.
        services.AddDbContextFactory<IntegrationsDbContext>(opt =>
            opt.UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__integrations_migrations", "integrations")));
        services.AddScoped<IntegrationsDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<IntegrationsDbContext>>().CreateDbContext());

        services.AddScoped<ITokenProtector, DataProtectionTokenProtector>();

        services.AddHttpClient<QuickBooksOAuthProvider>();
        services.AddHttpClient<XeroOAuthProvider>();
        services.AddTransient<IOAuthProvider>(sp => sp.GetRequiredService<QuickBooksOAuthProvider>());
        services.AddTransient<IOAuthProvider>(sp => sp.GetRequiredService<XeroOAuthProvider>());

        services.AddScoped<IAccountingConnectionService, AccountingConnectionService>();

        // ---- Accounting sync (customers + invoices → Tenancy) ----
        services.AddHttpClient<QuickBooksAccountingClient>();
        services.AddHttpClient<XeroAccountingClient>();
        services.AddTransient<IAccountingDataClient>(sp => sp.GetRequiredService<QuickBooksAccountingClient>());
        services.AddTransient<IAccountingDataClient>(sp => sp.GetRequiredService<XeroAccountingClient>());

        services.AddScoped<IPmAccountingSync, PmAccountingSyncService>();

        // "First sync on connect" job — enqueued by the Web OAuth callback so
        // a connection always pulls data server-side, even if the user never
        // completes the syncing wizard. Executed by the Worker's Hangfire
        // server; the Web host only enqueues it.
        services.AddScoped<Accounting.Jobs.IInitialSyncJob, Accounting.Jobs.InitialSyncJob>();

        // Used by the onboarding Daily Close form to pre-fill timezone from
        // the connected accounting system. Doesn't persist anything itself —
        // pure read + map.
        services.AddScoped<IAccountingTimezoneSuggester, AccountingTimezoneSuggester>();

        // ---- Ops alerting sinks (P0-4) ----
        // The Common module registered NullAlertSink as the default; we
        // OVERRIDE it here based on Ops:Alerts:Channel so the call chain
        // is just one resolve regardless of which sink is active. Sinks
        // are singletons — they hold one HttpClient or one IEmailSender
        // reference and are stateless beyond that.
        var alertChannel = configuration["Ops:Alerts:Channel"]?.Trim().ToLowerInvariant();
        if (alertChannel == "email")
        {
            services.AddSingleton<DueMap.Common.Ops.IAlertSink, DueMap.Integrations.Ops.EmailAlertSink>();
        }
        else if (alertChannel == "webhook")
        {
            services.AddHttpClient<DueMap.Integrations.Ops.WebhookAlertSink>();
            services.AddSingleton<DueMap.Common.Ops.IAlertSink>(sp =>
                sp.GetRequiredService<DueMap.Integrations.Ops.WebhookAlertSink>());
        }
        // else: leave NullAlertSink in place (Common's default).
    }
}
