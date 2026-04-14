using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VoiceInput.App.Overlay.Animations;

/// <summary>
/// 7 vertical bars waveform animation reacting to audio amplitude.
/// </summary>
public sealed class WaveformBarsAnimation : IRecordingAnimation
{
    private const int BarCount = 7;
    private readonly Canvas _container;
    private readonly Rectangle[] _bars;
    private readonly double[] _heights;
    private readonly Random _rng = new();
    private bool _running;
    private float _amplitude;
    private float _targetAmplitude;

    // Envelope: center bars are tallest
    private static readonly double[] Envelope = [0.3, 0.6, 0.85, 1.0, 0.85, 0.6, 0.3];

    public WaveformBarsAnimation()
    {
        _container = new Canvas { Width = 48, Height = 48 };
        _bars = new Rectangle[BarCount];
        _heights = new double[BarCount];

        double barW = 4, gap = 2.5;
        double totalW = BarCount * barW + (BarCount - 1) * gap;
        double startX = (48 - totalW) / 2;

        for (int i = 0; i < BarCount; i++)
        {
            _bars[i] = new Rectangle
            {
                Width = barW,
                RadiusX = 2, RadiusY = 2,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(0x63, 0x66, 0xF1),
                    Color.FromRgb(0xEC, 0x48, 0x99),
                    new Point(0, 0), new Point(0, 1))
            };
            Canvas.SetLeft(_bars[i], startX + i * (barW + gap));
            _container.Children.Add(_bars[i]);
            _heights[i] = 4;
        }
    }

    public UIElement Visual => _container;

    public void Start() { if (_running) return; _running = true; CompositionTarget.Rendering += OnRender; }
    public void Stop() { _running = false; CompositionTarget.Rendering -= OnRender; }
    public void UpdateAmplitude(float amplitude) => _targetAmplitude = Math.Clamp(amplitude, 0f, 1f);

    private void OnRender(object? sender, EventArgs e)
    {
        _amplitude += (_targetAmplitude - _amplitude) * 0.2f;

        for (int i = 0; i < BarCount; i++)
        {
            double targetH = 4 + _amplitude * Envelope[i] * 32 + _rng.NextDouble() * 2;
            _heights[i] += (targetH - _heights[i]) * 0.2;

            _bars[i].Height = Math.Max(4, _heights[i]);
            Canvas.SetTop(_bars[i], (48 - _bars[i].Height) / 2);
        }
    }
}
