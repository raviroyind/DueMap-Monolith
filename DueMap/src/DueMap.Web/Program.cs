using System.Security.Claims;
using Hangfire;
using Hangfire.SqlServer;
using DueMap.Billing;
using DueMap.Common.Modularity;
using DueMap.Common.FeatureFlags;
using DueMap.Common.Ops;
using DueMap.Identity;
using DueMap.Integrations;
using DueMap.Integrations.Accounting;
using DueMap.Notices;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Web.Components;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging end-to-end; sinks configured via Serilog section of appsettings.
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// -------------------------------------------------------------------------
// Module registration. The same list runs in DueMap.Worker so behavior
// is identical regardless of which process is calling a module's services.
// -------------------------------------------------------------------------
builder.Services.AddModules(builder.Configuration,
    new IdentityModule(),
    new TenancyModule(),
    new RulesModule(),
    new NoticesModule(),
    new BillingModule(),
    new IntegrationsModule());

// -------------------------------------------------------------------------
// Data Protection — encrypts OAuth tokens at rest. Web and Worker MUST share
// the same keyring or tokens written by one can't be read by the other.
// Dev: file system. Prod override via DataProtection:KeyRingPath in appsettings.
// -------------------------------------------------------------------------
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".dataprotection-keys");
builder.Services.AddDataProtection()
    .SetApplicationName("DueMap")
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

// -------------------------------------------------------------------------
// Blazor Server. Components run on the server; UI updates stream over SignalR.
// -------------------------------------------------------------------------
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Razor Pages host the auth surfaces (Login/Register/Logout). The rest of the
// app is Blazor Server.
builder.Services.AddRazorPages();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Claims-driven PM resolution; reads the PmId claim from the auth principal.
builder.Services.AddScoped<DueMap.Web.Services.PmContext>();

// Per-circuit money formatting keyed to the PM's book currency (never the
// host machine's locale — see PmMoney).
builder.Services.AddScoped<DueMap.Web.Services.PmMoney>();

// Stateless Excel/CSV parser used by the rent-roll importer. Singleton is
// safe — no mutable state, just helper methods over ClosedXML.
builder.Services.AddSingleton<DueMap.Web.Services.RentRollParser>();

// Dev-only schema migrator. Applies any unrun db/duemap_schema_v*.sql on
// startup so changing the schema doesn't require a manual sqlcmd run.
// Wired only when the env is Development.
builder.Services.AddScoped<DueMap.Web.Services.DevSchemaBootstrap>();

// Read-only Worker-log reader for the admin dashboard.
builder.Services.AddScoped<DueMap.Web.Services.WorkerLogReader>();

// Tenant-portal session resolver. Reads the dm_tenant cookie and joins
// against tenant_sessions on every authenticated /t/* render.
builder.Services.AddScoped<DueMap.Web.Services.TenantContext>();

// AI assistant chat (Claude). The singleton provider holds the SDK client;
// AssistantService is scoped so each Blazor circuit keeps its own
// conversation history. Missing Anthropic:ApiKey renders a friendly
// "not configured" notice on /assistant instead of failing at startup.
builder.Services.AddOptions<DueMap.Web.Services.Assistant.AssistantOptions>()
    .Bind(builder.Configuration.GetSection(DueMap.Web.Services.Assistant.AssistantOptions.SectionName));
builder.Services.AddSingleton<DueMap.Web.Services.Assistant.AnthropicClientProvider>();
builder.Services.AddScoped<DueMap.Web.Services.Assistant.AssistantDataTools>();
builder.Services.AddScoped<DueMap.Web.Services.Assistant.AssistantService>();

