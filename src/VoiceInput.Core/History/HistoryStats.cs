namespace VoiceInput.Core.History;

public sealed class HistoryStats
{
    public int TotalEntries { get; init; }
    public long TotalDurationMs { get; init; }
    public long TotalCharCount { get; init; }
    public double AverageDurationMs { get; init; }
    public double AverageLatencyMs { get; init; }
}
