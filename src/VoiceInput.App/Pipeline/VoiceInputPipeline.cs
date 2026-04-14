using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
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

    private bool _disposed;

    public VoiceInputPipeline(
        IRecordingSession recording,
        ISttProvider sttProvider,
        IInjectionOrchestrator injector,
        IHistoryStore history,
        AppConfig config,
        ILlmProcessor? llmProcessor = null)
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
            _recording.AmplitudeChanged += OnAmplitude;
            _recording.AutoStopped += OnAutoStopped;
            await _recording.StartAsync(deviceId).ConfigureAwait(false);
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
            var audioData = await _recording.StopAsync().ConfigureAwait(false);

            if (audioData.Length == 0)
            {
                SetState(PipelineState.Idle);
                return "";
            }

            // STT
            var sttResult = await _sttProvider.TranscribeAsync(audioData, _config.Language, ct).ConfigureAwait(false);
            var text = sttResult.Text;

            // Optional LLM post-processing
            if (_config.LlmPostProcessing.Enabled && _llmProcessor != null && !string.IsNullOrWhiteSpace(text))
            {
                text = await _llmProcessor.ProcessAsync(text, _config.LlmPostProcessing.Mode, ct).ConfigureAwait(false);
            }

            // Inject text
            SetState(PipelineState.Injecting);
            await _injector.InjectAsync(text).ConfigureAwait(false);

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
            }).ConfigureAwait(false);

            SetState(PipelineState.Idle);
            return text;
        }
        catch (SttException ex)
        {
            ErrorOccurred?.Invoke(this, $"Transcription failed: {ex.Message}");
            SetState(PipelineState.Idle);
            return "";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Pipeline error: {ex.Message}");
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
        // Fire-and-forget with error handling to avoid async void
        _ = Task.Run(async () =>
        {
            try { await StopAndProcessAsync().ConfigureAwait(false); }
            catch { /* logged inside StopAndProcessAsync */ }
        });
    }

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
