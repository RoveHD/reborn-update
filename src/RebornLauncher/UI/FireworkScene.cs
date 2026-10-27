using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>Transparent, non-interactive ambient fireworks overlay in viewport DIPs.</summary>
public sealed class FireworkScene : FrameworkElement
{
    private readonly FireworkSimulation _simulation;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<uint, Brush> _brushes = [];
    private readonly Dictionary<uint, Brush> _glows = [];
    private readonly Dictionary<(uint Color, int Width), Pen> _pens = [];
    private readonly List<BurstLight> _burstLights = [];
    private double _sceneWidth = 1, _sceneHeight = 1;
    private sealed class BurstLight(FireworkBurstEvent burst)
    {
        public double X = burst.X, Y = burst.Y, Age;
        public uint Color = burst.Color;
        public bool Secondary = burst.IsSecondary;
    }

    public event Action<string>? SoundRequested;
    public int ActiveParticleCount => _simulation.ActiveParticleCount;
    public int VisibleSparkCount => _simulation.Particles.Count;
    public bool IsAnimating => _timer.IsEnabled;
    public int BurstCount => _simulation.BurstCount;
    public FireworkBurstKind? LastBurstKind => _simulation.LastBurstKind;
    public int SecondaryBurstCount => _simulation.SecondaryBurstCount;
    public int ActiveAtmosphereCount => _burstLights.Count;

    public FireworkScene() : this(new FireworkSimulation()) { }

