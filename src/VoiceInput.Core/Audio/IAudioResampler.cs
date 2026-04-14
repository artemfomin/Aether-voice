namespace VoiceInput.Core.Audio;

/// <summary>
/// Resamples audio data between different PCM formats.
/// </summary>
public interface IAudioResampler
{
    /// <summary>
    /// Resamples raw PCM audio bytes from source format to target format.
    /// </summary>
    /// <param name="input">Raw PCM audio bytes in source format.</param>
    /// <param name="sourceFormat">Format of the input data.</param>
    /// <param name="targetFormat">Desired output format.</param>
    /// <returns>Raw PCM audio bytes in target format.</returns>
    byte[] Resample(byte[] input, WaveFormat sourceFormat, WaveFormat targetFormat);
}
