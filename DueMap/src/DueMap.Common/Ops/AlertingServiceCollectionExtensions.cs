using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Common.Ops;

/// <summary>
/// Registers the alerting infrastructure. Web and Worker both call this so
/// both processes can raise operator alerts independently. The actual sink
/// is decided by <c>Ops:Alerts:Channel</c>:
/// <list type="bullet">
///   <item><c>email</c>   → Integrations registers <c>EmailAlertSink</c></item>
///   <item><c>webhook</c> → Integrations registers <c>WebhookAlertSink</c></item>
///   <item>anything else → <see cref="NullAlertSink"/> (logs but doesn't send)</item>
/// </list>
/// </summary>
public static class AlertingServiceCollectionExtensions
{
    public static IServiceCollection AddAlerting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AlertsOptions>()
            .Bind(configuration.GetSection(AlertsOptions.SectionName));

        // The sink is registered by the host module (see IntegrationsModule).
        // If nothing has registered an IAlertSink by the time DI resolves
        // AlertService, we fall back to the null sink so the call still
        // works (logs only — operator gets nothing on a real failure, but
        // the system stays running).
        services.AddSingleton<IAlertSink, NullAlertSink>();
        services.AddSingleton<IAlertService, AlertService>();
        return services;
    }
}
