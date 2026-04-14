using System;
using System.IO;

namespace VoiceInput.Core.Stt;

/// <summary>
/// Creates WAV file bytes from raw PCM data for STT API submissions.
/// </summary>
public static class WavHelper
{
    /// <summary>
    /// Wraps raw PCM bytes in a WAV header.
    /// </summary>
    public static byte[] CreateWavBytes(byte[] pcmData, int sampleRate = 16000, int bitsPerSample = 16, int channels = 1)
    {
        ArgumentNullException.ThrowIfNull(pcmData);

        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length); // ChunkSize
        writer.Write("WAVE"u8);

        // fmt subchunk
        writer.Write("fmt "u8);
        writer.Write(16);               // Subchunk1Size (PCM)
        writer.Write((short)1);         // AudioFormat (PCM = 1)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data subchunk
        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return stream.ToArray();
    }
}