// Feature flags (P0-1): "ship dark, enable per-PM." Web and Worker share
// the same ops.feature_flags table so a toggle takes effect in both
// processes within ~60s. Default is OFF for unknown keys — fail-safe.
builder.Services.AddFeatureFlags(
    builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Default for feature flags."));

// P0-4: operator alerting (dedupe-by-key, fail-soft). Sink choice (email /
// webhook / null) is configured by Ops:Alerts:Channel and bound inside
// IntegrationsModule. Common's AddAlerting registers NullAlertSink as the
// default so resolves always succeed.
builder.Services.AddAlerting(builder.Configuration);

// P0-4: admin Basic-Auth credentials are read from config/secrets, not
// hardcoded. Middleware returns 503 if either Username or Password is
// missing — explicit fail-closed rather than silent dev fallback.
builder.Services.AddOptions<DueMap.Web.Security.AdminBasicAuthOptions>()
    .Bind(builder.Configuration.GetSection(DueMap.Web.Security.AdminBasicAuthOptions.SectionName));

// "Sign in with Intuit" via the QBO OAuth flow. The OIDC handler stays
// registered (further down) for the day Intuit approves the sandbox app
// for true Sign-in-with-Intuit, but the button currently routes through
// this orchestrator because it works against any Connect-to-QuickBooks app.
builder.Services.AddScoped<DueMap.Web.Services.IIntuitSignInOrchestrator,
                           DueMap.Web.Services.IntuitSignInOrchestratorImpl>();

// Dev-only demo seeder. Idempotent — no-op once data exists.
builder.Services.AddScoped<DueMap.Web.Services.DemoSeeder>();

// Default policy: every endpoint requires an authenticated user unless it's
// explicitly marked [AllowAnonymous] (the Razor login/register pages, the
// OAuth callbacks, the webhooks).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// -------------------------------------------------------------------------
// OIDC SSO providers (Phase 2, wave 1: identity scopes only).
// Each scheme is registered ONLY if its ClientId is configured — missing
// config keeps the UI buttons present but they short-circuit to a friendly
// "not configured" notice instead of crashing on Challenge.
//
// Set client credentials with `dotnet user-secrets`:
//   dotnet user-secrets set Integrations:QuickBooks:ClientId   <id>
//   dotnet user-secrets set Integrations:QuickBooks:ClientSecret <secret>
//   dotnet user-secrets set Integrations:Xero:ClientId         <id>
//   dotnet user-secrets set Integrations:Xero:ClientSecret     <secret>
// -------------------------------------------------------------------------
var intuitClientId    = builder.Configuration["Integrations:QuickBooks:ClientId"];
var intuitSecret      = builder.Configuration["Integrations:QuickBooks:ClientSecret"];
var xeroClientId      = builder.Configuration["Integrations:Xero:ClientId"];
var xeroSecret        = builder.Configuration["Integrations:Xero:ClientSecret"];

// We need the AuthenticationBuilder — Identity already called AddAuthentication
// during AddIdentity(...), so a second call here returns the same builder.
var authBuilder = builder.Services.AddAuthentication();

if (!string.IsNullOrWhiteSpace(intuitClientId) && !string.IsNullOrWhiteSpace(intuitSecret))
{
    // We bypass Intuit's OpenID Connect discovery doc and hand-build the
    // configuration. Two reasons:
    //   1. Intuit's discovery is split across two hosts (issuer on
    //      oauth.platform.intuit.com, doc on developer.api.intuit.com), and
    //      the dynamic discovery has been flaky in dev — an early misconfig
    //      with a trailing slash poisoned the ConfigurationManager cache and
    //      kept throwing "Cannot redirect to the authorization endpoint"
    //      even after the URL was fixed, because the empty cached config
    //      survived process restarts in some environments (IIS Express, dotnet watch).
    //   2. Intuit's endpoints have been stable for years. Hardcoding them
    //      buys us reliability at the cost of one manual update if Intuit
    //      ever rotates them.
    //
    // Signing keys (JWKS) still need to come from Intuit so id_token
    // signatures can be verified. We fetch them once at startup; if the
    // fetch fails, sign-in challenges still work (no keys needed to
    // redirect), but the callback's id_token validation will fail until
    // the next restart with working network. That's acceptable here —
    // a JWKS fetch failure at boot loudly logs.
    // Mirrors Intuit's published OpenID Connect discovery document
    // (https://developer.api.intuit.com/.well-known/openid_configuration).
    // We populate the algorithm / scope / response-type / auth-method lists
    // so that downstream token validators see a complete configuration —
    // missing these can cause some validators to skip steps silently.
    var intuitConfig = new OpenIdConnectConfiguration
    {
        Issuer                = "https://oauth.platform.intuit.com/op/v1",
        AuthorizationEndpoint = "https://appcenter.intuit.com/connect/oauth2",
        TokenEndpoint         = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer",
        UserInfoEndpoint      = "https://accounts.platform.intuit.com/v1/openid_connect/userinfo",
        JwksUri               = "https://oauth.platform.intuit.com/op/v1/jwks",
        EndSessionEndpoint    = "https://accounts.platform.intuit.com/v1/openid_connect/logout"
    };
    intuitConfig.IdTokenSigningAlgValuesSupported.Add("RS256");
    intuitConfig.SubjectTypesSupported.Add("public");
    intuitConfig.ResponseTypesSupported.Add("code");
    foreach (var s in new[] { "openid", "email", "profile", "address", "phone" })
        intuitConfig.ScopesSupported.Add(s);
    foreach (var m in new[] { "client_secret_post", "client_secret_basic" })
        intuitConfig.TokenEndpointAuthMethodsSupported.Add(m);
    try
    {
        using var jwksHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var jwksJson = jwksHttp.GetStringAsync(intuitConfig.JwksUri).GetAwaiter().GetResult();
        var keySet = new JsonWebKeySet(jwksJson);
        foreach (var key in keySet.GetSigningKeys()) intuitConfig.SigningKeys.Add(key);
        Log.Information("Intuit OIDC: loaded {KeyCount} signing keys", intuitConfig.SigningKeys.Count);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Intuit OIDC: JWKS fetch failed at startup. Sign-in challenges will still redirect, but id_token validation will fail on callback until next restart with working network.");
    }

    authBuilder.AddOpenIdConnect("Intuit", "Intuit", options =>
    {
        // Configuration is set DIRECTLY — the handler wraps it in a
        // StaticConfigurationManager and skips all dynamic discovery.
        options.Configuration     = intuitConfig;
        options.ClientId          = intuitClientId;
        options.ClientSecret      = intuitSecret;
        options.ResponseType      = OpenIdConnectResponseType.Code;
        options.UsePkce           = true;
        options.SaveTokens        = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath      = "/signin-intuit";
        options.SignedOutCallbackPath = "/signout-intuit";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("com.intuit.quickbooks.accounting");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "email",
            RoleClaimType = "role"
        };

        // Surface OIDC failures loudly instead of a stack trace + log what
        // Intuit's token endpoint actually returned, so future debugging
        // doesn't require packet captures.
        options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
        {
            OnRemoteFailure = ctx =>
            {
                ctx.Response.Redirect("/Account/Login?sso_error=" +
                    Uri.EscapeDataString(ctx.Failure?.Message ?? "Intuit sign-in failed."));
                ctx.HandleResponse();
                return Task.CompletedTask;
            },

            OnTokenResponseReceived = ctx =>
            {
                // The IDX10000 "token cannot be null" error trips when
                // Intuit's token endpoint returns a payload with no
                // id_token. Loud-log the shape of the response so we can
                // tell "OIDC not enabled on app" from "scope rejected"
                // from a genuine parser bug.
                var resp = ctx.TokenEndpointResponse;
                Log.Information(
                    "Intuit OIDC: token response received. has_access_token={Access} has_id_token={Id} has_refresh_token={Refresh} scope=\"{Scope}\" token_type={TokenType}",
                    !string.IsNullOrEmpty(resp?.AccessToken),
                    !string.IsNullOrEmpty(resp?.IdToken),
                    !string.IsNullOrEmpty(resp?.RefreshToken),
                    resp?.Scope ?? "(none)",
                    resp?.TokenType ?? "(none)");
                return Task.CompletedTask;
            }
        };
    });
}

