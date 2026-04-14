using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows;
using H.NotifyIcon;

namespace VoiceInput.App.Tray;

/// <summary>
/// System tray icon with context menu for the Voice Input service.
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
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Voice Input — Active",
            ContextMenu = BuildContextMenu(),
        };

        // Generate a simple icon programmatically (microphone-like)
        _trayIcon.Icon = CreateDefaultIcon();
    }

    /// <summary>Updates tray icon to indicate recording state.</summary>
    public void SetRecording(bool isRecording)
    {
        if (_trayIcon == null) return;
        _trayIcon.ToolTipText = isRecording ? "Voice Input — Recording..." : (_isPaused ? "Voice Input — Paused" : "Voice Input — Active");
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
                _trayIcon.ToolTipText = _isPaused ? "Voice Input — Paused" : "Voice Input — Active";
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
