namespace VoiceInput.Core.Audio;

/// <summary>
/// Provides a captured audio chunk for the <see cref="IAudioCapture.AudioDataAvailable"/> event.
/// </summary>
public sealed class AudioDataEventArgs : EventArgs
{
    /// <summary>Gets the raw PCM audio buffer as delivered by the capture device.</summary>
    public byte[] Buffer { get; }

    /// <summary>
    /// Gets the number of valid bytes in <see cref="Buffer"/>.
    /// The buffer may be larger than <see cref="BytesRecorded"/> due to internal pooling.
    /// </summary>
    public int BytesRecorded { get; }

    /// <summary>Gets the wave format that describes the audio data in <see cref="Buffer"/>.</summary>
    public WaveFormat Format { get; }

    /// <summary>Initialises a new instance of <see cref="AudioDataEventArgs"/>.</summary>
    /// <param name="buffer">Raw PCM buffer (must not be null).</param>
    /// <param name="bytesRecorded">Number of valid bytes within <paramref name="buffer"/>.</param>
    /// <param name="format">Audio format descriptor for the buffer contents.</param>
    public AudioDataEventArgs(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        Buffer = buffer;
        BytesRecorded = bytesRecorded;
        Format = format;
    }
}
