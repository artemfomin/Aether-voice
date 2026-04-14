namespace VoiceInput.Core.Config;

public class LlmPostProcessingConfig
{
    public bool Enabled { get; set; } = false;
    public string ProviderType { get; set; } = "";
    public string Url { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public LlmPostProcessingMode Mode { get; set; } = LlmPostProcessingMode.GrammarFix;
}
