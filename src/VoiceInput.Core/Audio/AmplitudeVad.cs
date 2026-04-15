using System;

namespace VoiceInput.Core.Audio;

/// <summary>
/// Voice activity detector based on RMS amplitude threshold with debounce.
/// </summary>
public sealed class AmplitudeVad : IVoiceActivityDetector
{
    private readonly float _speechThreshold;
    private readonly int _silenceTimeoutMs;
    private int _sampleRate;
    private readonly int _debounceFrames;

    private int _consecutiveSilentFrames;
    private int _silenceDurationMs;
    private bool _isSpeechActive;

    /// <summary>
    /// Creates a new amplitude-based VAD.
    /// </summary>
    /// <param name="speechThreshold">Normalized RMS threshold for speech detection (default 0.005).</param>
    /// <param name="silenceTimeoutMs">Silence duration in ms before auto-stop (default 1500).</param>
    /// <param name="sampleRate">Expected sample rate for duration calculation (default 16000).</param>
    /// <param name="debounceFrames">Consecutive silent frames required before declaring silence (default 3).</param>
    public AmplitudeVad(
        float speechThreshold = 0.005f,
        int silenceTimeoutMs = 1500,
        int sampleRate = 16000,
        int debounceFrames = 3)
    {
        _speechThreshold = speechThreshold;
        _silenceTimeoutMs = silenceTimeoutMs;
        _sampleRate = sampleRate;
        _debounceFrames = debounceFrames;
    }

    /// <summary>
    /// The configured silence timeout in milliseconds.
    /// </summary>
    public int SilenceTimeoutMs => _silenceTimeoutMs;

    /// <summary>
    /// Update the sample rate once the actual capture format is known.
    /// </summary>
    public void SetSampleRate(int sampleRate) => _sampleRate = sampleRate;

    public VadResult ProcessSamples(short[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Length == 0)
        {
            return new VadResult(IsSpeech: _isSpeechActive, SilenceDurationMs: _silenceDurationMs, CurrentAmplitude: 0f);
        }

        float rms = CalculateRms(samples);
        float normalizedAmplitude = Math.Clamp(rms / short.MaxValue, 0f, 1f);

        int chunkDurationMs = (int)((long)samples.Length * 1000 / _sampleRate);

        if (normalizedAmplitude > _speechThreshold)
        {
            // Speech detected — reset silence tracking
            _consecutiveSilentFrames = 0;
            _silenceDurationMs = 0;
            _isSpeechActive = true;
        }
        else
        {
            // Silence detected
            _consecutiveSilentFrames++;

            if (_consecutiveSilentFrames >= _debounceFrames)
            {
                // Past debounce — accumulate silence duration
                _silenceDurationMs += chunkDurationMs;
                _isSpeechActive = false;
            }
            // If within debounce window, keep previous speech state
        }

        return new VadResult(
            IsSpeech: _isSpeechActive,
            SilenceDurationMs: _silenceDurationMs,
            CurrentAmplitude: normalizedAmplitude);
    }

    public void Reset()
    {
        _consecutiveSilentFrames = 0;
        _silenceDurationMs = 0;
        _isSpeechActive = false;
    }

    private static float CalculateRms(short[] samples)
    {
        double sumSquares = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double sample = samples[i];
            sumSquares += sample * sample;
        }

        return (float)Math.Sqrt(sumSquares / samples.Length);
    }
}