    public FireworkScene(FireworkSimulation simulation)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = false;
        _simulation.SoundRequested += cue => SoundRequested?.Invoke(cue);
        _simulation.BurstCreated += burst =>
        {
            if (_burstLights.Count >= 18) _burstLights.RemoveAt(0);
            _burstLights.Add(new BurstLight(burst));
        };
        // Ambient scenery must yield to input, even on a software-rendered desktop.
        _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(1000d / 30) };
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
        SizeChanged += (_, _) => ResizeSimulation();
    }

    public void Configure(bool enabled, int particleLimit, double intensity, bool reduceMotion, bool active)
    {
        _simulation.Configure(enabled, Math.Clamp(particleLimit, 0, FireworkSimulation.MaxParticleBudget), intensity, reduceMotion, active);
        if (!_simulation.IsRunning) _burstLights.Clear();
        SyncTimer();
        InvalidateVisual();
    }

    /// <summary>Advances a deterministic simulation for renderer smoke tests without waiting on the UI clock.</summary>
    public void AdvanceForTest(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || !_simulation.IsRunning) return;
        ResizeSimulation();
        double left = Math.Min(seconds, 120);
        while (left > 0)
        {
            double dt = Math.Min(1d / 60d, left);
            AdvanceFrame(dt);
            left -= dt;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        // Local bloom, drifting powder smoke and thin pressure rings belong to
        // each explosion. There is deliberately no full-screen white flash.
        foreach (var light in _burstLights)
        {
            double scale = Math.Clamp(Math.Min(ActualWidth / 1280, ActualHeight / 800), .65, 1.7);
            double size = light.Secondary ? .52 : 1;
            double t = light.Age;
            var center = new Point(light.X, light.Y);
            double smoke = Math.Clamp(t / .45, 0, 1) * Math.Clamp(1 - t / 4.2, 0, 1);
            double radius = (24 + t * 22) * scale * size;
            dc.DrawEllipse(GlowFor(FadedColor(0xFFB4AC9E, smoke * (light.Secondary ? .10 : .20))), null, new Point(center.X + t * 13, center.Y - t * 10), radius * 1.5, radius);
            if (t < .65)
            {
                double glowRadius = (34 + t * 120) * scale * size;
                dc.DrawEllipse(GlowFor(FadedColor(light.Color, Math.Exp(-t * 9) * (light.Secondary ? .42 : .72))), null, center, glowRadius * 1.3, glowRadius);
                double ring = (12 + t * 170) * scale * size;
                dc.DrawEllipse(null, PenFor(FadedColor(light.Color, .20 * Math.Pow(1 - t / .65, 2)), .8), center, ring, ring);
            }
        }
        foreach (var particle in _simulation.Particles)
        {
            double fade = particle.Opacity;
            if (fade <= 0) continue;
            uint color = BlendColor(particle.Color, particle.EndColor, particle.ColorProgress);
            if (particle.LeavesTrail && particle.TrailPoints.Count > 1)
            {
                // One thin path retains the authored willow/palm shapes. Three
                // overlapping paths and per-star opacity layers stalled WPF's
                // software rasterizer, particularly at large window sizes.
                dc.DrawGeometry(null, PenFor(FadedColor(color, fade * .55), particle.Size * .65), TrailPath(particle.TrailPoints, 0));
            }
            double dx = particle.X - particle.PreviousX;
            double dy = particle.Y - particle.PreviousY;
            if (dx * dx + dy * dy > .35)
            {
                dc.DrawLine(PenFor(FadedColor(color, fade * .65), particle.Size * .65), new Point(particle.PreviousX, particle.PreviousY), new Point(particle.X, particle.Y));
            }
            if (!particle.IsTrail)
            {
                // Solid, translucent halos cost no gradient surface per spark.
                dc.DrawEllipse(BrushFor(FadedColor(color, fade * .12)), null, new Point(particle.X, particle.Y), particle.Size * 2.4, particle.Size * 2.4);
            }
            double opacity = fade * (particle.IsTrail ? .42 : .95);
            dc.DrawEllipse(BrushFor(FadedColor(color, opacity)), null, new Point(particle.X, particle.Y), particle.Size * 1.12, particle.Size * 1.12);
            dc.DrawEllipse(BrushFor(FadedColor(CoreColor(color), opacity)), null, new Point(particle.X, particle.Y), particle.Size * .58, particle.Size * .58);
            if (particle.Twinkles && fade > .6 && Math.Sin(particle.Age * 14 + particle.X * .03) > .72)
            {
                dc.DrawLine(PenFor(FadedColor(color, opacity), .65), new Point(particle.X - 3, particle.Y), new Point(particle.X + 3, particle.Y));
                dc.DrawLine(PenFor(FadedColor(color, opacity), .65), new Point(particle.X, particle.Y - 3), new Point(particle.X, particle.Y + 3));
            }
        }
        foreach (var rocket in _simulation.Rockets)
            DrawComet(dc, rocket.X, rocket.Y, rocket.Vx, rocket.Vy, 0xFFFFD58B, rocket.TrailPoints, 1);
        foreach (var shell in _simulation.SecondaryShells)
            DrawComet(dc, shell.X, shell.Y, shell.Vx, shell.Vy, shell.Color, shell.TrailPoints, .8);
    }

    private void DrawComet(DrawingContext dc, double x, double y, double vx, double vy, uint color, IReadOnlyList<FireworkTrailPoint> trail, double scale)
    {
        if (trail.Count > 1) dc.DrawGeometry(null, PenFor(FadedColor(color, .5), 2.2 * scale), TrailPath(trail, 0));
        dc.DrawEllipse(BrushFor(FadedColor(color, .14)), null, new Point(x, y), 5 * scale, 5 * scale);
        dc.DrawLine(PenFor(FadedColor(color, .85), 1.5 * scale), new Point(x - vx * .045, y - vy * .045), new Point(x, y));
        dc.DrawEllipse(BrushFor(FadedColor(CoreColor(color), .85)), null, new Point(x, y), 1.7 * scale, 1.7 * scale);
    }

    private static StreamGeometry TrailPath(IReadOnlyList<FireworkTrailPoint> trail, int start)
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(trail[start].X, trail[start].Y), false, false);
            for (int i = start + 1; i < trail.Count; i++) path.LineTo(new Point(trail[i].X, trail[i].Y), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static uint BlendColor(uint from, uint to, double amount)
    {
        // Nine levels keep color caches bounded while avoiding abrupt transitions.
        double t = Math.Clamp(Math.Round(amount * 8) / 8, 0, 1);
        static uint Channel(uint a, uint b, int shift, double t) => (uint)Math.Round(((a >> shift) & 255) * (1 - t) + ((b >> shift) & 255) * t);
        return 0xFF000000 | Channel(from, to, 16, t) << 16 | Channel(from, to, 8, t) << 8 | Channel(from, to, 0, t);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResizeSimulation();
        SyncTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _clock.Reset();
        _simulation.Clear();
        _burstLights.Clear();
        InvalidateVisual();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            _timer.Stop();
            _clock.Reset();
            _simulation.Clear();
            _burstLights.Clear();
            InvalidateVisual();
            return;
        }
        ResizeSimulation();
        SyncTimer();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        AdvanceFrame(elapsed);
        InvalidateVisual();
    }

    private void AdvanceFrame(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed <= 0 || !_simulation.IsRunning) return;
        double dt = Math.Min(elapsed, 1d / 30);
        for (int i = _burstLights.Count - 1; i >= 0; i--)
        {
            _burstLights[i].Age += dt;
            if (_burstLights[i].Age >= 4.2) _burstLights.RemoveAt(i);
        }
        _simulation.Step(dt);
    }

    private void SyncTimer()
    {
        if (!IsLoaded || !IsVisible || !_simulation.IsRunning)
        {
            _timer.Stop();
            _clock.Reset();
            if (!_simulation.IsRunning) { _simulation.Clear(); _burstLights.Clear(); }
            return;
        }
        if (!_timer.IsEnabled)
        {
            _clock.Restart();
            _timer.Start();
        }
    }

    private void ResizeSimulation()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        double sx = ActualWidth / _sceneWidth, sy = ActualHeight / _sceneHeight;
        foreach (var light in _burstLights) { light.X *= sx; light.Y *= sy; }
        _sceneWidth = ActualWidth; _sceneHeight = ActualHeight;
        _simulation.Resize(ActualWidth, ActualHeight);
    }

    private Pen PenFor(uint color, double width)
    {
        int quantized = Math.Clamp((int)Math.Round(width * 4), 2, 20);
        var key = (color, quantized);
        if (_pens.TryGetValue(key, out var existing)) return existing;
        if (_pens.Count >= 4096) _pens.Clear();
        var pen = new Pen(BrushFor(color), quantized / 4d) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze(); _pens[key] = pen;
        return pen;
    }

    private Brush GlowFor(uint color)
    {
        if (_glows.TryGetValue(color, out var existing)) return existing;
        if (_glows.Count >= 4096) _glows.Clear();
        byte red = (byte)(color >> 16), green = (byte)(color >> 8), blue = (byte)color;
        double alpha = (color >> 24) / 255d;
        var glow = new RadialGradientBrush();
        glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(190 * alpha), red, green, blue), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(70 * alpha), red, green, blue), .3));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, red, green, blue), 1));
        glow.Freeze(); _glows[color] = glow;
        return glow;
    }

    private Brush BrushFor(uint argb)
    {
        if (_brushes.TryGetValue(argb, out var brush)) return brush;
        if (_brushes.Count >= 4096) _brushes.Clear();
        var color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        var created = new SolidColorBrush(color);
        created.Freeze();
        _brushes[argb] = created;
        return created;
    }

    private static uint FadedColor(uint color, double opacity) =>
        ((uint)Math.Clamp((int)Math.Round(opacity * 16), 0, 16) * 255 / 16) << 24 | (color & 0xFFFFFF);

    private static uint CoreColor(uint argb)
    {
        var red = (byte)(argb >> 16);
        var green = (byte)(argb >> 8);
        var blue = (byte)argb;
        static byte Lift(byte channel) => (byte)Math.Min(255, channel + (255 - channel) * .65);
        return 0xFF000000u | (uint)Lift(red) << 16 | (uint)Lift(green) << 8 | Lift(blue);
    }
}
