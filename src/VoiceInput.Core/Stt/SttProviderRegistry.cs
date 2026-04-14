using System.Collections.Concurrent;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt;

/// <summary>
/// Thread-safe registry that maps <see cref="SttProviderType"/> to <see cref="ISttProvider"/>
/// instances. Providers are registered at startup and resolved at transcription time.
/// </summary>
public sealed class SttProviderRegistry
{
    private readonly ConcurrentDictionary<SttProviderType, ISttProvider> _providers = new();

    /// <summary>
    /// Registers a provider. If a provider for the same type already exists it is replaced.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    public void Register(ISttProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers[provider.ProviderType] = provider;
    }

    /// <summary>
    /// Resolves a provider by type.
    /// </summary>
    /// <param name="type">The provider type to look up.</param>
    /// <returns>The registered provider.</returns>
    /// <exception cref="SttException">
    /// Thrown with <see cref="SttErrorKind.Unavailable"/> when no provider is registered for
    /// <paramref name="type"/>.
    /// </exception>
    public ISttProvider Resolve(SttProviderType type)
    {
        if (_providers.TryGetValue(type, out var provider))
            return provider;

        throw new SttException(
            SttErrorKind.Unavailable,
            $"No STT provider registered for type '{type}'.");
    }

    /// <summary>
    /// Attempts to resolve a provider without throwing.
    /// </summary>
    /// <param name="type">The provider type to look up.</param>
    /// <param name="provider">
    /// When this method returns <see langword="true"/>, contains the registered provider;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if a provider was found; otherwise <see langword="false"/>.</returns>
    public bool TryResolve(SttProviderType type, out ISttProvider? provider)
        => _providers.TryGetValue(type, out provider);

    /// <summary>
    /// Returns a snapshot of all currently registered providers.
    /// </summary>
    public IReadOnlyList<ISttProvider> GetAll()
        => _providers.Values.ToList().AsReadOnly();
}
