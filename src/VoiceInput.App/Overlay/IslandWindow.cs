using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VoiceInput.App.Overlay.Animations;

namespace VoiceInput.App.Overlay;

/// <summary>
/// Floating island overlay window — transparent, topmost, non-activating pill-shaped overlay.
/// Hosts a recording animation and status text.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IslandWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int GwlExstyle = -20;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    private const double BottomMargin = 60;
    private static readonly string PositionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceInput", "overlay-position.json");

    private readonly Border _pill;
    private readonly TextBlock _statusText;
    private readonly ContentControl _animationHost;
    private IRecordingAnimation? _currentAnimation;

    public IslandWindow()
    {
        // Window properties — transparent, topmost, no taskbar, no chrome
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 220;
        Height = 64;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Status text
        _statusText = new TextBlock
        {
            Text = "Ready",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        // Animation host
        _animationHost = new ContentControl
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Horizontal layout: animation + text
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _animationHost, _statusText }
        };

        // Dark pill shape
        _pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1E, 0x1E, 0x2E)),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16, 10, 16, 10),
            Child = stack,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Content = _pill;

        _pill.MouseLeftButtonDown += OnPillMouseLeftButtonDown;

        Loaded += OnLoaded;
    }

    /// <summary>Current status text displayed in the island.</summary>
    public string StatusText
    {
        get => _statusText.Text;
        set => _statusText.Text = value;
    }

    /// <summary>Sets the recording animation to display.</summary>
    public void SetAnimation(IRecordingAnimation? animation)
    {
        _currentAnimation?.Stop();
        _currentAnimation = animation;
        _animationHost.Content = animation?.Visual;
    }

    /// <summary>Gets the current animation.</summary>
    public IRecordingAnimation? CurrentAnimation => _currentAnimation;

    /// <summary>Shows the island with a fade-in + slide-in animation.</summary>
    public void ShowAnimated()
    {
        Opacity = 0;
        Show();

        // Fade in the window
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        // Slide the pill content (RenderTransform on Window is forbidden in WPF)
        var transform = new TranslateTransform(0, -5);
        _pill.RenderTransform = transform;
        var slideIn = new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
    }

    /// <summary>Hides the island with a fade-out + slide-out animation.</summary>
    public void HideAnimated()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => Hide();
        BeginAnimation(OpacityProperty, fadeOut);

        // Slide the pill content down
        var transform = _pill.RenderTransform as TranslateTransform ?? new TranslateTransform(0, 0);
        _pill.RenderTransform = transform;
        var slideOut = new DoubleAnimation(0, 5, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        transform.BeginAnimation(TranslateTransform.YProperty, slideOut);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW — don't steal focus, don't show in Alt+Tab
        int exStyle = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, exStyle | WsExNoactivate | WsExToolwindow);

        // DWM rounded corners
        int pref = DwmwcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref pref, sizeof(int));

        // Restore saved position or use default bottom-center
        ApplySavedOrDefaultPosition();

        // Persist position when user drags or focus detector repositions
        LocationChanged += OnLocationChanged;
    }

    /// <summary>Handles mouse drag on the pill to move the window.</summary>
    private void OnPillMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* mouse not captured */ }
        }
    }

    /// <summary>Applies the last saved position, or falls back to default bottom-center.</summary>
    private void ApplySavedOrDefaultPosition()
    {
        var saved = LoadPosition();
        if (saved.HasValue)
        {
            Left = saved.Value.X;
            Top = saved.Value.Y;
        }
        else
        {
            SetDefaultPosition();
        }
    }

    /// <summary>Sets the window to bottom-center of primary screen.</summary>
    private void SetDefaultPosition()
    {
        Left = (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Bottom - Height - BottomMargin;
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (Left > 0 || Top > 0)
            SavePosition();
    }

    /// <summary>Reads the saved overlay position from disk. Returns null if unavailable.</summary>
    private static Point? LoadPosition()
    {
        try
        {
            if (!File.Exists(PositionFilePath)) return null;
            var json = File.ReadAllText(PositionFilePath);
            var data = JsonSerializer.Deserialize<OverlayPositionData>(json);
            return data is null ? null : new Point(data.X, data.Y);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Persists the current overlay position to disk.</summary>
    private void SavePosition()
    {
        try
        {
            var dir = Path.GetDirectoryName(PositionFilePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var data = new OverlayPositionData(Left, Top);
            var json = JsonSerializer.Serialize(data);
            File.WriteAllText(PositionFilePath, json);
        }
        catch
        {
            // Non-critical — position will use default next launch
        }
    }

    /// <summary>Serializable overlay position record.</summary>
    private sealed record OverlayPositionData(double X, double Y);
}
