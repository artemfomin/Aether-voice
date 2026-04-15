using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using VoiceInput.Core.Audio;
using VoiceInput.Core.Config;
using VoiceInput.Core.History;
using VoiceInput.Core.Injection;
using VoiceInput.Core.Llm;
using VoiceInput.Core.Stt;
using VoiceInput.Core.Stt.Providers;

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
        // Config
        services.AddSingleton<IConfigStore, JsonConfigStore>();
        services.AddSingleton(sp => sp.GetRequiredService<IConfigStore>().Load());

        // Audio (interfaces only — implementations are in VoiceInput.App)
        services.AddSingleton<IVoiceActivityDetector>(sp =>
        {
            var cfg = sp.GetRequiredService<AppConfig>();
            var timeout = cfg.SilenceTimeoutMs > 0 ? cfg.SilenceTimeoutMs : 3000;
            return new AmplitudeVad(silenceTimeoutMs: timeout);
        });

        // History
        services.AddSingleton<IHistoryStore>(sp =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbDir = Path.Combine(appData, "VoiceInput");
            Directory.CreateDirectory(dbDir);
            return new SqliteHistoryStore(Path.Combine(dbDir, "history.db"));
        });

        // STT provider registry
        services.AddSingleton<SttProviderRegistry>();

        // STT providers
        services.AddSingleton<OllamaSttProvider>();
        services.AddSingleton<LmStudioSttProvider>();

        return services;
    }
}
