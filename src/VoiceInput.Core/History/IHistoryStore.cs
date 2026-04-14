namespace VoiceInput.Core.History;

public interface IHistoryStore
{
    Task AddEntryAsync(HistoryEntry entry);
    Task<List<HistoryEntry>> GetEntriesAsync(DateTime? from = null, DateTime? to = null, int limit = 50);
    Task<HistoryStats> GetStatsAsync(DateTime? from = null, DateTime? to = null);
    Task ClearAsync();
}
