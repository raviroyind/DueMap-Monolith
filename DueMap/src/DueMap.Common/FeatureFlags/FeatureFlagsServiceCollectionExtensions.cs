using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DueMap.Common.FeatureFlags;

/// <summary>
/// Wires <see cref="IFeatureFlags"/> into both DueMap.Web and DueMap.Worker.
/// Singleton: the cache + connection string are immutable for the process
/// lifetime; concurrent reads on the SQL connection are safe (each call
/// creates its own <c>SqlConnection</c>).
/// </summary>
public static class FeatureFlagsServiceCollectionExtensions
{
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // MemoryCache is reused if the host already added one (Web does via
        // AddMemoryCache); harmless to call twice.
        services.AddMemoryCache();

        services.AddSingleton<IFeatureFlags>(sp => new SqlFeatureFlags(
            connectionString,
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<ILogger<SqlFeatureFlags>>()));

        return services;
    }
}
