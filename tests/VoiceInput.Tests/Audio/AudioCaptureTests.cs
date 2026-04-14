using FluentAssertions;
using VoiceInput.Core.Audio;
using Xunit;

namespace VoiceInput.Tests.Audio;

/// <summary>
/// Tests for the <see cref="IAudioCapture"/> interface contract,
/// state transitions, and the <see cref="AudioDataEventArgs"/> / <see cref="WaveFormat"/> types.
/// All tests use a in-process stub — no real WASAPI device is required.
/// </summary>
public sealed class AudioCaptureTests
{
    // ── WaveFormat record ─────────────────────────────────────────────────────

    [Fact]
    public void WaveFormat_RecordEquality_SameValues_AreEqual()
    {
        var a = new WaveFormat(16_000, 16, 1);
        var b = new WaveFormat(16_000, 16, 1);

        a.Should().Be(b);
    }

    [Fact]
    public void WaveFormat_RecordEquality_DifferentValues_AreNotEqual()
    {
        var mono = new WaveFormat(16_000, 16, 1);
        var stereo = new WaveFormat(16_000, 16, 2);

        mono.Should().NotBe(stereo);
    }

    [Fact]
    public void WaveFormat_Whisper_HasExpectedValues()
    {
        WaveFormat.Whisper.SampleRate.Should().Be(16_000);
        WaveFormat.Whisper.BitsPerSample.Should().Be(16);
        WaveFormat.Whisper.Channels.Should().Be(1);
    }

    // ── AudioDataEventArgs ────────────────────────────────────────────────────

    [Fact]
    public void AudioDataEventArgs_Properties_AreExposedCorrectly()
    {
        var buffer = new byte[] { 1, 2, 3, 4 };
        var format = new WaveFormat(48_000, 32, 2);
        var args = new AudioDataEventArgs(buffer, bytesRecorded: 4, format);

        args.Buffer.Should().BeSameAs(buffer);
        args.BytesRecorded.Should().Be(4);
        args.Format.Should().Be(format);
    }

    [Fact]
    public void AudioDataEventArgs_BytesRecorded_CanBeLessThanBufferLength()
    {
        var buffer = new byte[512];
        var args = new AudioDataEventArgs(buffer, bytesRecorded: 256, WaveFormat.Whisper);

        args.BytesRecorded.Should().Be(256);
        args.Buffer.Length.Should().Be(512);
    }

    // ── IAudioCapture initial state ───────────────────────────────────────────

    [Fact]
    public void IAudioCapture_InitialState_IsNotCapturing()
    {
        using var capture = new StubAudioCapture();

        capture.IsCapturing.Should().BeFalse();
    }

    // ── State transitions ─────────────────────────────────────────────────────

