using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VoiceInput.App.Settings;

/// <summary>
/// Live audio level meter control for the Audio settings tab.
/// Horizontal bar that shows real-time RMS amplitude with green→yellow→red gradient.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AudioLevelMeter : UserControl
{
    private readonly Rectangle _levelBar;
    private readonly Rectangle _background;
    private float _currentLevel;

    public AudioLevelMeter()
    {
        Width = 200;
        Height = 12;

        var canvas = new Canvas { Width = 200, Height = 12 };

        _background = new Rectangle
        {
            Width = 200, Height = 12,
            Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
            RadiusX = 6, RadiusY = 6
        };
        canvas.Children.Add(_background);

        _levelBar = new Rectangle
        {
            Width = 0, Height = 12,
            Fill = new LinearGradientBrush(
                Colors.Green, Colors.Red,
                new Point(0, 0), new Point(1, 0)),
            RadiusX = 6, RadiusY = 6
        };
        canvas.Children.Add(_levelBar);

        Content = canvas;
    }

    /// <summary>
    /// Update the level meter with a normalized amplitude [0.0, 1.0].
    /// Call from UI thread.
    /// </summary>
    public void UpdateLevel(float amplitude)
    {
        _currentLevel += (amplitude - _currentLevel) * 0.3f;
        _levelBar.Width = Math.Clamp(_currentLevel * 200, 0, 200);
    }

    /// <summary>Reset the level to zero.</summary>
    public void Reset()
    {
        _currentLevel = 0;
        _levelBar.Width = 0;
    }
}
