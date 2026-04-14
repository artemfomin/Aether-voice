namespace VoiceInput.Core.Config;

public interface IConfigStore
{
    AppConfig Load();
    void Save(AppConfig config);
    event EventHandler<AppConfig>? ConfigChanged;
}
