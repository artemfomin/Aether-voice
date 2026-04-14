using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using VoiceInput.Core.Config;

namespace VoiceInput.Core;

/// <summary>
/// Extension methods for registering VoiceInput.Core services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core VoiceInput services into the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddVoiceInputCore(this IServiceCollection services)
    {
        services.AddSingleton<IConfigStore, JsonConfigStore>();

        // Future services will be registered here:
        // services.AddSingleton<ISttProvider, ...>();
        // services.AddSingleton<IHistoryStore, SqliteHistoryStore>();
        // etc.

        return services;
    }
}
