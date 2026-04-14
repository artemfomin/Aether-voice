using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt;

/// <summary>
/// Abstraction over a speech-to-text backend.
/// Concrete implementations live in Wave 3 (T13–T16).
/// </summary>
public interface ISttProvider
{
    /// <summary>Human-readable provider name (e.g. "OpenAI Whisper").</summary>
    string Name { get; }

    /// <summary>Enum identifier used for registry lookup and configuration.</summary>
    SttProviderType ProviderType { get; }

    /// <summary>
    /// Indicates whether the provider is currently usable
    /// (e.g. API key configured, local service reachable).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Transcribes raw PCM audio (16 kHz, mono, int16) to text.
    /// </summary>
    /// <param name="audio">Raw audio bytes in 16 kHz mono int16 PCM format.</param>
    /// <param name="language">BCP-47 language hint (e.g. "ru", "en").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Transcription result.</returns>
    /// <exception cref="SttException">Thrown on any provider-level failure.</exception>
    Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default);
}
