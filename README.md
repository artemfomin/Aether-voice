# Aether Voice

**System-wide voice input service for Windows 11.** Press a hotkey, speak, and your words appear as text in any application.

Aether Voice lives in the system tray, captures audio via WASAPI, transcribes speech through configurable STT providers, and injects the result into the focused text field via smart clipboard paste.

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)

---

## Features

- **Push-to-talk & auto-stop** — press `Ctrl+Alt+V` to start recording, press again to stop, or let silence auto-stop after a configurable timeout (default 2s)
- **4 audio-reactive overlay animations** — Gradient Orb, Pulsing Rings, Particle Cloud (60 dust particles with Brownian motion), Waveform Bars
- **Floating island overlay** — transparent pill-shaped window, draggable, remembers position, appears at bottom-center of screen
- **6 STT providers**:
  - **NVIDIA Parakeet (NeMo)** — tested, fully working (`POST /transcribe`)
  - **OpenAI Whisper** — implemented, **not yet tested with real API**
  - **Google Cloud Speech-to-Text** — implemented, **not yet tested with real API**
  - **faster-whisper** — implemented (OpenAI-compatible local server)
  - **Ollama** — stub (waiting for upstream STT support)
  - **LM Studio** — stub (waiting for upstream STT support)
- **Smart clipboard injection** — saves clipboard state, pastes via `Ctrl+V` (or `Ctrl+Shift+V` for terminals), restores clipboard. Releases held modifier keys before pasting to avoid conflicts
- **Optional LLM post-processing** — clean up, punctuate, or translate transcribed text via any OpenAI-compatible API (Ollama, LM Studio, OpenAI)
- **SQLite history** — stores all transcriptions with duration, word count, target app, latency. Viewable in Settings
- **Dark theme Settings UI** — Windows 11 Fluent Design with acrylic backdrop, vertical navigation, 7 tabs (General, Speech Recognition, Audio, Hotkey, Appearance, AI Processing, History)
- **Live config reload** — animation style, hotkey, audio device, silence timeout update instantly without restart
- **Per-user MSI installer** — no admin rights needed, Start Menu + Desktop shortcuts, optional Run at Startup, Repair/Remove support
- **Efficiency-first** — `WS_EX_NOACTIVATE` overlay (never steals focus), single instance via named mutex, Serilog rolling file logs, DPAPI-encrypted API keys

## Architecture

```
VoiceInput.sln
├── src/VoiceInput.Core/        .NET 9 class library (zero WPF dependencies)
│   ├── Audio/                  IAudioCapture, IRecordingSession, AmplitudeVad
│   ├── Config/                 IConfigStore, JsonConfigStore (DPAPI), AppConfig
│   ├── Stt/                    ISttProvider, SttProviderRegistry, 6 providers
│   ├── Injection/              IClipboardManager, ITextInjector, IInjectionOrchestrator
│   ├── History/                IHistoryStore, SqliteHistoryStore
│   ├── Focus/                  IFocusDetector
│   ├── Llm/                    ILlmProcessor, OpenAiCompatLlmProcessor
│   └── Logging/                Serilog setup
│
├── src/VoiceInput.App/         WPF application (net9.0-windows)
│   ├── Startup/                App.xaml.cs (DI + bootstrap), SingleInstanceGuard
│   ├── Audio/                  WasapiAudioCapture, NAudioResampler, WasapiDeviceEnumerator
│   ├── Overlay/                IslandWindow, OverlayStateManager, 4 animations
│   ├── Injection/              Win32ClipboardManager, SendInputTextInjector, SmartInjectionOrchestrator
│   ├── Focus/                  UiaFocusDetector
│   ├── Hotkey/                 GlobalHotkeyService (RegisterHotKey)
│   ├── Tray/                   TrayIconService (H.NotifyIcon)
│   ├── Pipeline/               VoiceInputPipeline (E2E orchestration)
│   └── Settings/               SettingsWindow (dark theme), HistoryView, AudioLevelMeter
│
├── tests/VoiceInput.Tests/     168 xUnit tests
├── installer/                  WiX v5 MSI installer
└── build-installer.ps1         Build + optional silent install script
```

