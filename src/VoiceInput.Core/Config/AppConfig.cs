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
}
