using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Common.Modularity;

public static class ModuleRegistration
{
    /// <summary>
    /// Register the supplied modules in order. Both the web app and the
    /// worker call this with the same module list so behavior is consistent
    /// across processes.
    /// </summary>
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        params IModule[] modules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.Register(services, configuration);
        }

        return services;
    }
}
