using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using VoiceInput.App.Audio;
using VoiceInput.App.Focus;
using VoiceInput.App.Hotkey;
using VoiceInput.App.Injection;
using VoiceInput.App.Native;
using VoiceInput.App.Overlay;
using VoiceInput.App.Overlay.Animations;
using VoiceInput.App.Pipeline;
using VoiceInput.App.Settings;
using VoiceInput.App.Startup;
using VoiceInput.App.Tray;
using VoiceInput.Core;
using VoiceInput.Core.Audio;
using VoiceInput.Core.Config;
using VoiceInput.Core.Focus;
using VoiceInput.Core.History;
using VoiceInput.Core.Injection;
using VoiceInput.Core.Llm;
using VoiceInput.Core.Logging;
using VoiceInput.Core.Stt;
using VoiceInput.Core.Stt.Providers;

namespace VoiceInput.App;

[SupportedOSPlatform("windows6.0.6000")]
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private SingleInstanceGuard? _guard;
    private GlobalHotkeyService? _hotkey;
    private TrayIconService? _tray;
    private VoiceInputPipeline? _pipeline;
    private OverlayStateManager? _overlayState;
    private IslandWindow? _island;
    private IFocusDetector? _focusDetector;
    private SettingsWindow? _settingsWindow;
    private IConfigStore? _configStore;
    private IHistoryStore? _historyStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance check — exit immediately if another instance is running.
        _guard = new SingleInstanceGuard();
        if (!_guard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        try
        {
            // DI Container
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Efficiency Mode disabled — can interfere with network access in some configurations.
            // SetEfficiencyMode();

            // Bootstrap application services
            Bootstrap();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Aether Voice failed to start:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Aether Voice — Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        var logDir = LoggingSetup.GetDefaultLogDirectory();
        Directory.CreateDirectory(logDir);
        var serilogLogger = LoggingSetup.CreateLogger(logDir);

        services.AddSingleton(serilogLogger);
        services.AddLogging(builder => builder.AddSerilog(serilogLogger, dispose: false));

        // Core services (Config, VAD, History, SttProviderRegistry, stubs)
        services.AddVoiceInputCore();

        // HttpClient (shared, 60s timeout, no proxy for local STT servers)
        services.AddSingleton(_ => new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        }) { Timeout = TimeSpan.FromSeconds(60) });

        // Audio (App-level implementations)
        services.AddSingleton<IAudioCapture, WasapiAudioCapture>();
        services.AddSingleton<IAudioResampler, NAudioResampler>();
        services.AddSingleton<IRecordingSession>(sp =>
        {
            var cfg = sp.GetRequiredService<AppConfig>();
            return new RecordingSession(
                sp.GetRequiredService<IAudioCapture>(),
                sp.GetRequiredService<IAudioResampler>(),
                sp.GetRequiredService<IVoiceActivityDetector>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<RecordingSession>(),
                silenceTimeoutMs: cfg.SilenceTimeoutMs);
        });
        services.AddSingleton<IAudioDeviceEnumerator, WasapiDeviceEnumerator>();

        // Injection
        services.AddSingleton<IClipboardManager, Win32ClipboardManager>();
        services.AddSingleton<ITextInjector, SendInputTextInjector>();
        services.AddSingleton<IInjectionOrchestrator, SmartInjectionOrchestrator>();

        // Focus
        services.AddSingleton<IFocusDetector, UiaFocusDetector>();

        // Hotkey
        services.AddSingleton<GlobalHotkeyService>();

        // Overlay
        services.AddSingleton<IslandWindow>();
        services.AddSingleton<OverlayStateManager>();

        // Tray
        services.AddSingleton<TrayIconService>();
    }

    #pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs — Dispatcher.BeginInvoke is the standard WPF pattern for marshalling to the UI thread
    #pragma warning disable VSTHRD110 // Observe the awaitable result — fire-and-forget is intentional for UI event handlers
    private void Bootstrap()
    {
        var sp = _serviceProvider!;
        var logger = sp.GetRequiredService<ILogger<App>>();
        var config = sp.GetRequiredService<AppConfig>();
        var configStore = sp.GetRequiredService<IConfigStore>();
        _configStore = configStore;
        var registry = sp.GetRequiredService<SttProviderRegistry>();

        // Log VAD config
        var vad = sp.GetRequiredService<IVoiceActivityDetector>();
        if (vad is AmplitudeVad ampVad)
            logger.LogInformation("VAD config: silenceTimeout={Timeout}ms, speechThreshold={Threshold}",
                ampVad.SilenceTimeoutMs, 0.02f);

        // ── Register STT providers ───────────────────────────────────────────
        var httpClient = sp.GetRequiredService<HttpClient>();

        registry.Register(new OllamaSttProvider());
        registry.Register(new LmStudioSttProvider());

        var sttUrl = string.IsNullOrWhiteSpace(config.SttConfig.Url)
            ? GetDefaultSttUrl(config.SttProvider)
            : config.SttConfig.Url;

        // Register the 3 functional providers
        registry.Register(new OpenAiSttProvider(httpClient,
            config.SttProvider == SttProviderType.OpenAI && !string.IsNullOrWhiteSpace(config.SttConfig.Url)
                ? config.SttConfig.Url
                : "https://api.openai.com",
            config.SttConfig.ApiKey,
            string.IsNullOrWhiteSpace(config.SttConfig.Model) ? "whisper-1" : config.SttConfig.Model));

        registry.Register(new FasterWhisperSttProvider(httpClient,
            config.SttProvider == SttProviderType.FasterWhisper && !string.IsNullOrWhiteSpace(config.SttConfig.Url)
                ? config.SttConfig.Url
                : "http://localhost:8000",
            string.IsNullOrWhiteSpace(config.SttConfig.Model) ? "Systran/faster-distil-whisper-large-v3" : config.SttConfig.Model));

        registry.Register(new GoogleSttProvider(httpClient, config.SttConfig.ApiKey));

        registry.Register(new ParakeetSttProvider(httpClient,
            config.SttProvider == SttProviderType.Parakeet && !string.IsNullOrWhiteSpace(config.SttConfig.Url)
                ? config.SttConfig.Url
                : "http://localhost:8097"));

        // Resolve active provider
        ISttProvider activeStt = registry.Resolve(config.SttProvider);

        // ── Optional LLM post-processing ─────────────────────────────────────
        ILlmProcessor? llmProcessor = null;
        if (config.LlmPostProcessing.Enabled &&
            !string.IsNullOrWhiteSpace(config.LlmPostProcessing.Url))
        {
            llmProcessor = new OpenAiCompatLlmProcessor(
                httpClient,
                config.LlmPostProcessing.Url,
                config.LlmPostProcessing.ApiKey,
                config.LlmPostProcessing.Model);
        }

        // ── Pipeline ─────────────────────────────────────────────────────────
        var recording = sp.GetRequiredService<IRecordingSession>();
        var injector = sp.GetRequiredService<IInjectionOrchestrator>();
        var history = sp.GetRequiredService<IHistoryStore>();
        _historyStore = history;

        _pipeline = new VoiceInputPipeline(recording, activeStt, injector, history, config, llmProcessor,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<VoiceInputPipeline>());

        // ── Overlay + animation ──────────────────────────────────────────────
        _island = sp.GetRequiredService<IslandWindow>();
        _overlayState = sp.GetRequiredService<OverlayStateManager>();

        bool isRecording = false;
        bool isProcessing = false;
        DateTime lastHotkeyTime = DateTime.MinValue;

        _pipeline.StateChanged += (_, state) =>
        {
            _island.Dispatcher.BeginInvoke(() =>
            {
                _overlayState.OnPipelineStateChanged(state);
                // Reset flags when pipeline returns to Idle (e.g. after auto-stop)
                if (state == PipelineState.Idle)
                {
                    isRecording = false;
                    isProcessing = false;
                }
            });
        };

        _pipeline.AmplitudeChanged += (_, amp) =>
        {
            _island.Dispatcher.BeginInvoke(() => _overlayState.UpdateAmplitude(amp));
        };

        // ── Global hotkey ────────────────────────────────────────────────────
        _hotkey = sp.GetRequiredService<GlobalHotkeyService>();

        var modifiers = ParseModifiers(config.Hotkey.Modifiers);
        var key = ParseKey(config.Hotkey.Key);

        logger.LogInformation("Registering hotkey: {Modifiers}+{Key} (parsed: {ParsedMod}+{ParsedKey})",
            config.Hotkey.Modifiers, config.Hotkey.Key, modifiers, key);

        if (!_hotkey.Register(modifiers, key))
        {
            logger.LogWarning("Failed to register hotkey {Modifiers}+{Key} — may be taken by another app",
                modifiers, key);
        }
        else
        {
            logger.LogInformation("Hotkey registered successfully");
        }

        _pipeline.ErrorOccurred += (_, msg) =>
        {
            _island.Dispatcher.BeginInvoke(() =>
            {
                isRecording = false;
                isProcessing = false;
                _overlayState.ShowError(msg);
            });
            logger.LogWarning("Pipeline error: {Error}", msg);
        };
        _hotkey.HotkeyPressed += (_, _) =>
        {
            _island.Dispatcher.BeginInvoke(async () =>
            {
                // Debounce: ignore hotkey repeat within 500ms
                var now = DateTime.UtcNow;
                if ((now - lastHotkeyTime).TotalMilliseconds < 500) return;
                lastHotkeyTime = now;

                // Ignore while STT is processing
                if (isProcessing) return;

                try
                {
                    logger.LogInformation("Hotkey pressed: isRecording={IsRec}, isProcessing={IsProc}, pipelineState={State}",
                        isRecording, isProcessing, _pipeline.State);

                    if (!isRecording)
                    {
                        isRecording = true;
                        logger.LogInformation("→ START recording");
                        var liveConfig = _configStore.Load();
                        var anim = CreateAnimation(liveConfig.AnimationStyle);
                        _overlayState.ShowRecording(anim);
                        await _pipeline.StartRecordingAsync(
                            string.IsNullOrWhiteSpace(liveConfig.AudioDeviceId) ? null : liveConfig.AudioDeviceId);
                    }
                    else
                    {
                        isRecording = false;
                        isProcessing = true;
                        logger.LogInformation("→ STOP recording, starting processing");
                        var text = await _pipeline.StopAndProcessAsync();
                        isProcessing = false;
                        logger.LogInformation("→ Processing complete: \"{Text}\"", text);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _overlayState.ShowSuccess(text);
                        }
                        else
                        {
                            _overlayState.Hide();
                        }
                    }
                }
                catch (Exception ex)
                {
                    isRecording = false;
                    isProcessing = false;
                    logger.LogError(ex, "Hotkey handler error");
                    _overlayState.ShowError(ex.Message);
                }
            });
        };

        // ── Focus detection ──────────────────────────────────────────────────
        _focusDetector = sp.GetRequiredService<IFocusDetector>();

        if (config.ActivationMode is ActivationMode.AutoDetect or ActivationMode.Both)
        {
            _focusDetector.TextFieldFocused += (_, args) =>
            {
                if (EdgeCaseHelper.IsFullScreenAppRunning()) return;

                _island.Dispatcher.BeginInvoke(() =>
                {
                    var pos = OverlayPositioner.Calculate(
                        args.CaretBounds, _island.Width, _island.Height);
                    _island.Left = pos.X;
                    _island.Top = pos.Y;
                });
            };

            _focusDetector.TextFieldLostFocus += (_, _) =>
            {
                if (!isRecording)
                {
                    _island.Dispatcher.BeginInvoke(() => _overlayState.Hide());
                }
            };

            _focusDetector.StartMonitoring();
        }

        // ── Tray icon ────────────────────────────────────────────────────────
        _tray = sp.GetRequiredService<TrayIconService>();
        _tray.Initialize();

        _tray.SettingsRequested += (_, _) => ShowSettings();

        _tray.ExitRequested += (_, _) => Shutdown();

        _tray.PauseToggled += (_, paused) =>
        {
            if (paused)
            {
                _focusDetector?.StopMonitoring();
                _hotkey?.Unregister();
            }
            else
            {
                if (config.ActivationMode is ActivationMode.AutoDetect or ActivationMode.Both)
                {
                    _focusDetector?.StartMonitoring();
                }
                _hotkey?.Register(modifiers, key);
            }
        };

        _pipeline.StateChanged += (_, state) =>
        {
            _tray.SetRecording(state == PipelineState.Recording);
        };

        // ── Live config reload ────────────────────────────────────────────
        configStore.ConfigChanged += (_, newConfig) =>
        {
            _island.Dispatcher.BeginInvoke(() =>
            {
                logger.LogInformation("Config changed — applying live updates");

                // Re-register hotkey if changed
                var newMod = ParseModifiers(newConfig.Hotkey.Modifiers);
                var newKey = ParseKey(newConfig.Hotkey.Key);
                if (newMod != modifiers || newKey != key)
                {
                    _hotkey?.Unregister();
                    modifiers = newMod;
                    key = newKey;
                    if (_hotkey?.Register(modifiers, key) == true)
                        logger.LogInformation("Hotkey re-registered: {Mod}+{Key}", modifiers, key);
                    else
                        logger.LogWarning("Failed to re-register hotkey {Mod}+{Key}", modifiers, key);
                }

                // Update silence timeout on RecordingSession
                if (recording is RecordingSession rs)
                {
                    rs.SetSilenceTimeout(newConfig.SilenceTimeoutMs);
                    logger.LogInformation("Silence timeout updated: {Ms}ms", newConfig.SilenceTimeoutMs);
                }

                logger.LogInformation("Live config applied (animation, audio device, hotkey, silence timeout)");
            });
        };

        logger.LogInformation("Aether Voice started (STT: {Provider}, Hotkey: {Hotkey})",
            config.SttProvider, $"{config.Hotkey.Modifiers}+{config.Hotkey.Key}");
    }
    #pragma warning restore VSTHRD110
    #pragma warning restore VSTHRD001

    #pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs — Dispatcher.BeginInvoke is the standard WPF pattern for marshalling to the UI thread
    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            return;
        }

        var currentConfig = _configStore!.Load();
        _settingsWindow = new SettingsWindow(currentConfig, updatedConfig =>
        {
            _configStore.Save(updatedConfig);
        }, _historyStore!);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }
    #pragma warning restore VSTHRD001

    protected override void OnExit(ExitEventArgs e)
    {
        _focusDetector?.StopMonitoring();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _pipeline?.Dispose();
        _island?.Close();
        (_serviceProvider as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }

    private static string GetDefaultSttUrl(SttProviderType provider) => provider switch
    {
        SttProviderType.OpenAI => "https://api.openai.com",
        SttProviderType.FasterWhisper => "http://localhost:8000",
        SttProviderType.Google => "https://speech.googleapis.com",
        SttProviderType.Ollama => "http://localhost:11434",
        SttProviderType.LMStudio => "http://localhost:1234",
        SttProviderType.Parakeet => "http://localhost:8097",
        _ => ""
    };

    private static ModifierKeys ParseModifiers(string? modifiersStr)
    {
        if (string.IsNullOrWhiteSpace(modifiersStr))
            return ModifierKeys.Control | ModifierKeys.Shift;

        var result = ModifierKeys.None;
        var parts = modifiersStr.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                result |= ModifierKeys.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                result |= ModifierKeys.Shift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                result |= ModifierKeys.Alt;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                result |= ModifierKeys.Windows;
        }

        return result == ModifierKeys.None ? ModifierKeys.Control | ModifierKeys.Shift : result;
    }

    private static Key ParseKey(string? keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr))
            return Key.Space;

        if (Enum.TryParse<Key>(keyStr, ignoreCase: true, out var key))
            return key;

        return Key.Space;
    }

    private static IRecordingAnimation CreateAnimation(AnimationStyle style) => style switch
    {
        AnimationStyle.GradientOrb => new GradientOrbAnimation(),
        AnimationStyle.PulsingRings => new PulsingRingsAnimation(),
        AnimationStyle.ParticleCloud => new ParticleCloudAnimation(),
        AnimationStyle.WaveformBars => new WaveformBarsAnimation(),
        _ => new GradientOrbAnimation()
    };

    /// <summary>
    /// Enables Windows 11 Efficiency Mode for this process via SetProcessInformation.
    /// Lowers scheduling priority and enables power throttling for background tray apps.
    /// Silently ignored on older Windows versions that do not support this API.
    /// </summary>
    private static void SetEfficiencyMode()
    {
        try
        {
            NativeMethods.EnableEfficiencyMode();
        }
        catch (Exception)
        {
            // Non-critical: older Windows versions may not support this API.
        }
    }
}
