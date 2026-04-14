using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
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

    /// <summary>Shows the island with a slide-in animation.</summary>
    public void ShowAnimated()
    {
        Opacity = 0;
        Show();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var transform = new TranslateTransform(0, -5);
        RenderTransform = transform;
        var slideIn = new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fadeIn);
        transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
    }

    /// <summary>Hides the island with a slide-out animation.</summary>
    public void HideAnimated()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => Hide();

        var transform = RenderTransform as TranslateTransform ?? new TranslateTransform(0, 0);
        RenderTransform = transform;
        var slideOut = new DoubleAnimation(0, 5, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        BeginAnimation(OpacityProperty, fadeOut);
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
    }
}
