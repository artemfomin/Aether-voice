using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoiceInput.Core.Audio;
using CoreWaveFormat = VoiceInput.Core.Audio.WaveFormat;

namespace VoiceInput.App.Audio;

/// <summary>
/// WASAPI-based audio capture running in <b>shared mode</b> so it can operate
/// alongside other applications that use the microphone (Teams, Zoom, etc.).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiAudioCapture : IAudioCapture, IDisposable
{
    private readonly ILogger<WasapiAudioCapture> _logger;
    private NAudio.CoreAudioApi.WasapiCapture? _capture;
    private MMDevice? _device;
    private volatile bool _isCapturing;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    /// <inheritdoc/>
    public bool IsCapturing => _isCapturing;

    /// <summary>Initialises a new instance of <see cref="WasapiAudioCapture"/>.</summary>
    public WasapiAudioCapture(ILogger<WasapiAudioCapture> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void StartCapture(string? deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isCapturing)
            return;

        try
        {
            InitialiseCapture(deviceId);
            _capture!.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            _isCapturing = true;

            _logger.LogInformation(
                "WASAPI capture started (device: {DeviceId})",
                string.IsNullOrEmpty(deviceId) ? "default" : deviceId);
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Failed to start WASAPI capture — COM error");
            DisposeCapture();
            throw;
        }
    }

    /// <inheritdoc/>
    public void StopCapture()
    {
        if (!_isCapturing)
            return;

        _isCapturing = false;
        _capture?.StopRecording();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void InitialiseCapture(string? deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            using var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDevice(deviceId);
            _capture = new NAudio.CoreAudioApi.WasapiCapture(_device);
        }
        else
        {
            _capture = new NAudio.CoreAudioApi.WasapiCapture();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0)
            return;

        var capture = _capture;
        if (capture is null)
            return;

        var format = MapWaveFormat(capture.WaveFormat);
        var buffer = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, buffer, e.BytesRecorded);

        AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(buffer, e.BytesRecorded, format));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;

        if (e.Exception is COMException comEx)
        {
            _logger.LogError(comEx, "WASAPI capture stopped — COM error (device disconnected?)");
        }
        else if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "WASAPI capture stopped unexpectedly");
        }
        else
        {
            _logger.LogInformation("WASAPI capture stopped cleanly");
        }
    }

    private static CoreWaveFormat MapWaveFormat(NAudio.Wave.WaveFormat nf) =>
        new(nf.SampleRate, nf.BitsPerSample, nf.Channels);

    private void DisposeCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _isCapturing = false;
        DisposeCapture();
    }
}
