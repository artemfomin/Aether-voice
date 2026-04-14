namespace VoiceInput.Core;

/// <summary>
/// Defines the lifecycle contract for the voice-input application.
/// Implementations coordinate startup and graceful shutdown of all services.
/// </summary>
public interface IApplicationLifecycle
{
    /// <summary>Starts all application services.</summary>
    /// <param name="ct">Token to cancel the startup sequence.</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Stops all application services and releases resources.</summary>
    /// <param name="ct">Token to cancel the shutdown sequence.</param>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Gets a value indicating whether the application is currently running.</summary>
    bool IsRunning { get; }
}
