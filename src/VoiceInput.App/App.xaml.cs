using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using VoiceInput.App.Native;
using VoiceInput.App.Startup;
using VoiceInput.Core;
using VoiceInput.Core.Logging;

namespace VoiceInput.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private SingleInstanceGuard? _guard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance check — exit immediately if another instance is running.
        _guard = new SingleInstanceGuard();
        if (!_guard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        // DI Container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Reduce process priority and power consumption on Windows 11.
        SetEfficiencyMode();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        var logDir = LoggingSetup.GetDefaultLogDirectory();
        Directory.CreateDirectory(logDir);
        var serilogLogger = LoggingSetup.CreateLogger(logDir);

        services.AddSingleton(serilogLogger);
        services.AddLogging(builder => builder.AddSerilog(serilogLogger, dispose: false));

        // Core services
        services.AddVoiceInputCore();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (_serviceProvider as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Enables Windows 11 Efficiency Mode for this process via SetProcessInformation.
    /// Lowers scheduling priority and enables power throttling for background tray apps.
    /// Silently ignored on older Windows versions that do not support this API.
    /// </summary>
    private static void SetEfficiencyMode()
    {
        try
        {
            NativeMethods.EnableEfficiencyMode();
        }
        catch (Exception)
        {
            // Non-critical: older Windows versions may not support this API.
        }
    }
}
