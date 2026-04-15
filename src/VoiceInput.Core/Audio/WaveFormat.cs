namespace VoiceInput.Core.Audio;

/// <summary>
/// Describes the format of a PCM audio stream.
/// This is VoiceInput.Core's own format descriptor — it has zero dependency
/// on NAudio or any third-party audio library, keeping Core free of platform bindings.
/// </summary>
/// <param name="SampleRate">Samples per second (e.g. 44100, 48000, 16000).</param>
/// <param name="BitsPerSample">Bit depth per sample (e.g. 16, 32).</param>
/// <param name="Channels">Number of audio channels (1 = mono, 2 = stereo).</param>
public sealed record WaveFormat(int SampleRate, int BitsPerSample, int Channels, bool IsFloat = false)
{
    /// <summary>
    /// The 16 kHz, 16-bit, mono PCM format expected by Whisper STT models.
    /// </summary>
    public static WaveFormat Whisper { get; } = new(16_000, 16, 1);
}
