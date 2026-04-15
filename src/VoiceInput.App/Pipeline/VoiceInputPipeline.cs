using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable VSTHRD110 // Observe the awaitable result — fire-and-forget is intentional for auto-stop
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VoiceInput.Core.Audio;
using VoiceInput.Core.Config;
using VoiceInput.Core.History;
using VoiceInput.Core.Injection;
using VoiceInput.Core.Llm;
using VoiceInput.Core.Stt;

namespace VoiceInput.App.Pipeline;

/// <summary>
/// End-to-end voice input pipeline: Hotkey → Record → STT → (LLM) → Inject → History.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VoiceInputPipeline : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private readonly IRecordingSession _recording;
    private readonly ISttProvider _sttProvider;
    private readonly IInjectionOrchestrator _injector;
    private readonly IHistoryStore _history;
    private readonly ILlmProcessor? _llmProcessor;
    private readonly AppConfig _config;
    private readonly ILogger _logger;
    private readonly SynchronizationContext? _syncContext;

    private bool _disposed;
    private int _recordingGeneration;

    public VoiceInputPipeline(
        IRecordingSession recording,
        ISttProvider sttProvider,
        IInjectionOrchestrator injector,
        IHistoryStore history,
        AppConfig config,
        ILlmProcessor? llmProcessor = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(sttProvider);
        ArgumentNullException.ThrowIfNull(injector);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(config);

        _recording = recording;
        _sttProvider = sttProvider;
        _injector = injector;
        _history = history;
        _config = config;
        _llmProcessor = llmProcessor;
        _logger = logger ?? NullLogger.Instance;
        _syncContext = SynchronizationContext.Current;
    }

    /// <summary>Current pipeline state.</summary>
    public PipelineState State { get; private set; } = PipelineState.Idle;

    /// <summary>Fires when state changes.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Fires with error message when something goes wrong.</summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>Fires with amplitude updates for UI animation.</summary>
    public event EventHandler<float>? AmplitudeChanged;

    /// <summary>Start recording.</summary>
    public async Task StartRecordingAsync(string? deviceId = null)
    {
        if (State != PipelineState.Idle) return;

        try
        {
            SetState(PipelineState.Recording);
            _recordingGeneration++;
            _recording.AmplitudeChanged += OnAmplitude;
            _recording.AutoStopped += OnAutoStopped;
            await _recording.StartAsync(deviceId);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
            SetState(PipelineState.Idle);
        }
    }

    /// <summary>Stop recording and process through the full pipeline.</summary>
    public async Task<string> StopAndProcessAsync(CancellationToken ct = default)
    {
        if (State != PipelineState.Recording) return "";

        try
        {
            SetState(PipelineState.Processing);
            _recording.AmplitudeChanged -= OnAmplitude;
            _recording.AutoStopped -= OnAutoStopped;

            var sw = Stopwatch.StartNew();
            var audioData = await _recording.StopAsync();
            _logger.LogInformation("Audio captured: {Bytes} bytes", audioData.Length);

            if (audioData.Length == 0)
            {
                _logger.LogWarning("Empty audio — skipping STT");
                SetState(PipelineState.Idle);
                return "";
            }

            // STT
            _logger.LogInformation("Sending {Bytes} bytes to {Provider}...", audioData.Length, _sttProvider.Name);
            var sttResult = await _sttProvider.TranscribeAsync(audioData, _config.Language, ct);
            _logger.LogInformation("STT result: \"{Text}\"", sttResult.Text);
            var text = sttResult.Text;

            // Optional LLM post-processing
            if (_config.LlmPostProcessing.Enabled && _llmProcessor != null && !string.IsNullOrWhiteSpace(text))
            {
                text = await _llmProcessor.ProcessAsync(text, _config.LlmPostProcessing.Mode, ct);
            }

            // Inject text (clipboard requires STA thread)
            SetState(PipelineState.Injecting);
            _logger.LogInformation("Injecting text: \"{Text}\"", text);
            try
            {
                await _injector.InjectAsync(text);
                _logger.LogInformation("Text injected successfully");
            }
            catch (Exception injEx)
            {
                _logger.LogError(injEx, "Text injection failed");
            }

            sw.Stop();

            // Log to history
            string targetApp = "";
            string targetTitle = "";
            try
            {
                var hwnd = GetForegroundWindow();
                GetWindowThreadProcessId(hwnd, out uint pid);
                using var proc = Process.GetProcessById((int)pid);
                targetApp = proc.ProcessName;
                targetTitle = proc.MainWindowTitle;
            }
            catch { /* optional */ }

            await _history.AddEntryAsync(new HistoryEntry
            {
                Text = text,
                DurationMs = (int)_recording.Duration.TotalMilliseconds,
                CharCount = text.Length,
                WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                SttProvider = _sttProvider.Name,
                SttModel = "",
                TargetApp = targetApp,
                TargetAppTitle = targetTitle,
                Language = _config.Language,
                LatencyMs = (int)sw.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow
            });

            SetState(PipelineState.Idle);
            return text;
        }
        catch (SttException ex)
        {
            ErrorOccurred?.Invoke(this, $"STT error ({ex.Kind}): {ex.Message}");
            SetState(PipelineState.Idle);
            return "";
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException != null ? $" → {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "";
            ErrorOccurred?.Invoke(this, $"{ex.GetType().Name}: {ex.Message}{inner}");
            SetState(PipelineState.Idle);
            return "";
        }
    }

    private void SetState(PipelineState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnAmplitude(object? sender, float amp) => AmplitudeChanged?.Invoke(this, amp);

    private void OnAutoStopped(object? sender, EventArgs e)
    {
        var gen = _recordingGeneration;
        _logger.LogInformation("OnAutoStopped fired, pipeline state: {State}, gen: {Gen}", State, gen);
        if (_syncContext != null)
        {
            #pragma warning disable VSTHRD001
            _syncContext.Post(_ =>
            {
                // Only process if this is still the same recording generation
                if (_recordingGeneration == gen && State == PipelineState.Recording)
                    ProcessAutoStop();
                else
                    _logger.LogInformation("OnAutoStopped skipped — generation mismatch (was {Old}, now {New})", gen, _recordingGeneration);
            }, null);
            #pragma warning restore VSTHRD001
        }
    }

    #pragma warning disable VSTHRD100 // async void is intentional — top-level fire-and-forget dispatched from SyncContext
    private async void ProcessAutoStop()
    {
        _logger.LogInformation("ProcessAutoStop executing, pipeline state: {State}", State);
        try { await StopAndProcessAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "ProcessAutoStop error"); }
    }
    #pragma warning restore VSTHRD100

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recording.AmplitudeChanged -= OnAmplitude;
        _recording.AutoStopped -= OnAutoStopped;
    }
}

/// <summary>State of the voice input pipeline.</summary>
public enum PipelineState
{
    Idle,
    Recording,
    Processing,
    Injecting
}
