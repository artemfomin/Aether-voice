using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VoiceInput.App.Overlay.Animations;

/// <summary>
/// Particle cloud animation — swarm of tiny glowing dots with Brownian motion,
/// reacting to voice amplitude. Particles are 1-3px with varying opacity
/// creating a nebula/dust-cloud effect.
/// </summary>
public sealed class ParticleCloudAnimation : IRecordingAnimation
{
    private const int ParticleCount = 60;
    private readonly Canvas _container;
    private readonly Rectangle[] _particles;
    private readonly double[] _px, _py, _vx, _vy, _baseSize;
    private readonly float[] _phase;
    private readonly Random _rng = new();
    private bool _running;
    private float _amplitude;
    private float _targetAmplitude;
    private int _frame;

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x63, 0xE6, 0xBE), // teal
        Color.FromRgb(0x63, 0x66, 0xF1), // indigo
        Color.FromRgb(0x8B, 0x5C, 0xF6), // violet
        Color.FromRgb(0xA7, 0x8B, 0xFA), // lavender
        Color.FromRgb(0xEC, 0x48, 0x99), // pink
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white sparkle
    ];

    public ParticleCloudAnimation()
    {
        _container = new Canvas
        {
            Width = 48,
            Height = 48,
            ClipToBounds = true,
        };
        _particles = new Rectangle[ParticleCount];
        _px = new double[ParticleCount];
        _py = new double[ParticleCount];
        _vx = new double[ParticleCount];
        _vy = new double[ParticleCount];
        _baseSize = new double[ParticleCount];
        _phase = new float[ParticleCount];

        for (int i = 0; i < ParticleCount; i++)
        {
            // Mix of tiny (1px) dust and slightly larger (2-3px) particles
            double size = i < ParticleCount / 2
                ? _rng.NextDouble() * 0.5 + 0.5  // tiny: 0.5-1px
                : _rng.NextDouble() * 1.5 + 1.0;  // small: 1-2.5px

            _baseSize[i] = size;
            _phase[i] = (float)(_rng.NextDouble() * Math.PI * 2);

            var color = Palette[_rng.Next(Palette.Length)];
            _particles[i] = new Rectangle
            {
                Width = size,
                Height = size,
                RadiusX = size / 2,
                RadiusY = size / 2,
                Fill = new SolidColorBrush(color),
                Opacity = _rng.NextDouble() * 0.4 + 0.2, // 0.2 - 0.6
            };

            _px[i] = 24 + (_rng.NextDouble() - 0.5) * 30;
            _py[i] = 24 + (_rng.NextDouble() - 0.5) * 30;
            Canvas.SetLeft(_particles[i], _px[i]);
            Canvas.SetTop(_particles[i], _py[i]);
            _container.Children.Add(_particles[i]);
        }
    }

    public UIElement Visual => _container;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _frame = 0;
        CompositionTarget.Rendering += OnRender;
    }

    public void Stop()
    {
        _running = false;
        CompositionTarget.Rendering -= OnRender;
    }

    public void UpdateAmplitude(float amplitude) => _targetAmplitude = Math.Clamp(amplitude, 0f, 1f);

    private void OnRender(object? sender, EventArgs e)
    {
        _frame++;
        _amplitude += (_targetAmplitude - _amplitude) * 0.1f;

        double cloudRadius = 8 + _amplitude * 16;   // cloud expands with amplitude
        double brownianForce = 0.15 + _amplitude * 2.0; // jitter increases with voice
        double centerPull = 0.4 + _amplitude * 0.3;  // gravity toward center

        for (int i = 0; i < ParticleCount; i++)
        {
            // Brownian motion
            double bx = (_rng.NextDouble() - 0.5) * brownianForce;
            double by = (_rng.NextDouble() - 0.5) * brownianForce;

            // Center gravity (stronger when outside cloud radius)
            double dx = 24 - _px[i], dy = 24 - _py[i];
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > cloudRadius && dist > 0.01)
            {
                double pull = centerPull * (dist - cloudRadius) / dist;
                bx += dx / dist * pull;
                by += dy / dist * pull;
            }

            // Orbital drift (subtle swirl)
            double angle = Math.Atan2(dy, dx) + Math.PI / 2;
            double orbitalSpeed = 0.05 + _amplitude * 0.15;
            bx += Math.Cos(angle) * orbitalSpeed;
            by += Math.Sin(angle) * orbitalSpeed;

            _vx[i] = (_vx[i] + bx) * 0.88;
            _vy[i] = (_vy[i] + by) * 0.88;
            _px[i] = Math.Clamp(_px[i] + _vx[i], -2, 50);
            _py[i] = Math.Clamp(_py[i] + _vy[i], -2, 50);

            Canvas.SetLeft(_particles[i], _px[i]);
            Canvas.SetTop(_particles[i], _py[i]);

            // Pulsing opacity — twinkle effect
            float phaseOffset = _phase[i] + _frame * 0.04f;
            double twinkle = 0.3 + 0.3 * Math.Sin(phaseOffset) + _amplitude * 0.4;
            _particles[i].Opacity = Math.Clamp(twinkle, 0.1, 1.0);

            // Size pulse with amplitude
            double size = _baseSize[i] * (1.0 + _amplitude * 0.8);
            _particles[i].Width = size;
            _particles[i].Height = size;
        }
    }
}
