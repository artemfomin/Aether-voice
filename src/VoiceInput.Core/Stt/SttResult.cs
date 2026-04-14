namespace VoiceInput.Core.Stt;

/// <summary>
/// Represents the result of a speech-to-text transcription.
/// </summary>
/// <param name="Text">The transcribed text.</param>
/// <param name="Language">The detected or specified language code (e.g. "ru", "en").</param>
/// <param name="DurationMs">Duration of the audio that was transcribed, in milliseconds.</param>
/// <param name="Confidence">Optional confidence score in range [0.0, 1.0].</param>
public sealed record SttResult(
    string Text,
    string? Language,
    int DurationMs,
    double? Confidence);
