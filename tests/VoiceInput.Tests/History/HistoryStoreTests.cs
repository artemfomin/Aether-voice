using FluentAssertions;
using Microsoft.Data.Sqlite;
using VoiceInput.Core.History;
using Xunit;

namespace VoiceInput.Tests.History;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteHistoryStore _store;

    public HistoryStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"VoiceInputHistoryTests_{Guid.NewGuid():N}.db");
        _store = new SqliteHistoryStore(_dbPath);
    }

    public void Dispose()
    {
        // Clear connection pool so SQLite releases the file handle before deletion.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── Add + Get round-trip ──────────────────────────────────────────────────

    [Fact]
    public async Task AddEntry_ThenGetEntries_ReturnsMatchingData()
    {
        var entry = BuildEntry(text: "Hello world", durationMs: 1200, latencyMs: 80);

        await _store.AddEntryAsync(entry);
        var results = await _store.GetEntriesAsync();

        results.Should().HaveCount(1);
        var saved = results[0];
        saved.Text.Should().Be("Hello world");
        saved.DurationMs.Should().Be(1200);
        saved.CharCount.Should().Be(entry.CharCount);
        saved.WordCount.Should().Be(entry.WordCount);
        saved.SttProvider.Should().Be(entry.SttProvider);
        saved.SttModel.Should().Be(entry.SttModel);
        saved.TargetApp.Should().Be(entry.TargetApp);
        saved.TargetAppTitle.Should().Be(entry.TargetAppTitle);
        saved.Language.Should().Be(entry.Language);
        saved.LatencyMs.Should().Be(80);
        saved.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddEntry_PreservesCreatedAtUtc()
    {
        var now = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var entry = BuildEntry();
        entry.CreatedAt = now;

        await _store.AddEntryAsync(entry);
        var results = await _store.GetEntriesAsync();

        results[0].CreatedAt.Should().Be(now);
        results[0].CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── Date range filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetEntries_WithFromFilter_ExcludesOlderEntries()
    {
        var old = BuildEntry(text: "old");
        old.CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var recent = BuildEntry(text: "recent");
        recent.CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _store.AddEntryAsync(old);
        await _store.AddEntryAsync(recent);

        var cutoff = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var results = await _store.GetEntriesAsync(from: cutoff);

        results.Should().HaveCount(1);
        results[0].Text.Should().Be("recent");
    }

    [Fact]
    public async Task GetEntries_WithToFilter_ExcludesNewerEntries()
    {
        var old = BuildEntry(text: "old");
        old.CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var recent = BuildEntry(text: "recent");
        recent.CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _store.AddEntryAsync(old);
        await _store.AddEntryAsync(recent);

        var cutoff = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var results = await _store.GetEntriesAsync(to: cutoff);

        results.Should().HaveCount(1);
        results[0].Text.Should().Be("old");
    }

    [Fact]
    public async Task GetEntries_WithFromAndToFilter_ReturnsOnlyMatchingRange()
    {
        var jan = BuildEntry(text: "jan");
        jan.CreatedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var mar = BuildEntry(text: "mar");
        mar.CreatedAt = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var jun = BuildEntry(text: "jun");
        jun.CreatedAt = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        await _store.AddEntryAsync(jan);
        await _store.AddEntryAsync(mar);
        await _store.AddEntryAsync(jun);

        var from = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var results = await _store.GetEntriesAsync(from: from, to: to);

        results.Should().HaveCount(1);
        results[0].Text.Should().Be("mar");
    }

    // ── Limit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEntries_WithLimit_ReturnsAtMostLimitEntries()
    {
        for (var i = 0; i < 10; i++)
            await _store.AddEntryAsync(BuildEntry(text: $"entry {i}"));

        var results = await _store.GetEntriesAsync(limit: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetEntries_OrderedByCreatedAtDescending()
    {
        var first = BuildEntry(text: "first");
        first.CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var second = BuildEntry(text: "second");
        second.CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _store.AddEntryAsync(first);
        await _store.AddEntryAsync(second);

        var results = await _store.GetEntriesAsync(limit: 10);

        results[0].Text.Should().Be("second");
        results[1].Text.Should().Be("first");
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_ReturnsCorrectAggregates()
    {
        await _store.AddEntryAsync(BuildEntry(durationMs: 1000, charCount: 50, latencyMs: 100));
        await _store.AddEntryAsync(BuildEntry(durationMs: 3000, charCount: 150, latencyMs: 200));

        var stats = await _store.GetStatsAsync();

        stats.TotalEntries.Should().Be(2);
        stats.TotalDurationMs.Should().Be(4000);
        stats.TotalCharCount.Should().Be(200);
        stats.AverageDurationMs.Should().BeApproximately(2000.0, 0.01);
        stats.AverageLatencyMs.Should().BeApproximately(150.0, 0.01);
    }

    [Fact]
    public async Task GetStats_WithDateRange_FiltersCorrectly()
    {
        var old = BuildEntry(durationMs: 500, latencyMs: 50);
        old.CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var recent = BuildEntry(durationMs: 1500, latencyMs: 150);
        recent.CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _store.AddEntryAsync(old);
        await _store.AddEntryAsync(recent);

        var from = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var stats = await _store.GetStatsAsync(from: from);

        stats.TotalEntries.Should().Be(1);
        stats.TotalDurationMs.Should().Be(1500);
        stats.AverageLatencyMs.Should().BeApproximately(150.0, 0.01);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clear_RemovesAllEntries()
    {
        await _store.AddEntryAsync(BuildEntry());
        await _store.AddEntryAsync(BuildEntry());

        await _store.ClearAsync();
        var results = await _store.GetEntriesAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Clear_ThenAdd_WorksCorrectly()
    {
        await _store.AddEntryAsync(BuildEntry(text: "before clear"));
        await _store.ClearAsync();
        await _store.AddEntryAsync(BuildEntry(text: "after clear"));

        var results = await _store.GetEntriesAsync();

        results.Should().HaveCount(1);
        results[0].Text.Should().Be("after clear");
    }

    // ── Empty database ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEntries_EmptyDatabase_ReturnsEmptyList()
    {
        var results = await _store.GetEntriesAsync();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStats_EmptyDatabase_ReturnsZeroStats()
    {
        var stats = await _store.GetStatsAsync();

        stats.TotalEntries.Should().Be(0);
        stats.TotalDurationMs.Should().Be(0);
        stats.TotalCharCount.Should().Be(0);
        stats.AverageDurationMs.Should().Be(0.0);
        stats.AverageLatencyMs.Should().Be(0.0);
    }

    // ── Schema version ────────────────────────────────────────────────────────

    [Fact]
    public async Task SchemaVersion_IsInsertedOnFirstUse()
    {
        // Trigger initialization
        await _store.GetEntriesAsync();

        // Verify schema_version table has version 1
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM schema_version";
        var version = (long)(await cmd.ExecuteScalarAsync())!;

        version.Should().Be(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HistoryEntry BuildEntry(
        string text = "Test transcription",
        int durationMs = 2000,
        int charCount = 18,
        int latencyMs = 120) => new()
    {
        Text           = text,
        DurationMs     = durationMs,
        CharCount      = charCount,
        WordCount      = 2,
        SttProvider    = "FasterWhisper",
        SttModel       = "large-v3",
        TargetApp      = "notepad.exe",
        TargetAppTitle = "Untitled - Notepad",
        Language       = "en",
        LatencyMs      = latencyMs,
        CreatedAt      = DateTime.UtcNow,
    };
}
