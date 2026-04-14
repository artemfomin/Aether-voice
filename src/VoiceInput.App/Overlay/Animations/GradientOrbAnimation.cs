using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace VoiceInput.App.Overlay.Animations;

/// <summary>
/// Audio-reactive gradient orb animation. Default recording indicator.
/// Pulsing radial gradient ellipse with glow effect that responds to voice amplitude.
/// </summary>
public sealed class GradientOrbAnimation : IRecordingAnimation
{
    private readonly Grid _container;
    private readonly Ellipse _orb;
    private readonly ScaleTransform _scale;
    private readonly DropShadowEffect _glow;

    private bool _running;
    private float _currentAmplitude;
    private float _targetAmplitude;
    private double _idlePhase;

    private const double LerpFactor = 0.15;
    private const double IdleSpeed = 0.03; // radians per frame for breathing

    public GradientOrbAnimation()
    {
        _scale = new ScaleTransform(1.0, 1.0);

        _glow = new DropShadowEffect
        {
            Color = Color.FromRgb(0x63, 0x66, 0xF1),
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.8
        };

        _orb = new Ellipse
        {
            Width = 44,
            Height = 44,
            Fill = new RadialGradientBrush(new GradientStopCollection
            {
                new(Color.FromRgb(0x63, 0x66, 0xF1), 0.0),
                new(Color.FromRgb(0x8B, 0x5C, 0xF6), 0.5),
                new(Color.FromRgb(0xEC, 0x48, 0x99), 1.0),
            }),
            Effect = _glow,
            Opacity = 0.85,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _scale,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _container = new Grid
        {
            Width = 48,
            Height = 48,
            Children = { _orb }
        };
    }

    public UIElement Visual => _container;

    public void Start()
    {
        if (_running) return;
        _running = true;
        CompositionTarget.Rendering += OnRender;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRender;

        // Reset to idle
        _scale.ScaleX = 1.0;
        _scale.ScaleY = 1.0;
        _orb.Opacity = 0.85;
        _glow.BlurRadius = 10;
    }

    public void UpdateAmplitude(float amplitude)
    {
        _targetAmplitude = Math.Clamp(amplitude, 0f, 1f);
    }

    private void OnRender(object? sender, EventArgs e)
    {
        // Smooth amplitude
        _currentAmplitude += (_targetAmplitude - _currentAmplitude) * (float)LerpFactor;

        // Idle breathing
        _idlePhase += IdleSpeed;
        double idleBreath = (Math.Sin(_idlePhase) + 1.0) * 0.5; // 0..1

        // Combine idle breathing with amplitude
        double amp = Math.Max(_currentAmplitude, idleBreath * 0.15);

        double scale = 1.0 + amp * 0.3;
        double opacity = 0.7 + amp * 0.3;
        double blur = 5 + amp * 20;

        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _orb.Opacity = opacity;
        _glow.BlurRadius = blur;
    }
}
