namespace VoiceInput.Core.Audio;

/// <summary>
/// Result of voice activity detection for a single audio chunk.
/// </summary>
/// <param name="IsSpeech">Whether the chunk contains speech above the threshold.</param>
/// <param name="SilenceDurationMs">Cumulative silence duration in milliseconds since last speech.</param>
/// <param name="CurrentAmplitude">Normalized RMS amplitude in [0.0, 1.0] for audio-reactive UI.</param>
public sealed record VadResult(bool IsSpeech, int SilenceDurationMs, float CurrentAmplitude);