if (!string.IsNullOrWhiteSpace(xeroClientId) && !string.IsNullOrWhiteSpace(xeroSecret))
{
    authBuilder.AddOpenIdConnect("Xero", "Xero", options =>
    {
        // Xero's issuer == its identity host; the OIDC handler will fetch
        // metadata from https://identity.xero.com/.well-known/openid-configuration
        // automatically.
        options.Authority         = "https://identity.xero.com";
        options.ClientId          = xeroClientId;
        options.ClientSecret      = xeroSecret;
        options.ResponseType      = OpenIdConnectResponseType.Code;
        options.UsePkce           = true;
        options.SaveTokens        = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath      = "/signin-xero";
        options.SignedOutCallbackPath = "/signout-xero";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "email",
            RoleClaimType = "role"
        };
    });
}

// Expose which providers are actually wired so the buttons can fall back to
// a friendly stub instead of throwing on Challenge for an unregistered scheme.
builder.Services.AddSingleton(new DueMap.Web.Services.SsoProviderRegistry(
    Intuit: !string.IsNullOrWhiteSpace(intuitClientId) && !string.IsNullOrWhiteSpace(intuitSecret),
    Xero:   !string.IsNullOrWhiteSpace(xeroClientId)   && !string.IsNullOrWhiteSpace(xeroSecret)));

