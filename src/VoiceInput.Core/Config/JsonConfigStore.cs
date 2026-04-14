using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceInput.Core.Config;

/// <summary>
/// Persists <see cref="AppConfig"/> as indented JSON with DPAPI-encrypted API keys.
/// </summary>
/// <remarks>
/// This implementation relies on Windows DPAPI (<see cref="ProtectedData"/>)
/// and is therefore Windows-only.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _configPath;

    public event EventHandler<AppConfig>? ConfigChanged;

    public JsonConfigStore()
        : this(DefaultConfigPath())
    {
    }

    /// <summary>Allows injecting a custom path (used in tests).</summary>
    public JsonConfigStore(string configPath)
    {
        _configPath = configPath;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return new AppConfig();

        var json = File.ReadAllText(_configPath, Encoding.UTF8);
        var stored = JsonSerializer.Deserialize<StoredConfig>(json, SerializerOptions)
                     ?? new StoredConfig();

        return MapToAppConfig(stored);
    }

    public void Save(AppConfig config)
    {
        EnsureDirectoryExists();

        var stored = MapToStoredConfig(config);
        var json = JsonSerializer.Serialize(stored, SerializerOptions);
        File.WriteAllText(_configPath, json, Encoding.UTF8);

        ConfigChanged?.Invoke(this, config);
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static AppConfig MapToAppConfig(StoredConfig stored) => new()
    {
        SchemaVersion = stored.SchemaVersion,
        SttProvider = stored.SttProvider,
        SttConfig = new SttProviderConfig
        {
            Url = stored.SttConfig.Url,
            ApiKey = DecryptApiKey(stored.SttConfig.ApiKey),
            Model = stored.SttConfig.Model,
        },
        AudioDeviceId = stored.AudioDeviceId,
        ActivationMode = stored.ActivationMode,
        Hotkey = new HotkeyConfig
        {
            Modifiers = stored.Hotkey.Modifiers,
            Key = stored.Hotkey.Key,
        },
        RecordingMode = stored.RecordingMode,
        AnimationStyle = stored.AnimationStyle,
        LlmPostProcessing = new LlmPostProcessingConfig
        {
            Enabled = stored.LlmPostProcessing.Enabled,
            ProviderType = stored.LlmPostProcessing.ProviderType,
            Url = stored.LlmPostProcessing.Url,
            ApiKey = DecryptApiKey(stored.LlmPostProcessing.ApiKey),
            Model = stored.LlmPostProcessing.Model,
            Mode = stored.LlmPostProcessing.Mode,
        },
        RunAtStartup = stored.RunAtStartup,
        Language = stored.Language,
    };

    private static StoredConfig MapToStoredConfig(AppConfig config) => new()
    {
        SchemaVersion = config.SchemaVersion,
        SttProvider = config.SttProvider,
        SttConfig = new StoredSttProviderConfig
        {
            Url = config.SttConfig.Url,
            ApiKey = EncryptApiKey(config.SttConfig.ApiKey),
            Model = config.SttConfig.Model,
        },
        AudioDeviceId = config.AudioDeviceId,
        ActivationMode = config.ActivationMode,
        Hotkey = new StoredHotkeyConfig
        {
            Modifiers = config.Hotkey.Modifiers,
            Key = config.Hotkey.Key,
        },
        RecordingMode = config.RecordingMode,
        AnimationStyle = config.AnimationStyle,
        LlmPostProcessing = new StoredLlmPostProcessingConfig
        {
            Enabled = config.LlmPostProcessing.Enabled,
            ProviderType = config.LlmPostProcessing.ProviderType,
            Url = config.LlmPostProcessing.Url,
            ApiKey = EncryptApiKey(config.LlmPostProcessing.ApiKey),
            Model = config.LlmPostProcessing.Model,
            Mode = config.LlmPostProcessing.Mode,
        },
        RunAtStartup = config.RunAtStartup,
        Language = config.Language,
    };

    // ── DPAPI helpers ─────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    public static string EncryptApiKey(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return "";

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    [SupportedOSPlatform("windows")]
    public static string DecryptApiKey(string base64Ciphertext)
    {
        if (string.IsNullOrEmpty(base64Ciphertext))
            return "";

        var encrypted = Convert.FromBase64String(base64Ciphertext);
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    // ── Infrastructure ────────────────────────────────────────────────────────

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static string DefaultConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceInput",
            "config.json");

    // ── Stored DTOs (JSON shape) ──────────────────────────────────────────────

    private sealed class StoredConfig
    {
        public int SchemaVersion { get; set; } = 1;
        public SttProviderType SttProvider { get; set; } = SttProviderType.FasterWhisper;
        public StoredSttProviderConfig SttConfig { get; set; } = new();
        public string AudioDeviceId { get; set; } = "";
        public ActivationMode ActivationMode { get; set; } = ActivationMode.Both;
        public StoredHotkeyConfig Hotkey { get; set; } = new();
        public RecordingMode RecordingMode { get; set; } = RecordingMode.PushToTalk;
        public AnimationStyle AnimationStyle { get; set; } = AnimationStyle.GradientOrb;
        public StoredLlmPostProcessingConfig LlmPostProcessing { get; set; } = new();
        public bool RunAtStartup { get; set; } = false;
        public string Language { get; set; } = "ru";
    }

    private sealed class StoredSttProviderConfig
    {
        public string Url { get; set; } = "";
        /// <summary>Base64-encoded DPAPI ciphertext.</summary>
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
    }

    private sealed class StoredHotkeyConfig
    {
        public string Modifiers { get; set; } = "";
        public string Key { get; set; } = "";
    }

    private sealed class StoredLlmPostProcessingConfig
    {
        public bool Enabled { get; set; } = false;
        public string ProviderType { get; set; } = "";
        public string Url { get; set; } = "";
        /// <summary>Base64-encoded DPAPI ciphertext.</summary>
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public LlmPostProcessingMode Mode { get; set; } = LlmPostProcessingMode.GrammarFix;
    }
}