## Quick Start

### Prerequisites

- Windows 10/11 (x64)
- A microphone
- An STT server (see [STT Providers](#stt-providers))

### Install from Release

1. Download `VoiceInputSetup.msi` from [Releases](https://github.com/artemfomin/Aether-voice/releases)
2. Run the MSI — installs to `%LocalAppData%\AetherVoice\App\`
3. Launch **Aether Voice** from Start Menu
4. Configure your STT provider in Settings (click tray icon)
5. Press `Ctrl+Alt+V` to start recording, press again to stop

### Build from Source

```powershell
# Clone
git clone https://github.com/artemfomin/Aether-voice.git
cd Aether-voice

# Build
dotnet build VoiceInput.sln

# Run tests
dotnet test tests/VoiceInput.Tests/

# Build MSI installer
.\build-installer.ps1 -Version 1.0.0.0

# Build + silent install + launch
.\build-installer.ps1 -Version 1.0.0.0 -Install
```

## STT Providers

| Provider | Status | Setup |
|----------|--------|-------|
| **NVIDIA Parakeet (NeMo)** | Tested | Run NeMo Parakeet server, set URL to `http://<host>:8097` |
| **OpenAI Whisper** | Implemented, **not tested** | Set API key and URL `https://api.openai.com` |
| **Google Cloud STT** | Implemented, **not tested** | Set API key |
| **faster-whisper** | Implemented | Run [speaches](https://github.com/speaches-ai/speaches) server, set URL |
| **Ollama** | Stub | Waiting for [ollama#15243](https://github.com/ollama/ollama/pull/15243) |
| **LM Studio** | Stub | Waiting for transcribe API |

> **Note:** Only the Parakeet provider has been tested end-to-end in production. OpenAI and Google providers are implemented following their official API specs but have not been verified with real API keys. They should work, but may need minor adjustments.

## Configuration

Settings are stored in `%AppData%\VoiceInput\config.json` (API keys encrypted with DPAPI).

| Setting | Default | Description |
|---------|---------|-------------|
| `SttProvider` | `Parakeet` | Active STT provider |
| `SttConfig.Url` | varies | STT server endpoint |
| `SttConfig.ApiKey` | — | API key (DPAPI encrypted) |
| `SttConfig.Model` | varies | Model name |
| `Hotkey.Modifiers` | `Ctrl+Alt` | Hotkey modifier keys |
| `Hotkey.Key` | `V` | Hotkey key |
| `SilenceTimeoutMs` | `2000` | Auto-stop after silence (0 = disabled, push-to-talk only) |
| `AnimationStyle` | `GradientOrb` | Overlay animation (GradientOrb, PulsingRings, ParticleCloud, WaveformBars) |
| `Language` | `ru` | Recognition language |
| `LlmPostProcessing.Enabled` | `false` | Enable LLM text cleanup |

All settings except STT provider/URL can be changed live without restart.

## Logs

Rolling log files at `%AppData%\VoiceInput\logs\voiceinput-YYYYMMDD.log`

## Known Limitations

- **OpenAI and Google STT providers are not tested** — implemented per API docs but not verified with real keys
- **Ollama and LM Studio STT** — stubs only, waiting for upstream support
- WASAPI captures in int32 PCM on some devices — VAD threshold tuned for this but may need adjustment
- Clipboard injection may not work in elevated (admin) applications
- Settings for STT provider/URL require app restart to take effect

## Tech Stack

- **.NET 9**, C# 13, WPF
- **NAudio** — WASAPI audio capture + resampling
- **H.NotifyIcon.Wpf** — system tray
- **SQLite** (Microsoft.Data.Sqlite) — history storage
- **Serilog** — structured logging
- **WiX Toolset v5** — MSI installer
- **xUnit + Moq + FluentAssertions** — 168 tests

## License

Apache License 2.0 — see [LICENSE](LICENSE)
