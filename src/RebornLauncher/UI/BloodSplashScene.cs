using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>Short, vector-only blood splatter and drip overlay for the logo Easter egg.</summary>
public sealed class BloodSplashScene : FrameworkElement
{
    private const double FrameSeconds = 1d / 30d;
    private const double NormalLifetime = 9.2;
    private const double StillLifetime = 2.8;
    private const double FadeDuration = 1.15;
    private const int MaxDroplets = 42;
    private const int MaxSplashes = 42;
    private const int MaxDrips = 64;
    private const double Gravity = 520;

    private static readonly Brush DarkBlood = FrozenBrush(120, 7, 16);
    private static readonly Brush Blood = FrozenBrush(157, 14, 27);
    private static readonly Brush BrightBlood = FrozenBrush(195, 31, 43);
    private static readonly Brush BloodHighlight = FrozenBrush(224, 62, 69, 0.72);

    private sealed class Droplet(Point start, Point target, double flightTime, double radius, double vx, double vy)
    {
        public Point Start { get; } = start;
        public Point Target { get; } = target;
        public double FlightTime { get; } = flightTime;
        public double Radius { get; } = radius;
        public double Vx { get; } = vx;
        public double Vy { get; } = vy;
        public double Age { get; set; }
        public bool Impacted { get; set; }
        public Point Position { get; set; } = start;
    }

    private sealed record Splash(Point Center, double Radius, Geometry Shape, Brush Brush, Transform Transform);
    private sealed record Drip(double X, double StartY, double Width, double MaxLength, Brush Brush, double HighlightOffset)
    {
        public double Age { get; set; }
    }

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Random _random = new();
    private readonly List<Droplet> _droplets = [];
    private readonly List<Splash> _splashes = [];
    private readonly List<Drip> _drips = [];
    private double _elapsed;
    private double _lifetime = NormalLifetime;
    private bool _active;
    private bool _running;
    private bool _reduceMotion;
    private bool _visibleReady;
    private Point _origin;

    public bool IsAnimating => _timer.IsEnabled || _running;
    public int SplashCount => _splashes.Count;
    public int DripCount => _drips.Count;
    public int DropletCount => _droplets.Count(drop => !drop.Impacted);

