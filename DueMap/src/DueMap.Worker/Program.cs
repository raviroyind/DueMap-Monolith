using Hangfire;
using Hangfire.SqlServer;
using DueMap.Billing;
using DueMap.Common.Modularity;
using DueMap.Identity;
using DueMap.Integrations;
using DueMap.Notices;
using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Worker.Jobs;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// -------------------------------------------------------------------------
// Module registration. Identical to DueMap.Web minus Identity
// (no auth needed in the worker â€” it operates on behalf of the system).
// -------------------------------------------------------------------------
builder.Services.AddModules(builder.Configuration,
    new IdentityModule(),
    new TenancyModule(),
    new RulesModule(),
    new NoticesModule(),
    new BillingModule(),
    new IntegrationsModule());

// Worker-host-local job registrations.
builder.Services.AddScoped<IDailyAssessmentSweepJob, DailyAssessmentSweepJob>();
builder.Services.AddScoped<IDailyCloseReportJob, DailyCloseReportJob>();

// Data Protection — must use the same keyring as the Web host so the Worker
// can decrypt OAuth tokens written during onboarding.
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".dataprotection-keys");
builder.Services.AddDataProtection()
    .SetApplicationName("DueMap")
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

// -------------------------------------------------------------------------
// Hangfire. Shared SQL Server storage with the web process.
// AddHangfireServer here (and NOT in the web process) is what makes this
// the job-executing process â€” the web process only renders the dashboard.
// -------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Default'.");

builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
{
    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
    QueuePollInterval = TimeSpan.FromSeconds(15),
    UseRecommendedIsolationLevel = true,
    DisableGlobalLocks = true
}));

builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"DueMap.Worker.{Environment.MachineName}";
    options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 16);
    options.Queues = HangfireQueues;
});

var host = builder.Build();

// -------------------------------------------------------------------------
// Recurring job registration. Once modules expose their own job classes,
// register them here. The recurring registration is idempotent â€” calling
// AddOrUpdate with the same id and cron expression just refreshes config.
// -------------------------------------------------------------------------
using (var scope = host.Services.CreateScope())
{
    var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // Sweep every 15 minutes. The job itself filters PMs already processed
    // for today, so the cron just needs to be frequent enough that no PM's
    // local midnight goes more than ~15 min before being picked up.
    recurring.AddOrUpdate<IDailyAssessmentSweepJob>(
        recurringJobId: "billing.daily-assessment-sweep",
        methodCall:     job => job.RunAsync(CancellationToken.None),
        cronExpression: "*/15 * * * *");

    // The daily close report no longer has its own cron — it's chained to
    // each per-PM orchestrator run via Hangfire ContinueJobWith in the sweep.
    // Drop the stale recurring registration if a previous deploy created it.
    recurring.RemoveIfExists("reports.daily-close");
}

try
{
    Log.Information("DueMap.Worker starting up");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DueMap.Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
    private static readonly string[] HangfireQueues = ["critical", "default", "low"];
}
