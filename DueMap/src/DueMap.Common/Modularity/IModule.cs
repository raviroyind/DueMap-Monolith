using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Common.Modularity;

/// <summary>
/// Contract for a business module in the modular monolith.
/// Each module owns its entities, services, and configuration. Modules
/// communicate with each other through public interfaces exposed from
/// their root namespace â€” never by sharing DbContexts or entity types.
///
/// Both the web host and the worker host register the same set of modules
/// at startup, which means a module's services are available regardless
/// of whether the call comes from a Blazor component or a Hangfire job.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Stable identifier for the module. Used in logs and as a configuration
    /// section prefix (e.g. "Rules" maps to the "Rules" section of appsettings).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Register the module's services, options, and EF Core configuration
    /// with the application's DI container.
    /// </summary>
    void Register(IServiceCollection services, IConfiguration configuration);
}
