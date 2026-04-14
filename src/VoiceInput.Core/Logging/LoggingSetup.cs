using Serilog;

namespace VoiceInput.Core.Logging;

/// <summary>
/// Static helper to configure and create a Serilog logger instance.
/// Keeps logging configuration centralized and testable.
/// </summary>
public static class LoggingSetup
{
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    private const int RetainedFileDays = 7;

    /// <summary>
    /// Creates a configured Serilog <see cref="ILogger"/> that writes rolling daily log files
    /// to the specified directory.
    /// </summary>
    /// <param name="logDirectory">Directory where log files will be written.</param>
    /// <returns>A fully configured <see cref="ILogger"/> instance.</returns>
    public static ILogger CreateLogger(string logDirectory)
    {
        var logFilePath = Path.Combine(logDirectory, "voiceinput-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileDays,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    /// <summary>
    /// Returns the default log directory under the user's AppData/Roaming folder.
    /// </summary>
    public static string GetDefaultLogDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceInput",
            "logs");
}
