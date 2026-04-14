using Microsoft.Data.Sqlite;

namespace VoiceInput.Core.History;

public sealed class SqliteHistoryStore : IHistoryStore
{
    private const int SchemaVersion = 1;

    private readonly string _dbPath;
    private bool _initialized;

    public SqliteHistoryStore(string? dbPath = null)
    {
        _dbPath = dbPath ?? DefaultDbPath();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task AddEntryAsync(HistoryEntry entry)
    {
        await EnsureCreatedAsync().ConfigureAwait(false);

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO transcriptions
                (text, duration_ms, char_count, word_count, stt_provider, stt_model,
                 target_app, target_app_title, language, latency_ms, created_at)
            VALUES
                ($text, $duration_ms, $char_count, $word_count, $stt_provider, $stt_model,
                 $target_app, $target_app_title, $language, $latency_ms, $created_at)
            """;

        cmd.Parameters.AddWithValue("$text", entry.Text);
        cmd.Parameters.AddWithValue("$duration_ms", entry.DurationMs);
        cmd.Parameters.AddWithValue("$char_count", entry.CharCount);
        cmd.Parameters.AddWithValue("$word_count", entry.WordCount);
        cmd.Parameters.AddWithValue("$stt_provider", entry.SttProvider);
        cmd.Parameters.AddWithValue("$stt_model", entry.SttModel);
        cmd.Parameters.AddWithValue("$target_app", entry.TargetApp);
        cmd.Parameters.AddWithValue("$target_app_title", entry.TargetAppTitle);
        cmd.Parameters.AddWithValue("$language", entry.Language);
        cmd.Parameters.AddWithValue("$latency_ms", entry.LatencyMs);
        cmd.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<List<HistoryEntry>> GetEntriesAsync(
        DateTime? from = null, DateTime? to = null, int limit = 50)
    {
        await EnsureCreatedAsync().ConfigureAwait(false);

        using var connection = OpenConnection();
        using var cmd = BuildSelectCommand(connection, from, to, limit);

        var entries = new List<HistoryEntry>();
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
            entries.Add(MapEntry(reader));

        return entries;
    }

    public async Task<HistoryStats> GetStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        await EnsureCreatedAsync().ConfigureAwait(false);

        using var connection = OpenConnection();
        using var cmd = BuildStatsCommand(connection, from, to);
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

        if (!await reader.ReadAsync().ConfigureAwait(false))
            return new HistoryStats();

        return new HistoryStats
        {
            TotalEntries      = reader.GetInt32(0),
            TotalDurationMs   = await reader.IsDBNullAsync(1).ConfigureAwait(false) ? 0L : reader.GetInt64(1),
            TotalCharCount    = await reader.IsDBNullAsync(2).ConfigureAwait(false) ? 0L : reader.GetInt64(2),
            AverageDurationMs = await reader.IsDBNullAsync(3).ConfigureAwait(false) ? 0.0 : reader.GetDouble(3),
            AverageLatencyMs  = await reader.IsDBNullAsync(4).ConfigureAwait(false) ? 0.0 : reader.GetDouble(4),
        };
    }

    public async Task ClearAsync()
    {
        await EnsureCreatedAsync().ConfigureAwait(false);

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM transcriptions";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private async Task EnsureCreatedAsync()
    {
        if (_initialized) return;

        EnsureDirectoryExists();

        using var connection = OpenConnection();
        await CreateTablesAsync(connection).ConfigureAwait(false);
        await EnsureSchemaVersionAsync(connection).ConfigureAwait(false);

        _initialized = true;
    }

    private static async Task CreateTablesAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS transcriptions (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                text             TEXT    NOT NULL,
                duration_ms      INTEGER,
                char_count       INTEGER,
                word_count       INTEGER,
                stt_provider     TEXT,
                stt_model        TEXT,
                target_app       TEXT,
                target_app_title TEXT,
                language         TEXT,
                latency_ms       INTEGER,
                created_at       TEXT    NOT NULL
            );
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER
            );
            """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task EnsureSchemaVersionAsync(SqliteConnection connection)
    {
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM schema_version";
        var count = (long)(await checkCmd.ExecuteScalarAsync().ConfigureAwait(false))!;

        if (count > 0) return;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO schema_version (version) VALUES ($version)";
        insertCmd.Parameters.AddWithValue("$version", SchemaVersion);
        await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // ── Query builders ────────────────────────────────────────────────────────

    private static SqliteCommand BuildSelectCommand(
        SqliteConnection connection, DateTime? from, DateTime? to, int limit)
    {
        var cmd = connection.CreateCommand();
        var where = BuildDateWhereClause(cmd, from, to);

        cmd.CommandText = $"""
            SELECT id, text, duration_ms, char_count, word_count,
                   stt_provider, stt_model, target_app, target_app_title,
                   language, latency_ms, created_at
            FROM transcriptions
            {where}
            ORDER BY created_at DESC
            LIMIT $limit
            """;

        cmd.Parameters.AddWithValue("$limit", limit);
        return cmd;
    }

    private static SqliteCommand BuildStatsCommand(
        SqliteConnection connection, DateTime? from, DateTime? to)
    {
        var cmd = connection.CreateCommand();
        var where = BuildDateWhereClause(cmd, from, to);

        cmd.CommandText = $"""
            SELECT COUNT(*),
                   SUM(duration_ms),
                   SUM(char_count),
                   AVG(duration_ms),
                   AVG(latency_ms)
            FROM transcriptions
            {where}
            """;

        return cmd;
    }

    private static string BuildDateWhereClause(SqliteCommand cmd, DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return "";

        var clauses = new List<string>();

        if (from is not null)
        {
            clauses.Add("created_at >= $from");
            cmd.Parameters.AddWithValue("$from", from.Value.ToString("O"));
        }

        if (to is not null)
        {
            clauses.Add("created_at <= $to");
            cmd.Parameters.AddWithValue("$to", to.Value.ToString("O"));
        }

        return "WHERE " + string.Join(" AND ", clauses);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static HistoryEntry MapEntry(SqliteDataReader reader) => new()
    {
        Id             = reader.GetInt32(0),
        Text           = reader.GetString(1),
        DurationMs     = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
        CharCount      = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
        WordCount      = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
        SttProvider    = reader.IsDBNull(5) ? "" : reader.GetString(5),
        SttModel       = reader.IsDBNull(6) ? "" : reader.GetString(6),
        TargetApp      = reader.IsDBNull(7) ? "" : reader.GetString(7),
        TargetAppTitle = reader.IsDBNull(8) ? "" : reader.GetString(8),
        Language       = reader.IsDBNull(9) ? "" : reader.GetString(9),
        LatencyMs      = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
        CreatedAt      = DateTime.Parse(reader.GetString(11), null,
                             System.Globalization.DateTimeStyles.RoundtripKind),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        return connection;
    }

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static string DefaultDbPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceInput",
            "history.db");
}