// -------------------------------------------------------------------------
// Hangfire dashboard (web app hosts the UI; worker process executes the jobs).
// The shared SQL Server storage means both processes see the same job queue.
// -------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Default'.");

builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
{
    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
    QueuePollInterval = TimeSpan.Zero,
    UseRecommendedIsolationLevel = true,
    DisableGlobalLocks = true
}));

// NOTE: AddHangfireServer is intentionally NOT called here.
// The web process exposes the dashboard but does not execute jobs.
// The worker process is the only job executor.

builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
// Dev-only Basic-Auth gate for /admin/worker-logs — must sit between
// UseAuthentication and UseAuthorization so it can promote the principal
// to satisfy the global RequireAuthenticatedUser fallback policy.
app.UseMiddleware<DueMap.Web.Security.AdminBasicAuthMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

// -------------------------------------------------------------------------
// Sample rent-roll downloads. Both files are generated server-side from the
// same fixture so the CSV and XLSX never drift. Anonymous because the file
// itself contains no PM data — it's a template.
// -------------------------------------------------------------------------
app.MapGet("/samples/duemap-rent-roll-sample.xlsx", () =>
{
    using var workbook = new ClosedXML.Excel.XLWorkbook();
    var sheet = workbook.Worksheets.Add("Rent Roll");

    var headers = DueMap.Web.Services.RentRollSample.Headers;
    for (int c = 0; c < headers.Length; c++)
    {
        sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Cell(1, c + 1).Style.Font.Bold = true;
    }

    // Same five fixture rows as wwwroot/samples/duemap-rent-roll-sample.csv.
    object?[,] rows =
    {
        { "Jane Smith",         "101A", "jane.smith@example.com",   1850m, new DateTime(2025,1,15),  new DateTime(2026,1,14),  "CA" },
        { "Michael Chen",       "101B", "mchen@example.com",        1950m, new DateTime(2025,3,1),   null!,                    "CA" },
        { "Aisha Patel",        "202",  "aisha.patel@example.com",  2200m, new DateTime(2024,6,1),   new DateTime(2026,5,31),  "CA" },
        { "Rodriguez, Carlos",  "PH3",  "carlos.r@example.com",     3400m, new DateTime(2024,9,1),   new DateTime(2025,8,31),  "CA" },
        { "Emily Tanaka",       "310",  "emily.t@example.com",      2050m, new DateTime(2023,11,1),  null!,                    "CA" },
    };
    for (int r = 0; r < rows.GetLength(0); r++)
    {
        for (int c = 0; c < rows.GetLength(1); c++)
        {
            var v = rows[r, c];
            if (v is null) continue;
            var cell = sheet.Cell(r + 2, c + 1);
            switch (v)
            {
                case DateTime dt:
                    cell.Value = dt;
                    cell.Style.DateFormat.Format = "M/d/yyyy";
                    break;
                case decimal d:
                    cell.Value = d;
                    cell.Style.NumberFormat.Format = "$#,##0.00";
                    break;
                default:
                    cell.Value = v.ToString();
                    break;
            }
        }
    }
    sheet.Columns().AdjustToContents();

    var ms = new MemoryStream();
    workbook.SaveAs(ms);
    ms.Position = 0;
    return Results.File(
        ms.ToArray(),
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: "duemap-rent-roll-sample.xlsx");
}).AllowAnonymous();

