using DueMap.Common.Modularity;
using DueMap.Notices.Persistence;
using DueMap.Notices.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Notices;

/// <summary>
/// Registration entry point for the Notices module. Both the web host
/// and the worker host call <see cref="Register"/> with the same configuration.
/// </summary>
public sealed class NoticesModule : IModule
{
    public string Name => "Notices";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NoticesOptions>()
            .Bind(configuration.GetSection(NoticesOptions.SectionName));

        var connectionString = configuration.GetSection(NoticesOptions.SectionName)["ConnectionString"]
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Notices module: no connection string. Set ConnectionStrings:Default or Notices:ConnectionString.");

        services.AddDbContext<NoticesDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__notices_migrations", "notices")));

        services.AddMemoryCache();
        services.AddScoped<INoticeTemplateService, NoticeTemplateService>();
        services.AddScoped<INoticeRenderer, ScribanNoticeRenderer>();
        services.AddScoped<INoticeDeliveryRepository, NoticeDeliveryRepository>();
        services.AddScoped<IPmTemplateOverrideService, PmTemplateOverrideService>();
        services.AddScoped<INoticeTypeReader, NoticeTypeReader>();
    }
}
