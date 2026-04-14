using FluentAssertions;
using VoiceInput.Core.Config;
using Xunit;

namespace VoiceInput.Tests.Config;

public sealed class ConfigSerializationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigSerializationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"VoiceInputTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Round-trip serialization ──────────────────────────────────────────────

    [Fact]
    public void RoundTrip_AllFields_ArePreserved()
    {
        var original = BuildFullConfig();
        var store = new JsonConfigStore(_configPath);

        store.Save(original);
        var loaded = store.Load();

        loaded.SchemaVersion.Should().Be(original.SchemaVersion);
        loaded.SttProvider.Should().Be(original.SttProvider);
        loaded.SttConfig.Url.Should().Be(original.SttConfig.Url);
        loaded.SttConfig.ApiKey.Should().Be(original.SttConfig.ApiKey);
        loaded.SttConfig.Model.Should().Be(original.SttConfig.Model);
        loaded.AudioDeviceId.Should().Be(original.AudioDeviceId);
        loaded.ActivationMode.Should().Be(original.ActivationMode);
        loaded.Hotkey.Modifiers.Should().Be(original.Hotkey.Modifiers);
        loaded.Hotkey.Key.Should().Be(original.Hotkey.Key);
        loaded.RecordingMode.Should().Be(original.RecordingMode);
        loaded.AnimationStyle.Should().Be(original.AnimationStyle);
        loaded.LlmPostProcessing.Enabled.Should().Be(original.LlmPostProcessing.Enabled);
        loaded.LlmPostProcessing.ProviderType.Should().Be(original.LlmPostProcessing.ProviderType);
        loaded.LlmPostProcessing.Url.Should().Be(original.LlmPostProcessing.Url);
        loaded.LlmPostProcessing.ApiKey.Should().Be(original.LlmPostProcessing.ApiKey);
        loaded.LlmPostProcessing.Model.Should().Be(original.LlmPostProcessing.Model);
        loaded.LlmPostProcessing.Mode.Should().Be(original.LlmPostProcessing.Mode);
        loaded.RunAtStartup.Should().Be(original.RunAtStartup);
        loaded.Language.Should().Be(original.Language);
    }

    // ── Defaults when file missing ────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileMissing_ReturnsValidDefaults()
    {
        var missingPath = Path.Combine(_tempDir, "nonexistent.json");
        var store = new JsonConfigStore(missingPath);

        var config = store.Load();

        config.Should().NotBeNull();
        config.SchemaVersion.Should().Be(1);
        config.SttProvider.Should().Be(SttProviderType.FasterWhisper);
        config.SttConfig.Should().NotBeNull();
        config.SttConfig.ApiKey.Should().BeEmpty();
        config.AudioDeviceId.Should().BeEmpty();
        config.ActivationMode.Should().Be(ActivationMode.Both);
        config.Hotkey.Should().NotBeNull();
        config.RecordingMode.Should().Be(RecordingMode.PushToTalk);
        config.AnimationStyle.Should().Be(AnimationStyle.GradientOrb);
        config.LlmPostProcessing.Should().NotBeNull();
        config.LlmPostProcessing.Enabled.Should().BeFalse();
        config.RunAtStartup.Should().BeFalse();
        config.Language.Should().Be("ru");
    }

    // ── DPAPI encrypt / decrypt ───────────────────────────────────────────────

    [Fact]
    public void ApiKeys_AreEncryptedInJsonFile_NotStoredAsPlaintext()
    {
        const string sttKey = "sk-stt-secret-key";
        const string llmKey = "sk-llm-secret-key";

        var config = new AppConfig
        {
            SttConfig = new SttProviderConfig { ApiKey = sttKey },
            LlmPostProcessing = new LlmPostProcessingConfig { ApiKey = llmKey },
        };

        var store = new JsonConfigStore(_configPath);
        store.Save(config);

        var rawJson = File.ReadAllText(_configPath);

        rawJson.Should().NotContain(sttKey, "STT API key must not appear as plaintext in JSON");
        rawJson.Should().NotContain(llmKey, "LLM API key must not appear as plaintext in JSON");
    }

    [Fact]
    public void DpapiEncryptDecrypt_RoundTrip_ReturnsOriginalValue()
    {
        const string secret = "my-super-secret-api-key-12345";

        var encrypted = JsonConfigStore.EncryptApiKey(secret);
        var decrypted = JsonConfigStore.DecryptApiKey(encrypted);

        encrypted.Should().NotBe(secret, "encrypted value must differ from plaintext");
        decrypted.Should().Be(secret, "decrypted value must match original");
    }

    [Fact]
    public void DpapiEncryptDecrypt_EmptyString_ReturnsEmpty()
    {
        JsonConfigStore.EncryptApiKey("").Should().BeEmpty();
        JsonConfigStore.DecryptApiKey("").Should().BeEmpty();
    }

    // ── ConfigChanged event ───────────────────────────────────────────────────

    [Fact]
    public void Save_FiresConfigChangedEvent_WithSavedConfig()
    {
        var store = new JsonConfigStore(_configPath);
        AppConfig? received = null;
        store.ConfigChanged += (_, cfg) => received = cfg;

        var config = new AppConfig { Language = "en" };
        store.Save(config);

        received.Should().NotBeNull();
        received!.Language.Should().Be("en");
    }

    [Fact]
    public void Save_WhenNoSubscribers_DoesNotThrow()
    {
        var store = new JsonConfigStore(_configPath);
        var act = () => store.Save(new AppConfig());
        act.Should().NotThrow();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppConfig BuildFullConfig() => new()
    {
        SchemaVersion = 2,
        SttProvider = SttProviderType.OpenAI,
        SttConfig = new SttProviderConfig
        {
            Url = "https://api.openai.com/v1",
            ApiKey = "sk-test-stt-key",
            Model = "whisper-1",
        },
        AudioDeviceId = "device-guid-1234",
        ActivationMode = ActivationMode.HotkeyOnly,
        Hotkey = new HotkeyConfig { Modifiers = "Ctrl+Shift", Key = "R" },
        RecordingMode = RecordingMode.VadAutoStop,
        AnimationStyle = AnimationStyle.WaveformBars,
        LlmPostProcessing = new LlmPostProcessingConfig
        {
            Enabled = true,
            ProviderType = "OpenAI",
            Url = "https://api.openai.com/v1",
            ApiKey = "sk-test-llm-key",
            Model = "gpt-4o",
            Mode = LlmPostProcessingMode.RemoveFillers,
        },
        RunAtStartup = true,
        Language = "en",
    };
}
