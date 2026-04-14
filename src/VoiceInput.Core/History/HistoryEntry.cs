namespace VoiceInput.Core.History;

public sealed class HistoryEntry
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public int DurationMs { get; set; }
    public int CharCount { get; set; }
    public int WordCount { get; set; }
    public string SttProvider { get; set; } = "";
    public string SttModel { get; set; } = "";
    public string TargetApp { get; set; } = "";
    public string TargetAppTitle { get; set; } = "";
    public string Language { get; set; } = "";
    public int LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