    [Fact]
    public void IAudioCapture_AfterStartCapture_IsCapturingIsTrue()
    {
        using var capture = new StubAudioCapture();

        capture.StartCapture(deviceId: null);

        capture.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public void IAudioCapture_AfterStopCapture_IsCapturingIsFalse()
    {
        using var capture = new StubAudioCapture();

        capture.StartCapture(deviceId: null);
        capture.StopCapture();

        capture.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void IAudioCapture_FullCycle_NotCapturing_Start_Capturing_Stop_NotCapturing()
    {
        using var capture = new StubAudioCapture();

        capture.IsCapturing.Should().BeFalse("initial state");

        capture.StartCapture(deviceId: null);
        capture.IsCapturing.Should().BeTrue("after start");

        capture.StopCapture();
        capture.IsCapturing.Should().BeFalse("after stop");
    }

    [Fact]
    public void IAudioCapture_StartCapture_WhenAlreadyCapturing_IsIdempotent()
    {
        using var capture = new StubAudioCapture();

        capture.StartCapture(deviceId: null);
        capture.StartCapture(deviceId: null); // second call — must not throw

        capture.IsCapturing.Should().BeTrue();
        capture.StartCount.Should().Be(1, "second call should be a no-op");
    }

    [Fact]
    public void IAudioCapture_StopCapture_WhenNotCapturing_IsNoOp()
    {
        using var capture = new StubAudioCapture();

        var act = () => capture.StopCapture();

        act.Should().NotThrow();
        capture.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void IAudioCapture_StartCapture_WithDeviceId_PassesDeviceIdThrough()
    {
        const string deviceId = "{audio-device-guid-1234}";
        using var capture = new StubAudioCapture();

        capture.StartCapture(deviceId);

        capture.LastDeviceId.Should().Be(deviceId);
    }

    [Fact]
    public void IAudioCapture_StartCapture_WithNullDeviceId_UsesDefaultDevice()
    {
        using var capture = new StubAudioCapture();

        capture.StartCapture(deviceId: null);

        capture.LastDeviceId.Should().BeNull();
    }

    // ── AudioDataAvailable event ──────────────────────────────────────────────

    [Fact]
    public void IAudioCapture_AudioDataAvailable_FiresWithCorrectEventArgs()
    {
        using var capture = new StubAudioCapture();
        AudioDataEventArgs? received = null;
        capture.AudioDataAvailable += (_, args) => received = args;
        capture.StartCapture(deviceId: null);

        var expectedBuffer = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var expectedFormat = new WaveFormat(48_000, 16, 2);
        capture.SimulateAudioData(expectedBuffer, expectedFormat);

        received.Should().NotBeNull();
        received!.Buffer.Should().BeEquivalentTo(expectedBuffer);
        received.BytesRecorded.Should().Be(expectedBuffer.Length);
        received.Format.Should().Be(expectedFormat);
    }

    [Fact]
    public void IAudioCapture_AudioDataAvailable_DoesNotFireWhenNotCapturing()
    {
        using var capture = new StubAudioCapture();
        var eventFired = false;
        capture.AudioDataAvailable += (_, _) => eventFired = true;

        // Not started — event should not be raised by the real stub
        capture.SimulateAudioData(new byte[4], WaveFormat.Whisper);

        eventFired.Should().BeFalse();
    }

    [Fact]
    public void IAudioCapture_AudioDataAvailable_DoesNotFireAfterStop()
    {
        using var capture = new StubAudioCapture();
        capture.StartCapture(deviceId: null);
        capture.StopCapture();

        var eventFired = false;
        capture.AudioDataAvailable += (_, _) => eventFired = true;

        capture.SimulateAudioData(new byte[4], WaveFormat.Whisper);

        eventFired.Should().BeFalse();
    }

    // ── Stub implementation ───────────────────────────────────────────────────

    /// <summary>
    /// In-process stub that fulfils the <see cref="IAudioCapture"/> contract
    /// without any real audio hardware or WASAPI.
    /// </summary>
    private sealed class StubAudioCapture : IAudioCapture, IDisposable
    {
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        public bool IsCapturing { get; private set; }

        /// <summary>Number of times <see cref="StartCapture"/> actually started capture.</summary>
        public int StartCount { get; private set; }

        /// <summary>The device ID passed to the last <see cref="StartCapture"/> call.</summary>
        public string? LastDeviceId { get; private set; }

        public void StartCapture(string? deviceId)
        {
            if (IsCapturing)
                return;

            LastDeviceId = deviceId;
            IsCapturing = true;
            StartCount++;
        }

        public void StopCapture()
        {
            IsCapturing = false;
        }

        /// <summary>
        /// Simulates a burst of audio data arriving from the device.
        /// Only raises the event when <see cref="IsCapturing"/> is <c>true</c>.
        /// </summary>
        public void SimulateAudioData(byte[] buffer, WaveFormat format)
        {
            if (!IsCapturing)
                return;

            AudioDataAvailable?.Invoke(
                this,
                new AudioDataEventArgs(buffer, buffer.Length, format));
        }

        public void Dispose() => IsCapturing = false;
    }
}
