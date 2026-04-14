namespace VoiceInput.Core.Audio;

/// <summary>
/// Abstraction over a microphone / audio-input capture device.
/// Implementations are responsible for thread-safe event delivery.
/// </summary>
public interface IAudioCapture
{
    /// <summary>
    /// Raised whenever the device delivers a new block of PCM audio samples.
    /// The event may be raised on a background thread; subscribers must
    /// not perform long-running or UI work directly on the calling thread.
    /// </summary>
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    /// <summary>Gets a value indicating whether audio capture is currently active.</summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Starts recording from the specified device.
    /// If capture is already active the call is a no-op.
    /// </summary>
    /// <param name="deviceId">
    /// WASAPI endpoint ID of the desired capture device, or <c>null</c> / empty
    /// string to use the system default recording device.
    /// </param>
    void StartCapture(string? deviceId);

    /// <summary>
    /// Stops the active capture session.
    /// Has no effect when <see cref="IsCapturing"/> is <c>false</c>.
    /// </summary>
    void StopCapture();
}
