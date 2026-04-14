using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

/// <summary>
/// Transparent overlay window for WPF transparency + animation performance spike.
/// Tests three animation modes: orb (breathing gradient), particles (Brownian), waveform (bars).
/// </summary>
class SpikeWindow : Window
{
    // ── DWM rounded corners ──────────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;

    // ── FPS tracking ─────────────────────────────────────────────────────────
    int _frameCount;
    int _secondsElapsed;
    readonly List<int> _fpsSamples = new();
    readonly string _mode;

    // ── Amplitude simulation ─────────────────────────────────────────────────
    double _amplitude;       // smoothed 0–1
    double _targetAmplitude; // random target
    readonly Random _rng = new();

    // ── Mode: orb ────────────────────────────────────────────────────────────
    Ellipse? _orb;
    DropShadowEffect? _orbGlow;
    ScaleTransform? _orbScale;

    // ── Mode: particles ──────────────────────────────────────────────────────
    record struct Particle(double X, double Y, double Vx, double Vy, Ellipse Shape);
    readonly List<Particle> _particles = new();
    Canvas? _particleCanvas;

    // ── Mode: waveform ───────────────────────────────────────────────────────
    readonly List<Rectangle> _bars = new();

    public SpikeWindow(string mode)
    {
        _mode = mode;

        // Window chrome
        Title = $"TransparencySpike [{mode}]";
        Width = 300;
        Height = 300;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        // Close on Escape
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };

        // Allow dragging
        MouseLeftButtonDown += (_, e) => DragMove();

        Loaded += OnLoaded;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        // DWM rounded corners
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

        // Build the chosen animation
        switch (_mode)
        {
            case "orb":        BuildOrb();       break;
            case "particles":  BuildParticles(); break;
            case "waveform":   BuildWaveform();  break;
        }

        // FPS counter via CompositionTarget.Rendering
        CompositionTarget.Rendering += (_, _) => _frameCount++;

        // Amplitude noise + per-frame logic every 50 ms
        var animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        animTimer.Tick += OnAnimTick;
        animTimer.Start();

