using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows;
using H.NotifyIcon;

namespace VoiceInput.App.Tray;

/// <summary>
/// System tray icon with context menu for the Aether Voice service.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private bool _isPaused;
    private bool _disposed;

    /// <summary>Fires when user clicks "Settings" in tray menu.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Fires when user clicks "Exit" in tray menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Fires when pause state changes.</summary>
    public event EventHandler<bool>? PauseToggled;

    /// <summary>Whether the service is currently paused.</summary>
    public bool IsPaused => _isPaused;

    public void Initialize()
    {
        var icon = LoadEmbeddedIcon() ?? CreateDefaultIcon();
        var contextMenu = BuildContextMenu();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Aether Voice — Active",
            ContextMenu = contextMenu,
            Icon = icon,
            Visibility = Visibility.Visible,
        };

        // Force the tray icon to appear immediately
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);

        // Ensure context menu shows on right-click and left double-click opens settings
        _trayIcon.TrayRightMouseUp += (_, _) =>
        {
            contextMenu.IsOpen = true;
        };

        _trayIcon.TrayLeftMouseUp += (_, _) =>
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        };

        // Show toast notification so the user knows the tray app is running
        ShowStartupToast();
    }

    /// <summary>
    /// Shows a small WPF toast window in the bottom-right corner to inform the user.
    /// Auto-hides after 4 seconds.
    /// </summary>
    private static void ShowStartupToast()
    {
        var toast = new Window
        {
            Title = "Aether Voice",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0xEE, 0x1E, 0x1E, 0x2E)),
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Width = 300,
            Height = 70,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        // Position in bottom-right corner
        toast.Left = SystemParameters.WorkArea.Right - toast.Width - 12;
        toast.Top = SystemParameters.WorkArea.Bottom - toast.Height - 12;

        var panel = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(16, 12, 16, 12)
        };

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Aether Voice — Active",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold
        });

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Press Ctrl+Shift+Space to record",
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xA0, 0xA0, 0xB0)),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        });

        var border = new System.Windows.Controls.Border
        {
            CornerRadius = new CornerRadius(12),
            Background = toast.Background,
            Child = panel
        };

        toast.Content = border;
        toast.Show();

        // Auto-hide after 4 seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            toast.Close();
        };
        timer.Start();
    }

    /// <summary>Updates tray icon to indicate recording state.</summary>
    public void SetRecording(bool isRecording)
    {
        if (_trayIcon == null) return;
        _trayIcon.ToolTipText = isRecording ? "Aether Voice — Recording..." : (_isPaused ? "Aether Voice — Paused" : "Aether Voice — Active");
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        var pauseItem = new System.Windows.Controls.MenuItem { Header = "Pause" };
        pauseItem.Click += (_, _) =>
        {
            _isPaused = !_isPaused;
            pauseItem.Header = _isPaused ? "Resume" : "Pause";
            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = _isPaused ? "Aether Voice — Paused" : "Aether Voice — Active";
            }
            PauseToggled?.Invoke(this, _isPaused);
        };
        menu.Items.Add(pauseItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    private static Icon? LoadEmbeddedIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri);
            if (stream?.Stream != null)
            {
                return new Icon(stream.Stream);
            }
        }
        catch
        {
            // Fall through to programmatic icon
        }

        return null;
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 icon with a microphone-like shape
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        // Draw mic body
        using var brush = new SolidBrush(Color.FromArgb(0x63, 0x66, 0xF1));
        g.FillEllipse(brush, 5, 2, 6, 8);

        // Draw mic stand
        using var pen = new Pen(Color.FromArgb(0x63, 0x66, 0xF1), 1.5f);
        g.DrawArc(pen, 3, 6, 10, 8, 0, 180);
        g.DrawLine(pen, 8, 14, 8, 10);

        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _trayIcon?.Dispose();
    }
}
