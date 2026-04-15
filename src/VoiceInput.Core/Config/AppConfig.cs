namespace VoiceInput.Core.Config;

public class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public SttProviderType SttProvider { get; set; } = SttProviderType.FasterWhisper;
    public SttProviderConfig SttConfig { get; set; } = new();
    public string AudioDeviceId { get; set; } = "";
    public ActivationMode ActivationMode { get; set; } = ActivationMode.Both;
    public HotkeyConfig Hotkey { get; set; } = new();
    public RecordingMode RecordingMode { get; set; } = RecordingMode.PushToTalk;
    public AnimationStyle AnimationStyle { get; set; } = AnimationStyle.GradientOrb;
    public LlmPostProcessingConfig LlmPostProcessing { get; set; } = new();
    public bool RunAtStartup { get; set; } = false;
    public string Language { get; set; } = "ru";

    /// <summary>
    /// Silence timeout in milliseconds before VAD auto-stops recording.
    /// Default 3000ms (3 seconds). Increase for longer pauses between sentences.
    /// </summary>
    /// <summary>0 = disabled (push-to-talk only). 1000-10000 = auto-stop after silence. Default: 5000ms.</summary>
    public int SilenceTimeoutMs { get; set; } = 5000;
}
