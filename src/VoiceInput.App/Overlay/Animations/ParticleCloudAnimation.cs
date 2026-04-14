using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VoiceInput.App.Overlay.Animations;

/// <summary>
/// Particle cloud animation — swarm of dots reacting to voice amplitude.
/// </summary>
public sealed class ParticleCloudAnimation : IRecordingAnimation
{
    private const int ParticleCount = 30;
    private readonly Canvas _container;
    private readonly Ellipse[] _dots;
    private readonly double[] _px, _py, _vx, _vy;
    private readonly Random _rng = new();
    private bool _running;
    private float _amplitude;
    private float _targetAmplitude;

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x63, 0x66, 0xF1),
        Color.FromRgb(0x8B, 0x5C, 0xF6),
        Color.FromRgb(0xEC, 0x48, 0x99),
    ];

    public ParticleCloudAnimation()
    {
        _container = new Canvas { Width = 48, Height = 48 };
        _dots = new Ellipse[ParticleCount];
        _px = new double[ParticleCount];
        _py = new double[ParticleCount];
        _vx = new double[ParticleCount];
        _vy = new double[ParticleCount];

        for (int i = 0; i < ParticleCount; i++)
        {
            double r = _rng.NextDouble() * 1.5 + 1.5;
            _dots[i] = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(Palette[_rng.Next(Palette.Length)]),
                Opacity = 0.7
            };
            _px[i] = 24 + (_rng.NextDouble() - 0.5) * 20;
            _py[i] = 24 + (_rng.NextDouble() - 0.5) * 20;
            Canvas.SetLeft(_dots[i], _px[i]);
            Canvas.SetTop(_dots[i], _py[i]);
            _container.Children.Add(_dots[i]);
        }
    }

    public UIElement Visual => _container;

    public void Start() { if (_running) return; _running = true; CompositionTarget.Rendering += OnRender; }
    public void Stop() { _running = false; CompositionTarget.Rendering -= OnRender; }
    public void UpdateAmplitude(float amplitude) => _targetAmplitude = Math.Clamp(amplitude, 0f, 1f);

    private void OnRender(object? sender, EventArgs e)
    {
        _amplitude += (_targetAmplitude - _amplitude) * 0.12f;
        double radius = 10 + _amplitude * 14;
        double speed = 0.3 + _amplitude * 1.5;

        for (int i = 0; i < ParticleCount; i++)
        {
            double ax = (_rng.NextDouble() - 0.5) * speed;
            double ay = (_rng.NextDouble() - 0.5) * speed;

            double dx = 24 - _px[i], dy = 24 - _py[i];
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > radius && dist > 0.01)
            {
                ax += dx / dist * 0.8;
                ay += dy / dist * 0.8;
            }

            _vx[i] = (_vx[i] + ax) * 0.85;
            _vy[i] = (_vy[i] + ay) * 0.85;
            _px[i] = Math.Clamp(_px[i] + _vx[i], 0, 46);
            _py[i] = Math.Clamp(_py[i] + _vy[i], 0, 46);

            Canvas.SetLeft(_dots[i], _px[i]);
            Canvas.SetTop(_dots[i], _py[i]);
            _dots[i].Opacity = 0.3 + _amplitude * 0.7;
        }
    }
}
