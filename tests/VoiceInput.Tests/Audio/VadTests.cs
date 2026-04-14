using System;
using FluentAssertions;
using VoiceInput.Core.Audio;
using Xunit;

namespace VoiceInput.Tests.Audio;

public class VadTests
{
    private static short[] GenerateSilence(int count) => new short[count];

    private static short[] GenerateTone(int count, double amplitude = 0.5)
    {
        var samples = new short[count];
        double peak = short.MaxValue * amplitude;
        for (int i = 0; i < count; i++)
        {
            samples[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000) * peak);
        }

        return samples;
    }

    [Fact]
    public void Silence_ReturnsNotSpeech()
    {
        var vad = new AmplitudeVad();
        var result = vad.ProcessSamples(GenerateSilence(1024));

        result.IsSpeech.Should().BeFalse();
        result.CurrentAmplitude.Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void LoudTone_ReturnsSpeech()
    {
        var vad = new AmplitudeVad();
        var result = vad.ProcessSamples(GenerateTone(1024, 0.5));

        result.IsSpeech.Should().BeTrue();
        result.CurrentAmplitude.Should().BeGreaterThan(0.3f);
    }

    [Fact]
    public void Amplitude_AlwaysInRange()
    {
        var vad = new AmplitudeVad();

        foreach (double amp in new[] { 0.0, 0.01, 0.1, 0.5, 1.0 })
        {
            var result = vad.ProcessSamples(GenerateTone(1024, amp));
            result.CurrentAmplitude.Should().BeInRange(0f, 1f);
        }
    }

    [Fact]
    public void SilenceDuration_AccumulatesAcrossCalls()
    {
        // debounceFrames=1 so silence is immediately detected
        var vad = new AmplitudeVad(debounceFrames: 1, sampleRate: 16000);

        // Each 1600-sample chunk at 16kHz = 100ms
        var silence = GenerateSilence(1600);

        vad.ProcessSamples(silence); // frame 1: hits debounce threshold
        var result = vad.ProcessSamples(silence); // frame 2: accumulates

        result.SilenceDurationMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AutoStop_AfterSilenceTimeout()
    {
        // 1500ms timeout, debounce=1, 16kHz, chunks of 1600 = 100ms each
        var vad = new AmplitudeVad(silenceTimeoutMs: 1500, debounceFrames: 1, sampleRate: 16000);
        var silence = GenerateSilence(1600);

        // Need >=16 chunks of 100ms to reach 1500ms (1 debounce + 15 accumulation)
        for (int i = 0; i < 20; i++)
        {
            vad.ProcessSamples(silence);
        }

        var result = vad.ProcessSamples(silence);
        result.SilenceDurationMs.Should().BeGreaterOrEqualTo(1500);
        result.IsSpeech.Should().BeFalse();
    }

    [Fact]
    public void Speech_ResetsSilenceCounter()
    {
        var vad = new AmplitudeVad(debounceFrames: 1, sampleRate: 16000);
        var silence = GenerateSilence(1600);
        var tone = GenerateTone(1600, 0.5);

        // Accumulate some silence
        for (int i = 0; i < 5; i++)
        {
            vad.ProcessSamples(silence);
        }

        // Speech resets counter
        var result = vad.ProcessSamples(tone);
        result.IsSpeech.Should().BeTrue();
        result.SilenceDurationMs.Should().Be(0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var vad = new AmplitudeVad(debounceFrames: 1, sampleRate: 16000);

        // Build up some state
        vad.ProcessSamples(GenerateTone(1600, 0.5));
        for (int i = 0; i < 5; i++)
        {
            vad.ProcessSamples(GenerateSilence(1600));
        }

        // Capture pre-reset silence
        var preReset = vad.ProcessSamples(GenerateSilence(1600));
        preReset.SilenceDurationMs.Should().BeGreaterThan(0);

        vad.Reset();

        // After reset, silence counter should be reset to 0
        // First silent frame will start accumulating again from scratch
        var result = vad.ProcessSamples(GenerateSilence(1600));
        result.SilenceDurationMs.Should().BeLessThan(preReset.SilenceDurationMs,
            "silence duration should be much lower after reset");
        result.IsSpeech.Should().BeFalse();
    }

    [Fact]
    public void Debounce_BriefSilenceDoesNotResetSpeech()
    {
        // debounce=3 means need 3 consecutive silent frames to declare silence
        var vad = new AmplitudeVad(debounceFrames: 3, sampleRate: 16000);

        // Start with speech
        vad.ProcessSamples(GenerateTone(1600, 0.5));

        // 2 silent frames (below debounce threshold of 3) — should still report speech
        vad.ProcessSamples(GenerateSilence(1600));
        var result = vad.ProcessSamples(GenerateSilence(1600));

        result.IsSpeech.Should().BeTrue("debounce hasn't been reached yet");
    }

    [Fact]
    public void EmptyInput_ReturnsPreviousState()
    {
        var vad = new AmplitudeVad();
        var result = vad.ProcessSamples(Array.Empty<short>());

        result.CurrentAmplitude.Should().Be(0f);
    }

    [Fact]
    public void NullInput_ThrowsArgumentNull()
    {
        var vad = new AmplitudeVad();
        var act = () => vad.ProcessSamples(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