        // FPS report every 1 s
        var fpsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        fpsTimer.Tick += OnFpsTick;
        fpsTimer.Start();
    }

    // ── Animation tick (50 ms) ───────────────────────────────────────────────

    void OnAnimTick(object? sender, EventArgs e)
    {
        // Random walk toward new target
        if (_rng.NextDouble() < 0.1)
            _targetAmplitude = _rng.NextDouble();

        // Lerp smoothing
        _amplitude += (_targetAmplitude - _amplitude) * 0.15;

        switch (_mode)
        {
            case "orb":       UpdateOrb();       break;
            case "particles": UpdateParticles(); break;
            case "waveform":  UpdateWaveform();  break;
        }
    }

    // ── FPS tick (1 s) ───────────────────────────────────────────────────────

    void OnFpsTick(object? sender, EventArgs e)
    {
        int fps = _frameCount;
        _frameCount = 0;
        _fpsSamples.Add(fps);
        _secondsElapsed++;

        double avg = Average(_fpsSamples);
        Console.WriteLine($"[FPS] {_mode}: {fps} fps (avg: {avg:F1})");

        if (_secondsElapsed >= 10)
        {
            int min = Min(_fpsSamples);
            int max = Max(_fpsSamples);
            string verdict = avg >= 30 ? "PASS" : "FAIL";
            Console.WriteLine(
                $"RESULT: mode={_mode} avg_fps={avg:F1} min_fps={min} max_fps={max} VERDICT={verdict}");
            Close();
        }
    }

    // ── Orb ─────────────────────────────────────────────────────────────────

    void BuildOrb()
    {
        var grid = new Grid();
        Content = grid;

        _orbGlow = new DropShadowEffect
        {
            Color = Color.FromRgb(0x63, 0x66, 0xF1),
            BlurRadius = 20,
            ShadowDepth = 0,
            Opacity = 0.8
        };

        var brush = new RadialGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromRgb(0x63, 0x66, 0xF1), 0.0),
            new GradientStop(Color.FromRgb(0x8B, 0x5C, 0xF6), 0.5),
            new GradientStop(Color.FromRgb(0xEC, 0x48, 0x99), 1.0),
        });

        _orbScale = new ScaleTransform(1.0, 1.0);

        _orb = new Ellipse
        {
            Width = 60,
            Height = 60,
            Fill = brush,
            Effect = _orbGlow,
            Opacity = 0.85,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _orbScale,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.Children.Add(_orb);
    }

    void UpdateOrb()
    {
        if (_orb == null) return;
        double scale  = 1.0 + _amplitude * 0.3;
        double opacity = 0.7 + _amplitude * 0.3;
        _orbScale!.ScaleX = scale;
        _orbScale!.ScaleY = scale;
        _orb.Opacity = opacity;
        _orbGlow!.BlurRadius = 15 + _amplitude * 25;
    }

    // ── Particles ────────────────────────────────────────────────────────────

    void BuildParticles()
    {
        _particleCanvas = new Canvas { Width = 300, Height = 300 };
        Content = _particleCanvas;

        Color[] palette =
        {
            Color.FromRgb(0x63, 0x66, 0xF1),
            Color.FromRgb(0x8B, 0x5C, 0xF6),
            Color.FromRgb(0xEC, 0x48, 0x99),
        };

        for (int i = 0; i < 20; i++)
        {
            double r = _rng.NextDouble() * 2 + 2; // 2–4 px
            var dot = new Ellipse
            {
                Width  = r * 2,
                Height = r * 2,
                Fill   = new SolidColorBrush(palette[_rng.Next(palette.Length)]),
                Opacity = 0.85
            };
            double x = _rng.NextDouble() * 280 + 10;
            double y = _rng.NextDouble() * 280 + 10;
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot,  y);
            _particleCanvas.Children.Add(dot);
            _particles.Add(new Particle(x, y, 0, 0, dot));
        }
    }

    void UpdateParticles()
    {
        if (_particleCanvas == null) return;
        double cx = 150, cy = 150;
        double radius = 60 + _amplitude * 80; // 60–140 px boundary
        double speedMult = 1 + _amplitude * 3;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];

            // Brownian nudge
            double ax = (_rng.NextDouble() - 0.5) * 2 * speedMult;
            double ay = (_rng.NextDouble() - 0.5) * 2 * speedMult;

            // Attraction to centre
            double dx = cx - p.X, dy = cy - p.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > radius)
            {
                ax += dx / dist * 2;
                ay += dy / dist * 2;
            }

            double vx = (p.Vx + ax) * 0.85;
            double vy = (p.Vy + ay) * 0.85;
            double nx = Math.Clamp(p.X + vx, 0, 296);
            double ny = Math.Clamp(p.Y + vy, 0, 296);

            Canvas.SetLeft(p.Shape, nx);
            Canvas.SetTop(p.Shape,  ny);
            _particles[i] = p with { X = nx, Y = ny, Vx = vx, Vy = vy };
        }
    }

    // ── Waveform ─────────────────────────────────────────────────────────────

    void BuildWaveform()
    {
        var panel = new Canvas { Width = 300, Height = 300 };
        Content = panel;

        // 7 bars, width=4, gap=3  → total span = 7*4 + 6*3 = 46
        // centre at 150 → start at 150 - 23 = 127
        double startX = 127;
        double barW   = 4;
        double gap    = 3;
        double stride = barW + gap;

        // Centre envelope: bar 0 & 6 → 0.3, bars 1 & 5 → 0.6, bars 2 & 4 → 0.85, bar 3 → 1.0
        double[] envFactors = { 0.3, 0.6, 0.85, 1.0, 0.85, 0.6, 0.3 };

        for (int i = 0; i < 7; i++)
        {
            var bar = new Rectangle
            {
                Width  = barW,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(0x63, 0x66, 0xF1),
                    Color.FromRgb(0xEC, 0x48, 0x99),
                    new Point(0, 0), new Point(0, 1))
            };
            // Tag stores envelope factor
            bar.Tag = envFactors[i];
            panel.Children.Add(bar);
            Canvas.SetLeft(bar, startX + i * stride);
            _bars.Add(bar);
        }
    }

    void UpdateWaveform()
    {
        double maxH = 120;
        double minH = 4;
        double canvasH = 300;

        for (int i = 0; i < _bars.Count; i++)
        {
            double env = (double)_bars[i].Tag!;
            double h = minH + (_amplitude * env) * (maxH - minH);
            _bars[i].Height = h;
            Canvas.SetTop(_bars[i], canvasH / 2 - h / 2);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static double Average(List<int> s) => s.Count == 0 ? 0 : (double)Sum(s) / s.Count;
    static int Sum(List<int> s) { int t = 0; foreach (var v in s) t += v; return t; }
    static int Min(List<int> s) { int m = int.MaxValue; foreach (var v in s) if (v < m) m = v; return m; }
    static int Max(List<int> s) { int m = int.MinValue; foreach (var v in s) if (v > m) m = v; return m; }
}
