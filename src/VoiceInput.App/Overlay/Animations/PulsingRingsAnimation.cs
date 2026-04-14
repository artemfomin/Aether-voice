using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VoiceInput.App.Overlay.Animations;

/// <summary>
/// 3 concentric pulsing rings that respond to audio amplitude. Siri/Cortana style.
/// </summary>
public sealed class PulsingRingsAnimation : IRecordingAnimation
{
    private readonly Canvas _container;
    private readonly Ellipse[] _rings;
    private bool _running;
    private float _amplitude;
    private float _targetAmplitude;
    private double _phase;

    private static readonly Color[] Colors =
    [
        Color.FromRgb(0x63, 0x66, 0xF1),
        Color.FromRgb(0x8B, 0x5C, 0xF6),
        Color.FromRgb(0xEC, 0x48, 0x99),
    ];

    public PulsingRingsAnimation()
    {
        _container = new Canvas { Width = 48, Height = 48 };
        _rings = new Ellipse[3];
        double[] radii = [16, 24, 32];

        for (int i = 0; i < 3; i++)
        {
            _rings[i] = new Ellipse
            {
                Width = radii[i], Height = radii[i],
                Stroke = new SolidColorBrush(Colors[i]),
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                Opacity = 1.0 - i * 0.25,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };
            Canvas.SetLeft(_rings[i], (48 - radii[i]) / 2);
            Canvas.SetTop(_rings[i], (48 - radii[i]) / 2);
            _container.Children.Add(_rings[i]);
        }
    }

    public UIElement Visual => _container;

    public void Start() { if (_running) return; _running = true; CompositionTarget.Rendering += OnRender; }
    public void Stop() { _running = false; CompositionTarget.Rendering -= OnRender; }
    public void UpdateAmplitude(float amplitude) => _targetAmplitude = Math.Clamp(amplitude, 0f, 1f);

    private void OnRender(object? sender, EventArgs e)
    {
        _amplitude += (_targetAmplitude - _amplitude) * 0.12f;
        _phase += 0.04;

        for (int i = 0; i < 3; i++)
        {
            double offset = i * Math.PI * 2 / 3; // 120° staggered
            double pulse = (Math.Sin(_phase + offset) + 1) * 0.5;
            double scale = 1.0 + (_amplitude * 0.2 * (i + 1)) + pulse * 0.05;
            double opacity = (1.0 - i * 0.2) * (0.4 + _amplitude * 0.6);

            var st = (ScaleTransform)_rings[i].RenderTransform;
            st.ScaleX = scale;
            st.ScaleY = scale;
            _rings[i].Opacity = Math.Clamp(opacity, 0.1, 1.0);
        }
    }
}
