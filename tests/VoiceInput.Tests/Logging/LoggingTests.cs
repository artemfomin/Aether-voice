using FluentAssertions;
using Serilog;
using VoiceInput.Core.Logging;
using Xunit;

namespace VoiceInput.Tests.Logging;

/// <summary>
/// Tests for <see cref="LoggingSetup"/> — verifies logger creation, file output,
/// and log format correctness.
/// </summary>
public sealed class LoggingTests : IDisposable
{
    private readonly string _tempLogDirectory;

    public LoggingTests()
    {
        _tempLogDirectory = Path.Combine(Path.GetTempPath(), $"VoiceInputTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempLogDirectory);
    }

    public void Dispose()
    {
        // Allow Serilog to flush and release file handles before cleanup
        Log.CloseAndFlush();

        try
        {
            Directory.Delete(_tempLogDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; test runner may still hold a handle
        }
    }

    [Fact]
    public void CreateLogger_WithValidDirectory_ReturnsNonNullLogger()
    {
        // Act
        var logger = LoggingSetup.CreateLogger(_tempLogDirectory);

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void CreateLogger_AfterWritingMessage_CreatesLogFile()
    {
        // Arrange
        var logger = LoggingSetup.CreateLogger(_tempLogDirectory);

        // Act
        logger.Information("Test message written by {Test}", nameof(CreateLogger_AfterWritingMessage_CreatesLogFile));
        (logger as IDisposable)?.Dispose();

        // Assert
        var logFiles = Directory.GetFiles(_tempLogDirectory, "voiceinput-*.log");
        logFiles.Should().NotBeEmpty("a rolling log file should be created after writing a message");
    }

    [Fact]
    public void CreateLogger_LogFileContent_MatchesExpectedFormat()
    {
        // Arrange
        const string testMessage = "Format verification message";
        var logger = LoggingSetup.CreateLogger(_tempLogDirectory);

        // Act
        logger.Information(testMessage);
        (logger as IDisposable)?.Dispose();

        // Assert
        var logFile = Directory.GetFiles(_tempLogDirectory, "voiceinput-*.log").Single();
        var content = File.ReadAllText(logFile);

        content.Should().Contain(testMessage, "the written message should appear in the log file");
        content.Should().MatchRegex(
            @"\[\d{2}:\d{2}:\d{2} INF\]",
            "log entries must follow the [HH:mm:ss LVL] format");
    }

    [Fact]
    public void GetDefaultLogDirectory_ReturnsPathUnderAppData()
    {
        // Act
        var defaultDir = LoggingSetup.GetDefaultLogDirectory();

        // Assert
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        defaultDir.Should().StartWith(appData, "default log directory must be under AppData/Roaming");
        defaultDir.Should().EndWith(Path.Combine("VoiceInput", "logs"));
    }
}