    public BloodSplashScene()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = false;
        _timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromSeconds(FrameSeconds)
        };
        _timer.Tick += OnTick;
        Loaded += (_, _) => { _visibleReady = true; UpdateTimer(); };
        Unloaded += (_, _) => { _visibleReady = false; StopAndClear(); };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) StopAndClear();
            else UpdateTimer();
        };
        SizeChanged += (_, _) =>
        {
            if (_elapsed > 0) _origin = ClampToViewport(_origin);
        };
    }

    /// <summary>Enables the Easter egg while the launcher can safely show it.</summary>
    public void Configure(bool active, bool reduceMotion)
    {
        if (!active)
        {
            _active = false;
            StopAndClear();
            return;
        }

        bool wasReduced = _reduceMotion;
        _active = true;
        _reduceMotion = reduceMotion;
        if (_elapsed > 0 && wasReduced != _reduceMotion)
        {
            if (_reduceMotion) FreezeAsStillImage();
            else
            {
                _lifetime = NormalLifetime;
                foreach (var drip in _drips) drip.Age = Math.Max(drip.Age, .7);
            }
        }
        UpdateTimer();
    }

    /// <summary>Replaces any current effect with one burst launched from the logo center.</summary>
    public void Trigger(Point logoCenter)
    {
        if (!_active || ActualWidth <= 0 || ActualHeight <= 0) return;
        _timer.Stop();
        _droplets.Clear();
        _splashes.Clear();
        _drips.Clear();
        _elapsed = 0;
        _running = true;
        _origin = ClampToViewport(logoCenter);
        _lifetime = _reduceMotion ? StillLifetime : NormalLifetime;

        if (_reduceMotion) CreateStillBurst();
        else CreateFlyingBurst();

        _clock.Restart();
        InvalidateVisual();
        UpdateTimer();
    }

    /// <summary>Advances the current effect deterministically for the isolated UI verification.</summary>
    public void AdvanceForTest(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || !_running) return;
        double left = Math.Min(seconds, 30);
        while (left > 0 && _running)
        {
            double delta = Math.Min(1d / 60d, left);
            Step(delta);
            left -= delta;
        }
        InvalidateVisual();
    }

    private void CreateFlyingBurst()
    {
        for (int i = 0; i < MaxDroplets; i++)
        {
            double flightTime = .38 + _random.NextDouble() * 1.05;
            var target = new Point(
                _random.NextDouble() * Math.Max(1, ActualWidth),
                ActualHeight * (.08 + _random.NextDouble() * .62));
            double vx = (target.X - _origin.X) / flightTime;
            double vy = (target.Y - _origin.Y - .5 * Gravity * flightTime * flightTime) / flightTime;
            _droplets.Add(new Droplet(_origin, target, flightTime, 2 + _random.NextDouble() * 6, vx, vy));
        }
    }

    private void CreateStillBurst()
    {
        for (int i = 0; i < MaxSplashes; i++)
        {
            var center = new Point(_random.NextDouble() * ActualWidth,
                ActualHeight * (.08 + _random.NextDouble() * .66));
            AddImpact(center, 3 + _random.NextDouble() * 11, createDrips: true, still: true);
        }
    }

    private void FreezeAsStillImage()
    {
        foreach (var droplet in _droplets.Where(drop => !drop.Impacted).ToArray())
            AddImpact(droplet.Position, Math.Max(3, droplet.Radius * 1.65), createDrips: true, still: true);
        _droplets.Clear();
        foreach (var drip in _drips) drip.Age = 20;
        _lifetime = StillLifetime;
        _elapsed = 0;
        _clock.Restart();
        UpdateTimer();
        InvalidateVisual();
    }

    private void AddImpact(Point center, double radius, bool createDrips, bool still)
    {
        if (_splashes.Count >= MaxSplashes) return;
        center = ClampToViewport(center);
        var brush = _random.Next(4) == 0 ? DarkBlood : Blood;
        double rotation = _random.NextDouble() * 360;
        var transform = new TransformGroup();
        transform.Children.Add(new RotateTransform(rotation));
        transform.Children.Add(new TranslateTransform(center.X, center.Y));
        transform.Freeze();
        _splashes.Add(new Splash(center, radius, BuildSplat(radius, _random), brush, transform));
        if (!createDrips || _drips.Count >= MaxDrips) return;

        int count = Math.Min(_drips.Count >= MaxDrips - 1 ? 1 : 1 + _random.Next(2), MaxDrips - _drips.Count);
        for (int i = 0; i < count; i++)
        {
            double x = center.X + (_random.NextDouble() - .5) * radius * 1.2;
            double maxLength = Math.Min(Math.Max(0, ActualHeight - center.Y), ActualHeight * (.15 + _random.NextDouble() * .5));
            if (maxLength < 8) continue;
            var drip = new Drip(x, center.Y, 1.5 + _random.NextDouble() * Math.Max(2, radius * .55),
                maxLength, brush, (_random.NextDouble() - .5) * 1.4);
            if (still) drip.Age = 30;
            _drips.Add(drip);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        if (!_active || !_visibleReady || !IsVisible)
        {
            StopAndClear();
            return;
        }
        if (!double.IsFinite(elapsed) || elapsed <= 0) return;
        if (elapsed > .5)
        {
            // Do not replay a large missed interval after the dispatcher or application wakes.
            StopAndClear();
            return;
        }
        Step(elapsed);
        InvalidateVisual();
    }

    private void Step(double elapsed)
    {
        _elapsed += elapsed;
        if (!_reduceMotion)
        {
            foreach (var drop in _droplets.Where(drop => !drop.Impacted).ToArray())
            {
                drop.Age = Math.Min(drop.FlightTime, drop.Age + elapsed);
                double t = drop.Age;
                drop.Position = new Point(drop.Start.X + drop.Vx * t,
                    drop.Start.Y + drop.Vy * t + .5 * Gravity * t * t);
                if (drop.Age >= drop.FlightTime)
                {
                    drop.Impacted = true;
                    AddImpact(drop.Target, drop.Radius * (1.5 + _random.NextDouble()), createDrips: true, still: false);
                }
            }
            foreach (var drip in _drips) drip.Age += elapsed;
        }
        if (_elapsed >= _lifetime) StopAndClear();
    }

    private void UpdateTimer()
    {
        if (!_active || !_visibleReady || !IsVisible || !_running)
        {
            _timer.Stop();
            return;
        }
        if (!_timer.IsEnabled)
        {
            _clock.Restart();
            _timer.Start();
        }
    }

    private void StopAndClear()
    {
        _timer.Stop();
        _clock.Reset();
        _elapsed = 0;
        _running = false;
        _droplets.Clear();
        _splashes.Clear();
        _drips.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (!_running) return;
        double fadeStart = _lifetime - FadeDuration;
        double fade = _elapsed <= fadeStart ? 1 : Math.Clamp((_lifetime - _elapsed) / FadeDuration, 0, 1);
        if (fade <= 0) return;

        if (!_reduceMotion)
        {
            foreach (var drop in _droplets)
            {
                if (drop.Impacted) continue;
                double streak = drop.Radius * 2.3;
                context.PushOpacity(fade * .82);
                var pen = new Pen(Blood, Math.Max(1.3, drop.Radius * .58)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                var from = new Point(drop.Position.X - drop.Vx * .015, drop.Position.Y - drop.Vy * .015);
                context.DrawLine(pen, from, drop.Position);
                context.DrawEllipse(BrightBlood, null, drop.Position, drop.Radius, drop.Radius * 1.2);
                context.Pop();
            }
        }

        foreach (var drip in _drips)
        {
            double growth = _reduceMotion ? 1 : Math.Clamp(drip.Age / (1.1 + drip.MaxLength / 240), 0, 1);
            double length = drip.MaxLength * growth;
            if (length < .5) continue;
            context.PushOpacity(fade * (_reduceMotion ? .88 : .94));
            var bounds = new Rect(drip.X - drip.Width / 2, drip.StartY, drip.Width, length);
            context.DrawRoundedRectangle(drip.Brush, null, bounds, drip.Width / 2, drip.Width / 2);
            context.DrawEllipse(drip.Brush, null, new Point(drip.X + drip.HighlightOffset, drip.StartY + length),
                Math.Max(.7, drip.Width * .48), Math.Max(.7, drip.Width * .48));
            var highlightPen = new Pen(BloodHighlight, Math.Max(.55, drip.Width * .14));
            highlightPen.Freeze();
            context.DrawLine(highlightPen,
                new Point(drip.X - drip.Width * .16, drip.StartY + Math.Min(length * .55, 15)),
                new Point(drip.X - drip.Width * .16, drip.StartY + Math.Min(length * .55 + 8, length)));
            context.Pop();
        }

        foreach (var splash in _splashes)
        {
            context.PushOpacity(fade * .94);
            context.PushTransform(splash.Transform);
            context.DrawGeometry(splash.Brush, null, splash.Shape);
            context.DrawEllipse(BrightBlood, null,
                new Point(-splash.Radius * .18, -splash.Radius * .17),
                splash.Radius * .19, splash.Radius * .12);
            context.Pop();
            context.Pop();
        }
    }

    private Point ClampToViewport(Point point) => new(
        Math.Clamp(point.X, 0, Math.Max(0, ActualWidth)),
        Math.Clamp(point.Y, 0, Math.Max(0, ActualHeight)));

    private Geometry BuildSplat(double radius, Random random)
    {
        int count = 9 + random.Next(5);
        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double angle = 2 * Math.PI * i / count;
            double factor = .56 + random.NextDouble() * .58;
            points[i] = new Point(Math.Cos(angle) * radius * factor,
                Math.Sin(angle) * radius * factor);
        }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: true, isClosed: true);
            ctx.PolyLineTo(points.Skip(1).ToArray(), isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Brush FrozenBrush(byte r, byte g, byte b, double opacity = 1)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(opacity * 255), r, g, b));
        brush.Freeze();
        return brush;
    }
}
