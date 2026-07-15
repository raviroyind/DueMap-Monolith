using DueMap.Common.Modularity;
using DueMap.Tenancy.Persistence;
using DueMap.Tenancy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Tenancy;

public sealed class TenancyModule : IModule
{
    public string Name => "Tenancy";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TenancyOptions>()
            .Bind(configuration.GetSection(TenancyOptions.SectionName));

        var connectionString = configuration.GetSection(TenancyOptions.SectionName)["ConnectionString"]
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Tenancy module: no connection string. Set ConnectionStrings:Default or Tenancy:ConnectionString.");

        // Dual registration — same pattern as Integrations (task #71):
        //   * Factory is the primary path. Every service creates a short-lived
        //     context per public method to survive Blazor Server's "one
        //     scoped DbContext per circuit" footgun (rapid nav between pages
        //     races on the shared context and throws
        //     "A second operation was started on this context instance").
        //   * Scoped registration kept as a thin shim over the factory so any
        //     residual code paths (DevSchemaBootstrap, EF migrations, tests)
        //     that ask for a scoped DbContext keep working.
        services.AddDbContextFactory<TenancyDbContext>(opt =>
            opt.UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__tenancy_migrations", "tenancy")));
        services.AddScoped<TenancyDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TenancyDbContext>>().CreateDbContext());

        services.AddScoped<IPmNoticePreferencesService, PmNoticePreferencesService>();
        services.AddScoped<ILeaseNoticeSettingsService, LeaseNoticeSettingsService>();
        services.AddScoped<ILeaseReader, LeaseReader>();
        services.AddScoped<ITenantContactResolver, TenantContactResolver>();
        services.AddScoped<IPmAccountingDefaultsService, PmAccountingDefaultsService>();
        services.AddScoped<IPmCurrencyReader, PmCurrencyReader>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IRentInvoiceRepository, RentInvoiceRepository>();
        services.AddScoped<IPropertyManagerReader, PropertyManagerReader>();
        services.AddScoped<IPropertyManagerWriter, PropertyManagerWriter>();
        services.AddScoped<ILeaseLinkService, LeaseLinkService>();
        services.AddScoped<ILeaseWriter, LeaseWriter>();
        services.AddScoped<IOnboardingProgressService, OnboardingProgressService>();
        services.AddScoped<IPmDailyCloseSettingsService, PmDailyCloseSettingsService>();
        services.AddScoped<ITenantPortalAuth, TenantPortalAuth>();
        // P1-1 — auto-discovery. Scoped because it uses a DbContextFactory
        // for per-call contexts but holds no per-call state itself.
        services.AddScoped<Discovery.IDiscoveryService, Discovery.DiscoveryService>();
        // P1-4 — explicit "Go live" transition (staged fees → live + Active).
        services.AddScoped<IGoLiveService, Services.GoLiveService>();
    }
}
