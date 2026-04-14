using System;
using System.Threading.Tasks;

namespace VoiceInput.Core.Audio;

/// <summary>
/// Orchestrates a voice recording session: capture → accumulate → resample.
/// </summary>
public interface IRecordingSession
{
    /// <summary>
    /// Starts recording from the specified audio device.
    /// </summary>
    /// <param name="deviceId">WASAPI device ID, or null for default device.</param>
    Task StartAsync(string? deviceId = null);

    /// <summary>
    /// Stops recording and returns resampled audio in Whisper format (16kHz mono int16).
    /// Returns empty array if recording was too short (&lt;0.5s).
    /// </summary>
    Task<byte[]> StopAsync();

    /// <summary>Current state of the recording session.</summary>
    RecordingState State { get; }

    /// <summary>Duration of the current or last recording.</summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Fires on each audio chunk with normalized amplitude [0.0, 1.0] for UI animation.
    /// </summary>
    event EventHandler<float>? AmplitudeChanged;

    /// <summary>
    /// Fires when VAD detects silence timeout and auto-stops the recording.
    /// </summary>
    event EventHandler? AutoStopped;
}
