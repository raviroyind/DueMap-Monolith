using DueMap.Common.Modularity;
using DueMap.Rules.Persistence;
using DueMap.Rules.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Rules;

/// <summary>
/// Registration entry point for the Rules module. Both the web host
/// and the worker host call <see cref="Register"/> with the same configuration.
/// </summary>
public sealed class RulesModule : IModule
{
    public string Name => "Rules";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RulesOptions>()
            .Bind(configuration.GetSection(RulesOptions.SectionName));

        var connectionString = configuration.GetSection(RulesOptions.SectionName)["ConnectionString"]
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Rules module: no connection string. Set ConnectionStrings:Default or Rules:ConnectionString.");

        services.AddDbContext<RulesDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__rules_migrations", "rules")));

        services.AddMemoryCache();
        services.AddScoped<IRulesService, RulesService>();
        services.AddScoped<IStateReader, StateReader>();
    }
}
