using System;
using System.IO;
using System.Runtime.Versioning;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using CoreWaveFormat = VoiceInput.Core.Audio.WaveFormat;

namespace VoiceInput.App.Audio;

/// <summary>
/// Resamples audio using NAudio's WDL resampler pipeline.
/// Converts any supported input format to the target PCM format (typically 16kHz mono int16 for Whisper).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NAudioResampler : VoiceInput.Core.Audio.IAudioResampler
{
    public byte[] Resample(byte[] input, CoreWaveFormat sourceFormat, CoreWaveFormat targetFormat)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sourceFormat);
        ArgumentNullException.ThrowIfNull(targetFormat);

        if (input.Length == 0)
        {
            return [];
        }

        // If formats already match, return input as-is
        if (sourceFormat.SampleRate == targetFormat.SampleRate &&
            sourceFormat.BitsPerSample == targetFormat.BitsPerSample &&
            sourceFormat.Channels == targetFormat.Channels)
        {
            return input;
        }

        // Build NAudio wave format for input
        var naudioSource = ToNAudioFormat(sourceFormat);

        // Create raw source stream from input bytes
        using var inputStream = new RawSourceWaveStream(new MemoryStream(input), naudioSource);

        // Convert to float samples (ISampleProvider)
        ISampleProvider sampleProvider = ConvertToSamples(inputStream);

        // Resample if needed
        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, targetFormat.SampleRate);
        }

        // Convert stereo to mono if needed
        if (sampleProvider.WaveFormat.Channels == 2 && targetFormat.Channels == 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
        }
        else if (sampleProvider.WaveFormat.Channels == 1 && targetFormat.Channels == 2)
        {
            sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
        }

        // Convert back to target bit depth
        if (targetFormat.BitsPerSample == 16)
        {
            var waveProvider16 = new SampleToWaveProvider16(sampleProvider);
            return ReadAllBytes(waveProvider16);
        }

        // For 32-bit float output
        var waveProvider = new SampleToWaveProvider(sampleProvider);
        return ReadAllBytes(waveProvider);
    }

    private static NAudio.Wave.WaveFormat ToNAudioFormat(CoreWaveFormat format)
    {
        if (format.BitsPerSample == 32)
        {
            return NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels);
        }

        return new NAudio.Wave.WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);
    }

    private static ISampleProvider ConvertToSamples(RawSourceWaveStream stream)
    {
        if (stream.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return new WaveToSampleProvider(stream);
        }

        if (stream.WaveFormat.BitsPerSample == 16)
        {
            return new Pcm16BitToSampleProvider(stream);
        }

        // Fallback: try generic conversion
        return new WaveToSampleProvider(new Wave16ToFloatProvider(stream));
    }

    private static byte[] ReadAllBytes(IWaveProvider provider)
    {
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }
}