// -------------------------------------------------------------------------
// P0-2: dry-run simulation endpoint. Returns the plan a PM's day WOULD
// produce without writing or dispatching anything. Gated by the
// ops.dry_run feature flag (503 when off) and by AdminBasicAuthMiddleware.
// -------------------------------------------------------------------------
app.MapGet("/admin/dry-run/{pmId:int}", async (
    int pmId,
    string? date,
    DueMap.Common.FeatureFlags.IFeatureFlags flags,
    DueMap.Billing.IPmDailyOrchestrator orchestrator,
    CancellationToken ct) =>
{
    if (!await flags.IsEnabledAsync("ops.dry_run", ct: ct))
    {
        return Results.Json(new { error = "Feature 'ops.dry_run' is disabled. Toggle it in ops.feature_flags." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var businessDate = string.IsNullOrWhiteSpace(date)
        ? DateOnly.FromDateTime(DateTime.UtcNow)
        : (DateOnly.TryParse(date, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow));

    var outcome = await orchestrator.ProcessAsync(
        pmId, businessDate, DueMap.Billing.Domain.ExecutionMode.DryRun, ct);

    return Results.Ok(new
    {
        propertyManagerId = pmId,
        businessDate = businessDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        outcome.LeasesPlanned,
        outcome.ActionsExecuted,
        outcome.ActionsSkipped,
        outcome.ActionsFailed,
        preview = outcome.DryRunPreview
    });
}).AllowAnonymous();   // AdminBasicAuthMiddleware does the actual auth gate.

// -------------------------------------------------------------------------
// Tenant portal: invoice PDF download. Gated by the dm_tenant session cookie.
// Pulls the authoritative PDF straight from the connected accounting system
// (QBO or Xero) — same path used by the late-fee notice attachment.
//
// 404 is returned for *any* failure mode (no session, wrong tenant, missing
// invoice, provider 404) to avoid leaking which case applied.
// -------------------------------------------------------------------------
app.MapGet("/t/invoices/{invoiceId:int}/pdf", async (
    int invoiceId,
    HttpContext http,
    DueMap.Web.Services.TenantContext tenantCtx,
    DueMap.Tenancy.IRentInvoiceRepository invoiceRepo,
    DueMap.Integrations.Accounting.IAccountingConnectionService connections,
    IEnumerable<DueMap.Integrations.Accounting.Sync.IAccountingDataClient> clients,
    CancellationToken ct) =>
{
    var session = await tenantCtx.GetAsync(ct);
    if (session is null) return Results.NotFound();

    var invoice = await invoiceRepo.GetByIdAsync(invoiceId, ct);
    if (invoice is null || invoice.CustomerId != session.CustomerId)
    {
        return Results.NotFound();   // tenant scoping — never leak cross-tenant existence
    }

    var conn = await connections.GetActiveAsync(invoice.PropertyManagerId, ct);
    if (conn is null) return Results.NotFound();

    var token = await connections.GetAccessTokenAsync(invoice.PropertyManagerId, ct);
    if (token is null) return Results.NotFound();

    var client = clients.FirstOrDefault(c => c.Provider == conn.Provider);
    if (client is null) return Results.NotFound();

    byte[]? bytes;
    try
    {
        bytes = await client.GetInvoicePdfAsync(token, conn.RealmId, invoice.ExternalId, ct);
    }
    catch (Exception)
    {
        // Transient provider failure — present as 404 so the tenant just
        // retries; the underlying error is in Serilog.
        return Results.NotFound();
    }
    if (bytes is null || bytes.Length == 0) return Results.NotFound();

    var filename = !string.IsNullOrWhiteSpace(invoice.ExternalDocNumber)
        ? $"Invoice-{invoice.ExternalDocNumber}.pdf"
        : $"Invoice-{invoice.ExternalId}.pdf";
    return Results.File(bytes, "application/pdf", filename);
}).AllowAnonymous();

// -------------------------------------------------------------------------
// Minimal API: webhooks from integration partners.
// These are external entry points; CSRF tokens don't apply, but each handler
// is responsible for verifying its provider's signature.
// -------------------------------------------------------------------------
var webhooks = app.MapGroup("/webhooks").DisableAntiforgery().AllowAnonymous();
webhooks.MapPost("/quickbooks", () => Results.Ok())   // TODO: verify Intuit signature, dispatch to Integrations
        .WithName("QuickBooksWebhook");
webhooks.MapPost("/xero",       () => Results.Ok())   // TODO: verify Xero signature
        .WithName("XeroWebhook");
webhooks.MapPost("/stripe",     () => Results.Ok())   // TODO: verify Stripe-Signature header
        .WithName("StripeWebhook");
webhooks.MapPost("/sendgrid",   () => Results.Ok())   // TODO: delivery / bounce events
        .WithName("SendGridWebhook");
webhooks.MapPost("/twilio",     () => Results.Ok())   // TODO: SMS delivery callbacks
        .WithName("TwilioWebhook");

// P2-2: Twilio inbound SMS — STOP/START opt-out handling. Twilio POSTs an
// x-www-form-urlencoded body with From + Body when a tenant replies. We mirror
// the carrier-level STOP into our suppression list so we never even attempt a
// send. The request is signature-verified (X-Twilio-Signature) before we trust
// it — critical for START, which un-suppresses a number.
// Standard carrier opt-out / opt-in keywords (created once, not per request).
var smsStopWords  = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "STOP", "STOPALL", "UNSUBSCRIBE", "CANCEL", "END", "QUIT" };
var smsStartWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "START", "UNSTOP", "YES" };

webhooks.MapPost("/twilio/inbound", async (
    HttpRequest req,
    DueMap.Integrations.Notices.ISmsSuppressionStore suppressions,
    Microsoft.Extensions.Options.IOptions<DueMap.Integrations.IntegrationsOptions> intOpts,
    CancellationToken ct) =>
{
    var form = await req.ReadFormAsync(ct);

    // ---- Verify the request really came from Twilio ----
    // Validate when an auth token is configured (prod). In dev with no token we
    // can't validate and there's no live Twilio anyway, so we skip + log.
    var authToken = intOpts.Value.Twilio.AuthToken;
    if (!string.IsNullOrWhiteSpace(authToken))
    {
        var signature = req.Headers["X-Twilio-Signature"].ToString();
        // The URL Twilio signed is the public callback URL. Behind a TLS-
        // terminating proxy, ensure forwarded headers set the https scheme so
        // this matches what Twilio used.
        var url = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(req);
        var parameters = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var validator = new Twilio.Security.RequestValidator(authToken);
        if (string.IsNullOrEmpty(signature) || !validator.Validate(url, parameters, signature))
        {
            Log.Warning("Rejected Twilio inbound SMS: invalid or missing X-Twilio-Signature.");
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    var from = form["From"].ToString();
    var body = form["Body"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(from)) return Results.Ok();

    if (smsStopWords.Contains(body))
    {
        await suppressions.SuppressAsync(from, $"Inbound SMS: {body.ToUpperInvariant()}", propertyManagerId: null, ct);
    }
    else if (smsStartWords.Contains(body))
    {
        await suppressions.UnsuppressAsync(from, ct);
    }

    // Empty 200 — Twilio treats this as "no auto-reply from the app."
    return Results.Ok();
}).WithName("TwilioInboundSms");

// -------------------------------------------------------------------------
// OAuth endpoints for accounting onboarding (QuickBooks + Xero).
//   GET  /oauth/{provider}/connect/{pmId}    → 302 to the provider authorize URL
//   GET  /oauth/{provider}/callback?code=…   → finalize, persist encrypted tokens
// The callbacks disable antiforgery because they're external redirects.
// -------------------------------------------------------------------------
// /oauth/{provider}/connect/{pmId} requires the caller to be authenticated
// (otherwise anyone could initiate a connection against any PM id). The
// /callback endpoint must allow anonymous because the auth cookie may be
// stripped during the provider's redirect chain.
var oauthGroup = app.MapGroup("/oauth").DisableAntiforgery();

oauthGroup.MapGet("/{provider}/connect/{pmId:int}", async (
    string provider, int pmId, HttpRequest req, IAccountingConnectionService svc, CancellationToken ct) =>
{
    if (!TryParseProvider(provider, out var parsed))
    {
        return Results.BadRequest($"Unknown provider '{provider}'.");
    }

    var redirectUri = $"{req.Scheme}://{req.Host}/oauth/{provider}/callback";
    var result = await svc.InitiateAsync(pmId, parsed, redirectUri, ct);
    return Results.Redirect(result.AuthorizationUrl);
});

oauthGroup.MapGet("/{provider}/callback", async (
    string provider, HttpRequest req, IAccountingConnectionService svc,
    Hangfire.IBackgroundJobClient jobs, CancellationToken ct) =>
{
    if (!TryParseProvider(provider, out var parsed))
    {
        return Results.BadRequest($"Unknown provider '{provider}'.");
    }

    var code    = req.Query["code"].ToString();
    var state   = req.Query["state"].ToString();
    var realmId = req.Query["realmId"].ToString();   // QuickBooks supplies this; empty for Xero
    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
    {
        return Results.BadRequest("Missing code or state.");
    }

    var conn = await svc.CompleteAsync(new ConnectionCallbackInput(
        Provider: parsed,
        Code: code,
        State: state,
        RealmId: string.IsNullOrEmpty(realmId) ? null : realmId), ct);

    // Guarantee a first sync server-side the moment the connection is
    // persisted — independent of any UI page. This is what keeps a freshly
    // connected account from sitting at "Last sync: never": the daily sweep
    // skips PMs with no active leases (chicken-and-egg — leases come from the
    // sync), so without this nothing would ever pull the first batch unless
    // the user happened to complete the syncing wizard. The Worker runs it;
    // SyncForAsync is idempotent so overlap with the wizard's own sync is safe.
    jobs.Enqueue<DueMap.Integrations.Accounting.Jobs.IInitialSyncJob>(
        j => j.RunAsync(conn.PropertyManagerId, CancellationToken.None));

    // Land on the onboarding sync page so the user sees real progress for
    // their first customer/invoice pull instead of staring at a stale dashboard.
    // The page kicks the sync, ticks each step, and then forwards to "/".
    return Results.Redirect("/onboarding/syncing");
}).AllowAnonymous();

// -------------------------------------------------------------------------
// "Sign in with Intuit" via QBO OAuth (replaces the OIDC challenge path).
// See IntuitSignInOrchestrator class summary for the design rationale.
//   GET /oauth/intuit/signin            → 302 to Intuit's authorize page
//   GET /oauth/intuit/signin-callback   → identify-or-provision + sign-in
// Both anonymous — they're the entry points to authentication, not
// authenticated endpoints.
// -------------------------------------------------------------------------
app.MapGet("/oauth/intuit/signin", (
    HttpRequest req,
    DueMap.Web.Services.IIntuitSignInOrchestrator orchestrator) =>
{
    var callbackUrl = $"{req.Scheme}://{req.Host}/oauth/intuit/signin-callback";
    var url = orchestrator.BuildSignInUrl(callbackUrl);
    return Results.Redirect(url);
}).AllowAnonymous();

app.MapGet("/oauth/intuit/signin-callback", async (
    HttpRequest req,
    DueMap.Web.Services.IIntuitSignInOrchestrator orchestrator,
    Microsoft.AspNetCore.Identity.SignInManager<DueMap.Identity.Domain.ApplicationUser> signIn,
    CancellationToken ct) =>
{
    var code    = req.Query["code"].ToString();
    var state   = req.Query["state"].ToString();
    var realmId = req.Query["realmId"].ToString();
    var callbackUrl = $"{req.Scheme}://{req.Host}/oauth/intuit/signin-callback";

    var result = await orchestrator.HandleCallbackAsync(code, state, realmId, callbackUrl, ct);
    if (result.User is null)
    {
        // Bounce back to Login with the rose error banner.
        var msg = Uri.EscapeDataString(result.Error ?? "Sign in with Intuit failed.");
        return Results.Redirect($"/Account/Login?sso_error={msg}");
    }

    // Sign in via Identity's cookie scheme. isPersistent: false matches the
    // email/password path — a new browser session re-prompts for sign-in.
    await signIn.SignInAsync(result.User, isPersistent: false);

    // New users go to onboarding (which now knows to skip step 1 because
    // step_connect_done_at was just stamped). Returning users go home.
    return Results.Redirect(result.IsNewUser ? "/onboarding/close-settings" : "/");
}).AllowAnonymous();

static bool TryParseProvider(string raw, out AccountingProvider provider)
{
    switch (raw.ToLowerInvariant())
    {
        case "quickbooks": provider = AccountingProvider.QuickBooks; return true;
        case "xero":       provider = AccountingProvider.Xero;       return true;
        default:           provider = default; return false;
    }
}

// -------------------------------------------------------------------------
// Hangfire dashboard. Gated by HangfireDashboardAuthorizationFilter — any
// authenticated user gets in for v1; promote to a role check once roles land.
// -------------------------------------------------------------------------
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new DueMap.Web.Security.HangfireDashboardAuthorizationFilter() }
});

app.MapHealthChecks("/health").AllowAnonymous();
app.MapRazorPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// -------------------------------------------------------------------------
// Dev-only: seed a demo PM + admin user + customers + leases + invoices so a
// fresh install has something clickable. Idempotent.
// -------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    // Apply pending schema migrations BEFORE the seeder runs — the seeder may
    // depend on columns added by the latest scripts.
    var bootstrap = scope.ServiceProvider.GetRequiredService<DueMap.Web.Services.DevSchemaBootstrap>();
    await bootstrap.RunAsync(CancellationToken.None);

    var seeder = scope.ServiceProvider.GetRequiredService<DueMap.Web.Services.DemoSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

try
{
    Log.Information("DueMap.Web starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DueMap.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
