namespace VoiceInput.Core.Audio;

/// <summary>
/// State of a recording session.
/// </summary>
public enum RecordingState
{
    /// <summary>No recording in progress.</summary>
    Idle,

    /// <summary>Actively capturing audio.</summary>
    Recording,

    /// <summary>Recording stopped, processing audio (resampling).</summary>
    Processing
}
