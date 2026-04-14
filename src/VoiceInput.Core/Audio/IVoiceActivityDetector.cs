namespace VoiceInput.Core.Audio;

/// <summary>
/// Detects voice activity in PCM audio samples.
/// </summary>
public interface IVoiceActivityDetector
{
    /// <summary>
    /// Processes a chunk of 16-bit PCM samples and returns the detection result.
    /// </summary>
    VadResult ProcessSamples(short[] samples);

    /// <summary>
    /// Resets all internal state (silence counters, debounce state).
    /// </summary>
    void Reset();
}
