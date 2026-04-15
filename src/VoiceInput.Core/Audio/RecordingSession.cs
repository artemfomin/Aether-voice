using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace VoiceInput.Core.Audio;

/// <summary>
/// Orchestrates audio recording: captures via IAudioCapture, feeds VAD for amplitude,
/// accumulates raw PCM, and resamples to Whisper format on stop.
/// </summary>
public sealed class RecordingSession : IRecordingSession, IDisposable
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinDuration = TimeSpan.FromMilliseconds(500);

    private readonly IAudioCapture _capture;
    private readonly IAudioResampler _resampler;
    private readonly IVoiceActivityDetector _vad;
    private readonly ILogger _logger;
    private readonly int _silenceTimeoutMs;

    private readonly object _lock = new();
    private MemoryStream? _buffer;
    private WaveFormat? _captureFormat;
    private Stopwatch? _stopwatch;
    private Timer? _maxDurationTimer;
    private volatile RecordingState _state;
    private volatile bool _speechDetected;
    private volatile bool _autoStopFired;
    private bool _disposed;

    public RecordingSession(
        IAudioCapture capture,
        IAudioResampler resampler,
        IVoiceActivityDetector vad,
        ILogger? logger = null,
        int silenceTimeoutMs = 0)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(resampler);
        ArgumentNullException.ThrowIfNull(vad);

        _capture = capture;
        _resampler = resampler;
        _vad = vad;
        _logger = logger ?? NullLogger.Instance;
        _silenceTimeoutMs = silenceTimeoutMs;
    }

    public RecordingState State => _state;

    public TimeSpan Duration => _stopwatch?.Elapsed ?? TimeSpan.Zero;

    public event EventHandler<float>? AmplitudeChanged;
    public event EventHandler? AutoStopped;

    public Task StartAsync(string? deviceId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state == RecordingState.Recording)
        {
            _logger.LogWarning("StartAsync called while already recording — ignoring");
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _buffer = new MemoryStream();
            _captureFormat = null;
            _vad.Reset();
            _speechDetected = false;
            _autoStopFired = false;
            _dataCallbackCount = 0;
            _stopwatch = Stopwatch.StartNew();
            _state = RecordingState.Recording;
        }

        _capture.AudioDataAvailable += OnAudioData;
        _capture.StartCapture(deviceId);

        // Max duration safety timer (5 minutes)
        _maxDurationTimer = new Timer(
            _ => _ = HandleMaxDurationAsync(),
            null,
            MaxDuration,
            Timeout.InfiniteTimeSpan);

        _logger.LogInformation("Recording started (device: {DeviceId})", deviceId ?? "default");
        return Task.CompletedTask;
    }

    public Task<byte[]> StopAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state != RecordingState.Recording)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        return Task.FromResult(StopAndProcess());
    }

    private byte[] StopAndProcess()
    {
        _state = RecordingState.Processing;
        _stopwatch?.Stop();
        _maxDurationTimer?.Dispose();
        _maxDurationTimer = null;

        _capture.AudioDataAvailable -= OnAudioData;
        _capture.StopCapture();

        byte[] rawAudio;
        WaveFormat? sourceFormat;

        lock (_lock)
        {
            rawAudio = _buffer?.ToArray() ?? [];
            sourceFormat = _captureFormat;
            _buffer?.Dispose();
            _buffer = null;
        }

        _state = RecordingState.Idle;

        var duration = _stopwatch?.Elapsed ?? TimeSpan.Zero;
        _logger.LogInformation("Recording stopped (duration: {Duration:F1}s, raw bytes: {Bytes})",
            duration.TotalSeconds, rawAudio.Length);

        // Too short — discard
        if (duration < MinDuration)
        {
            _logger.LogWarning("Recording too short ({Duration:F1}s < 0.5s) — discarded", duration.TotalSeconds);
            return [];
        }

        if (rawAudio.Length == 0 || sourceFormat is null)
        {
            return [];
        }

        // Resample to Whisper format
        try
        {
            return _resampler.Resample(rawAudio, sourceFormat, WaveFormat.Whisper);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resample audio");
            return [];
        }
    }

    private int _dataCallbackCount;

    private void OnAudioData(object? sender, AudioDataEventArgs e)
    {
        if (_state != RecordingState.Recording) return;

        var count = Interlocked.Increment(ref _dataCallbackCount);
        if (count == 1)
            _logger.LogInformation("First audio data callback: {Bytes} bytes, format: {Rate}Hz/{Bits}bit/{Ch}ch",
                e.BytesRecorded, e.Format.SampleRate, e.Format.BitsPerSample, e.Format.Channels);

        lock (_lock)
        {
            _buffer?.Write(e.Buffer, 0, e.BytesRecorded);
            _captureFormat ??= e.Format;
        }

        // Feed VAD with int16 samples for amplitude detection
        var samples = ConvertToInt16Samples(e.Buffer, e.BytesRecorded, e.Format);
        if (samples.Length > 0)
        {
            var vadResult = _vad.ProcessSamples(samples);
            AmplitudeChanged?.Invoke(this, vadResult.CurrentAmplitude);

            if (vadResult.IsSpeech)
                _speechDetected = true;

            // Log every 50 callbacks (~0.5 sec)
            if (count % 50 == 0 && _vad is AmplitudeVad ampVadLog)
            {
                _logger.LogInformation("VAD[{Count}]: amp={Amp:F4}, speech={Speech}, speechDetected={SD}, silence={SilMs}ms/{TimeoutMs}ms, dur={Dur:F1}s",
                    count, vadResult.CurrentAmplitude, vadResult.IsSpeech, _speechDetected,
                    vadResult.SilenceDurationMs, ampVadLog.SilenceTimeoutMs, Duration.TotalSeconds);
            }

            // VAD auto-stop: only if timeout > 0 (0 = disabled / push-to-talk only)
            if (_silenceTimeoutMs > 0 &&
                _speechDetected &&
                _vad is AmplitudeVad ampVad &&
                vadResult.SilenceDurationMs >= _silenceTimeoutMs &&
                Duration > MinDuration)
            {
                _logger.LogInformation("VAD silence timeout ({SilenceMs}ms >= {TimeoutMs}ms) — auto-stopping",
                    vadResult.SilenceDurationMs, _silenceTimeoutMs);
                _ = HandleAutoStopAsync();
            }
        }
    }

    private Task HandleMaxDurationAsync()
    {
        if (_state != RecordingState.Recording) return Task.CompletedTask;
        _logger.LogWarning("Max recording duration reached (5 min) — auto-stopping");
        AutoStopped?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Task HandleAutoStopAsync()
    {
        if (_autoStopFired || _state != RecordingState.Recording) return Task.CompletedTask;
        _autoStopFired = true;
        _logger.LogInformation("HandleAutoStopAsync firing (duration={Duration:F1}s)", Duration.TotalSeconds);
        AutoStopped?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private static short[] ConvertToInt16Samples(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (format.BitsPerSample == 16)
        {
            int sampleCount = bytesRecorded / 2;
            var samples = new short[sampleCount];
            Buffer.BlockCopy(buffer, 0, samples, 0, sampleCount * 2);
            return samples;
        }

        if (format.BitsPerSample == 32) // float32
        {
            int sampleCount = bytesRecorded / 4;
            var samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float value = BitConverter.ToSingle(buffer, i * 4);
                samples[i] = (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
            }

            return samples;
        }

        return [];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _maxDurationTimer?.Dispose();
        _capture.AudioDataAvailable -= OnAudioData;

        lock (_lock)
        {
            _buffer?.Dispose();
        }
    }
}
