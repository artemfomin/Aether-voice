using System;
using FluentAssertions;
using VoiceInput.App.Audio;
using VoiceInput.Core.Audio;
using Xunit;

namespace VoiceInput.Tests.Audio;

public class AudioResamplerTests
{
    private readonly NAudioResampler _resampler = new();

    private static byte[] GenerateSineInt16(int sampleRate, int channels, double durationSec, double amplitude = 0.5)
    {
        int totalSamples = (int)(sampleRate * durationSec) * channels;
        var bytes = new byte[totalSamples * 2]; // 16-bit = 2 bytes per sample
        double peak = short.MaxValue * amplitude;
        int sampleIndex = 0;

        for (int i = 0; i < totalSamples; i++)
        {
            int channelSample = channels > 1 ? i / channels : i;
            short value = (short)(Math.Sin(2 * Math.PI * 440 * channelSample / sampleRate) * peak);
            bytes[sampleIndex++] = (byte)(value & 0xFF);
            bytes[sampleIndex++] = (byte)((value >> 8) & 0xFF);
        }

        return bytes;
    }

    private static byte[] GenerateSineFloat32(int sampleRate, int channels, double durationSec, double amplitude = 0.5)
    {
        int totalSamples = (int)(sampleRate * durationSec) * channels;
        var bytes = new byte[totalSamples * 4]; // 32-bit float = 4 bytes per sample
        int byteIndex = 0;

        for (int i = 0; i < totalSamples; i++)
        {
            int channelSample = channels > 1 ? i / channels : i;
            float value = (float)(Math.Sin(2 * Math.PI * 440 * channelSample / sampleRate) * amplitude);
            var floatBytes = BitConverter.GetBytes(value);
            bytes[byteIndex++] = floatBytes[0];
            bytes[byteIndex++] = floatBytes[1];
            bytes[byteIndex++] = floatBytes[2];
            bytes[byteIndex++] = floatBytes[3];
        }

        return bytes;
    }

    [Fact]
    public void Resample_48kStereoFloat32_To_16kMonoInt16()
    {
        var source = new WaveFormat(48000, 32, 2);
        var target = WaveFormat.Whisper; // 16kHz mono int16
        var input = GenerateSineFloat32(48000, 2, 1.0);

        var output = _resampler.Resample(input, source, target);

        // 1 second at 16kHz mono int16 = 16000 samples * 2 bytes = 32000 bytes
        // Allow 5% tolerance for resampler edge effects
        output.Length.Should().BeInRange(30000, 34000);
    }

    [Fact]
    public void Resample_44kStereoInt16_To_16kMonoInt16()
    {
        var source = new WaveFormat(44100, 16, 2);
        var target = WaveFormat.Whisper;
        var input = GenerateSineInt16(44100, 2, 1.0);

        var output = _resampler.Resample(input, source, target);

        // 1 second at 16kHz mono int16 = ~32000 bytes
        output.Length.Should().BeInRange(30000, 34000);
    }

    [Fact]
    public void Resample_SameFormat_ReturnsInputUnchanged()
    {
        var format = WaveFormat.Whisper;
        var input = GenerateSineInt16(16000, 1, 0.5);

        var output = _resampler.Resample(input, format, format);

        output.Should().BeSameAs(input, "passthrough should return the same array reference");
    }

    [Fact]
    public void Resample_EmptyInput_ReturnsEmptyOutput()
    {
        var source = new WaveFormat(48000, 16, 2);
        var target = WaveFormat.Whisper;

        var output = _resampler.Resample([], source, target);

        output.Should().BeEmpty();
    }

    [Fact]
    public void Resample_NullInput_ThrowsArgumentNull()
    {
        var format = WaveFormat.Whisper;
        var act = () => _resampler.Resample(null!, format, format);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resample_48kMonoInt16_To_16kMonoInt16()
    {
        var source = new WaveFormat(48000, 16, 1);
        var target = WaveFormat.Whisper;
        var input = GenerateSineInt16(48000, 1, 1.0);

        var output = _resampler.Resample(input, source, target);

        // 48k→16k mono: output should be ~1/3 of input by sample count
        // Input: 48000 * 2 = 96000 bytes, output: ~16000 * 2 = 32000 bytes
        output.Length.Should().BeInRange(30000, 34000);
    }

    [Fact]
    public void Resample_OutputIsValidInt16Pcm()
    {
        var source = new WaveFormat(48000, 32, 2);
        var target = WaveFormat.Whisper;
        var input = GenerateSineFloat32(48000, 2, 0.1);

        var output = _resampler.Resample(input, source, target);

        // Output length must be even (16-bit = 2 bytes per sample)
        (output.Length % 2).Should().Be(0, "output should be 16-bit aligned");
        output.Length.Should().BeGreaterThan(0);
    }
}
