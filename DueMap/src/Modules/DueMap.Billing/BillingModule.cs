using DueMap.Billing.Persistence;
using DueMap.Billing.Services;
using DueMap.Common.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Billing;

/// <summary>
/// Registration entry point for the Billing module. Billing is the orchestrator
/// tier: it consumes the public contracts of Rules, Tenancy, and Notices, and
/// exposes the decision/scheduling surface (effective policy + assessment plan).
/// </summary>
public sealed class BillingModule : IModule
{
    public string Name => "Billing";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BillingOptions>()
            .Bind(configuration.GetSection(BillingOptions.SectionName));

        var connectionString = configuration.GetSection(BillingOptions.SectionName)["ConnectionString"]
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Billing module: no connection string. Set ConnectionStrings:Default or Billing:ConnectionString.");

        services.AddDbContext<BillingDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__billing_migrations", "billing")));

        services.AddScoped<IRentScheduleService, MonthlyRentScheduleService>();
        services.AddScoped<IEffectivePolicyService, EffectivePolicyService>();
        services.AddScoped<IAssessmentPlanner, AssessmentPlanner>();
        services.AddScoped<IAssessmentRunRepository, AssessmentRunRepository>();
        services.AddScoped<ILateFeeAssessmentRepository, LateFeeAssessmentRepository>();
        services.AddScoped<IPmProcessingRunRepository, PmProcessingRunRepository>();
        // IPmAccountingSync is now implemented in the Integrations module
        // (DueMap.Integrations.Accounting.Sync.PmAccountingSyncService).
        services.AddScoped<IActionExecutor, ActionExecutor>();
        services.AddScoped<IPmDailyOrchestrator, PmDailyOrchestrator>();

        // Reports — daily close email per PM (Hangfire-triggered in Worker).
        services.AddScoped<Reports.IDailyCloseReportService, Reports.DailyCloseReportService>();
    }
}
