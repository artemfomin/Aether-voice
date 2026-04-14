using System;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceInput.Core.Audio;
using Xunit;

namespace VoiceInput.Tests.Audio;

public class RecordingSessionTests : IDisposable
{
    private readonly StubAudioCapture _capture = new();
    private readonly StubResampler _resampler = new();
    private readonly AmplitudeVad _vad = new(debounceFrames: 1, sampleRate: 16000);
    private readonly RecordingSession _session;

    public RecordingSessionTests()
    {
        _session = new RecordingSession(_capture, _resampler, _vad);
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        _session.State.Should().Be(RecordingState.Idle);
        _session.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Start_SetsStateToRecording()
    {
        await _session.StartAsync();
        _session.State.Should().Be(RecordingState.Recording);
    }

    [Fact]
    public async Task StartThenStop_ReturnsToIdle()
    {
        await _session.StartAsync();

        // Feed some audio to make duration > 0.5s
        _capture.SimulateAudio(duration: TimeSpan.FromSeconds(1));
        await Task.Delay(600); // Let the duration timer accumulate

        var result = await _session.StopAsync();
        _session.State.Should().Be(RecordingState.Idle);
    }

    [Fact]
    public async Task Stop_WhenIdle_ReturnsEmpty()
    {
        var result = await _session.StopAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WhenAlreadyRecording_IsNoOp()
    {
        await _session.StartAsync();
        await _session.StartAsync(); // should not throw
        _session.State.Should().Be(RecordingState.Recording);
    }

    [Fact]
    public async Task AmplitudeChanged_FiresDuringRecording()
    {
        float lastAmplitude = -1;
        _session.AmplitudeChanged += (_, amp) => lastAmplitude = amp;

        await _session.StartAsync();
        _capture.SimulateAudioChunk(GenerateToneBytes(1024, 0.5), new WaveFormat(16000, 16, 1));

        lastAmplitude.Should().BeGreaterThanOrEqualTo(0f);
    }

    [Fact]
    public async Task Duration_TracksRecordingTime()
    {
        await _session.StartAsync();
        await Task.Delay(100);
        _session.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ShortRecording_ReturnsEmpty()
    {
        await _session.StartAsync();
        // Feed very little audio, stop quickly (< 0.5s)
        _capture.SimulateAudioChunk(new byte[100], new WaveFormat(16000, 16, 1));
        // Stop immediately — duration < 0.5s
        var result = await _session.StopAsync();
        result.Should().BeEmpty("recording was too short");
    }

    [Fact]
    public void Constructor_NullCapture_Throws()
    {
        var act = () => new RecordingSession(null!, _resampler, _vad);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullResampler_Throws()
    {
        var act = () => new RecordingSession(_capture, null!, _vad);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullVad_Throws()
    {
        var act = () => new RecordingSession(_capture, _resampler, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static byte[] GenerateToneBytes(int sampleCount, double amplitude)
    {
        var bytes = new byte[sampleCount * 2];
        double peak = short.MaxValue * amplitude;
        for (int i = 0; i < sampleCount; i++)
        {
            short value = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000) * peak);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return bytes;
    }

    /// <summary>Stub IAudioCapture that allows manual event simulation.</summary>
    private sealed class StubAudioCapture : IAudioCapture
    {
        public bool IsCapturing { get; private set; }
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        public void StartCapture(string? deviceId = null) => IsCapturing = true;
        public void StopCapture() => IsCapturing = false;

        public void SimulateAudioChunk(byte[] data, WaveFormat format)
        {
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(data, data.Length, format));
        }

        public void SimulateAudio(TimeSpan duration)
        {
            var format = WaveFormat.Whisper;
            int totalSamples = (int)(format.SampleRate * duration.TotalSeconds);
            var bytes = new byte[totalSamples * 2]; // 16-bit
            // Fill with low-level noise
            var rng = new Random(42);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)rng.Next(0, 5);
            }

            SimulateAudioChunk(bytes, format);
        }
    }

    /// <summary>Stub resampler that returns input unchanged (passthrough).</summary>
    private sealed class StubResampler : IAudioResampler
    {
        public byte[] Resample(byte[] input, WaveFormat sourceFormat, WaveFormat targetFormat) => input;
    }
}
