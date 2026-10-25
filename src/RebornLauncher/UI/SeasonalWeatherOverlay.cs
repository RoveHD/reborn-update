using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>Click-through weather in window coordinates, above scenery and below modal effects.</summary>
public sealed class SeasonalWeatherOverlay : FrameworkElement
{
    private static readonly DependencyProperty DaylightProperty = DependencyProperty.Register(
        nameof(Daylight), typeof(double), typeof(SeasonalWeatherOverlay),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly Brush Snow = Brush(222, 243, 255);
    private static readonly Brush Ice = Brush(162, 214, 243);
    private static readonly Brush Gold = Brush(255, 211, 129);
    private static readonly Brush Cream = Brush(255, 241, 197);
    private static readonly Brush Pink = Brush(255, 185, 211);
    private static readonly Brush Green = Brush(144, 201, 139);
    private static readonly Brush Copper = Brush(217, 111, 59);
    private static readonly Brush Amber = Brush(240, 177, 66);
    private static readonly Brush Red = Brush(191, 83, 67);
    private static readonly Brush Violet = Brush(178, 147, 224);
    private static readonly Pen SnowLine = Pen(218, 241, 255, 1);
    private static readonly Pen WaterLine = Pen(155, 220, 241, 1.1);
    private static readonly Geometry Leaf = Shape(false);
    private static readonly Geometry Petal = Shape(true);
    private static readonly Brush Sunbeam = Beam(255, 224, 161);
    private static readonly Brush Moonbeam = Beam(147, 193, 235);
    private static readonly Brush Mist = new RadialGradientBrush(Color.FromArgb(24, 179, 153, 211), Colors.Transparent);
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1d / 30) };
    private readonly Stopwatch _clock = new();
    private Rect[] _readingZones = [];
    private string _theme = "standard";
    private bool _enabled, _active, _reduce, _configured, _daylight;
    private int _limit;
    private double _intensity = 1, _elapsed;
    private double Daylight { get => (double)GetValue(DaylightProperty); set => SetValue(DaylightProperty, value); }
    public bool IsAnimating => _timer.IsEnabled;
    public int ParticleCount => !_enabled ? 0 : Math.Min(_limit, (int)Math.Round(Math.Clamp(ActualWidth * ActualHeight / 18000, 40, 95) * (_reduce ? .18 : _intensity)));

    public SeasonalWeatherOverlay()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
        _timer.Tick += (_, _) =>
        {
            double dt = Math.Min(.1, _clock.Elapsed.TotalSeconds);
            _clock.Restart();
            AdvanceForTest(dt);
        };
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => UpdateTimer();
    }

    public void Configure(string theme, bool daylight, bool enabled, int particleLimit, double intensity, bool reduceMotion, bool active)
    {
        string next = theme is "winter" or "spring" or "summer" or "harvest" or "halloween" or "newyear" or "arceus" ? theme : "standard";
        bool changed = next != _theme;
        if (changed) _elapsed = 0;
        _theme = next;
        _limit = Math.Clamp(particleLimit, 0, 160);
        _intensity = double.IsFinite(intensity) ? Math.Clamp(intensity, .2, 1.6) : 1;
        _enabled = enabled && _limit > 0;
        _active = active;
        _reduce = reduceMotion;
        if (!_configured || changed || daylight != _daylight || reduceMotion || !active)
        {
            double from = Daylight, target = daylight ? 1 : 0;
            BeginAnimation(DaylightProperty, null);
            Daylight = target;
            if (_configured && !changed && daylight != _daylight && active && !reduceMotion)
                BeginAnimation(DaylightProperty, new DoubleAnimation(from, target, TimeSpan.FromSeconds(2.8))
                { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
        }
        _daylight = daylight;
        _configured = true;
        Visibility = _enabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateTimer();
        InvalidateVisual();
    }

    public void SetReadingZones(IEnumerable<Rect> zones)
    {
        var next = zones.Where(r => !r.IsEmpty && double.IsFinite(r.X) && double.IsFinite(r.Y)
            && double.IsFinite(r.Width) && double.IsFinite(r.Height)).ToArray();
        if (_readingZones.SequenceEqual(next)) return;
        _readingZones = next;
        InvalidateVisual();
    }

    /// <summary>Deterministic advancement for the render check; the real clock pauses when inactive.</summary>
    public void AdvanceForTest(double seconds)
    {
        if (!_enabled || _reduce || !double.IsFinite(seconds) || seconds <= 0) return;
        _elapsed += seconds;
        InvalidateVisual();
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!_enabled || ActualWidth < 1 || ActualHeight <= 98) return;
        // Leave navigation and the status/taskbar edge completely clear.
        var viewport = new Rect(0, 64, ActualWidth, ActualHeight - 98);
        dc.PushClip(new RectangleGeometry(viewport));
        if (_theme is "summer" or "arceus" or "standard") DrawLight(dc, viewport);
        if (_theme == "halloween") DrawMist(dc, viewport);
        int count = ParticleCount;
        for (int i = 0; i < count; i++)
        {
            double seed = Random(i * 7 + 3), depth = .55 + Random(i * 11 + 5) * .7;
            bool rising = _theme is "standard" or "halloween" or "arceus" or "summer";
            double speed = (_theme is "harvest" or "spring" ? 24 : 13) + seed * 26;
            double y = 64 + Wrap(Random(i * 23 + 7) * (viewport.Height + 36) + _elapsed * speed * depth * (rising ? -1 : 1), viewport.Height + 36) - 18;
            double x = Wrap(seed * (ActualWidth + 44) + _elapsed * (4 + Random(i * 17) * 11)
                + Math.Sin(_elapsed * (.32 + seed * .5) + i) * (14 + seed * 25), ActualWidth + 44) - 22;
            var center = new Point(x, y);
            double opacity = (.38 + depth * .24) * Readability(center);
            dc.PushOpacity(opacity);
            dc.PushTransform(new TranslateTransform(x, y));
            switch (_theme)
            {
                case "winter":
                    double radius = (1.3 + seed * 2.5) * depth;
                    if (i % 9 == 0)
                    {
                        dc.PushTransform(new RotateTransform(_elapsed * (8 + seed * 15) + i * 37));
                        for (int arm = 0; arm < 3; arm++)
                        {
                            dc.DrawLine(SnowLine, new Point(-radius * 1.8, 0), new Point(radius * 1.8, 0));
                            dc.PushTransform(new RotateTransform(60));
                        }
                        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
                    }
                    else dc.DrawEllipse(i % 3 == 0 ? Ice : Snow, null, new Point(), radius, radius);
                    break;
                case "spring":
                case "harvest":
                    double size = (_theme == "spring" ? 4.5 : 7) + seed * 5;
                    dc.PushTransform(new RotateTransform(i * 43 + _elapsed * (17 + seed * 33) + Math.Sin(_elapsed + i) * 22));
                    dc.PushTransform(new ScaleTransform(size * (.65 + Math.Abs(Math.Sin(_elapsed * .7 + i)) * .35), size));
                    dc.DrawGeometry(_theme == "spring" ? (i % 4 == 0 ? Green : i % 3 == 0 ? Cream : Pink)
                        : i % 3 == 0 ? Red : i % 3 == 1 ? Copper : Amber, null, _theme == "spring" ? Petal : Leaf);
                    dc.Pop(); dc.Pop();
                    break;
                case "summer":
                    if (i % 3 == 0)
                    {
                        double bubble = 2 + seed * 5;
                        dc.DrawEllipse(null, WaterLine, new Point(), bubble, bubble);
                        dc.DrawEllipse(Snow, null, new Point(-bubble * .3, -bubble * .3), .8, .8);
                    }
                    else Spark(dc, 1.2 + seed * 1.8, i % 2 == 0 ? Gold : Ice);
                    break;
                case "newyear":
                    dc.PushTransform(new RotateTransform(i * 23 + _elapsed * (25 + seed * 35)));
                    dc.DrawRoundedRectangle(i % 3 == 0 ? Pink : i % 3 == 1 ? Gold : Ice, null,
                        new Rect(-1.2 * depth, -3.2 * depth, 2.4 * depth, 6.4 * depth), .4, .4);
                    dc.Pop();
                    break;
                case "arceus":
                    dc.PushOpacity(.5 + .5 * Math.Pow(Math.Sin(_elapsed * .9 + i), 2));
                    Spark(dc, 2 + seed * 3, i % 3 == 0 ? Snow : Cream);
                    dc.Pop();
                    break;
                case "halloween":
                    dc.DrawEllipse(i % 3 == 0 ? Violet : Copper, null, new Point(), 1.4 * depth, (2.2 + seed) * depth);
                    break;
                default:
                    dc.PushOpacity(.4 + .6 * Math.Pow(Math.Sin(_elapsed * .7 + i), 2));
                    if (i % 7 == 0) Spark(dc, 2 + seed, Daylight > .5 ? Gold : Snow);
                    else dc.DrawEllipse(i % 3 == 0 ? Gold : Ice, null, new Point(), depth * 1.5, depth * 1.5);
                    dc.Pop();
                    break;
            }
            dc.Pop(); dc.Pop();
        }
        if (_theme == "summer") DrawWater(dc, viewport);
        dc.Pop();
    }

    private double Readability(Point p)
    {
        foreach (var zone in _readingZones)
        {
            if (zone.Contains(p)) return .12;
            var padded = zone; padded.Inflate(14, 14);
            if (padded.Contains(p)) return .35;
        }
        return 1;
    }

    private void DrawLight(DrawingContext dc, Rect viewport)
    {
        double daylight = _theme == "arceus" ? 1 : Daylight;
        for (int i = 0; i < 3; i++)
        {
            dc.PushOpacity(.04 + Math.Sin(_elapsed * .23 + i) * .012);
            dc.PushTransform(new TranslateTransform(ActualWidth * (.84 + i * .07), viewport.Top - 20));
            dc.PushTransform(new RotateTransform(24 + Math.Sin(_elapsed * .14 + i) * 3));
            var beam = new Rect(-18, 0, 36 + i * 18, viewport.Height * .9);
            dc.PushOpacity(daylight);
            dc.DrawRectangle(Sunbeam, null, beam);
            dc.Pop();
            dc.PushOpacity((1 - daylight) * .5);
            dc.DrawRectangle(Moonbeam, null, beam);
            dc.Pop();
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    private void DrawMist(DrawingContext dc, Rect viewport)
    {
        for (int i = 0; i < 3; i++)
            dc.DrawEllipse(Mist, null, new Point(ActualWidth * (i % 2 == 0 ? .12 : .9) + Math.Sin(_elapsed * .15 + i) * 45,
                viewport.Bottom - 45 - i * 28), ActualWidth * .24, 48 + i * 13);
    }

    private void DrawWater(DrawingContext dc, Rect viewport)
    {
        for (int side = 0; side < 2; side++)
        for (int i = 0; i < 3; i++)
        {
            double pulse = Wrap(_elapsed * .18 + i / 3d + side * .23, 1);
            dc.PushOpacity((1 - pulse) * .23);
            dc.DrawEllipse(null, WaterLine, new Point(ActualWidth * (side == 0 ? .1 : .91), viewport.Bottom - 22 - i * 10),
                12 + pulse * 95, 2 + pulse * 13);
            dc.Pop();
        }
    }

    private static void Spark(DrawingContext dc, double radius, Brush brush)
    {
        dc.DrawEllipse(brush, null, new Point(), radius * .27, radius * .27);
        dc.DrawRoundedRectangle(brush, null, new Rect(-radius, -.45, radius * 2, .9), .4, .4);
        dc.DrawRoundedRectangle(brush, null, new Rect(-.45, -radius, .9, radius * 2), .4, .4);
    }

    private void UpdateTimer()
    {
        bool run = IsLoaded && IsVisible && _enabled && _active && !_reduce;
        if (run && !_timer.IsEnabled) { _clock.Restart(); _timer.Start(); }
        else if (!run) Stop();
    }

    private void Stop() { _timer.Stop(); _clock.Reset(); }
    private static double Wrap(double value, double length) => (value % length + length) % length;
    private static double Random(int seed) { double value = Math.Sin(seed * 127.1 + 311.7) * 43758.5453; return value - Math.Floor(value); }
    private static SolidColorBrush Brush(byte r, byte g, byte b) { var brush = new SolidColorBrush(Color.FromRgb(r, g, b)); brush.Freeze(); return brush; }
    private static Pen Pen(byte r, byte g, byte b, double width) { var pen = new Pen(Brush(r, g, b), width); pen.Freeze(); return pen; }
    private static Brush Beam(byte r, byte g, byte b)
    {
        var brush = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, r, g, b), 0), new(Color.FromRgb(r, g, b), .16), new(Color.FromArgb(0, r, g, b), 1) }, new Point(0, 0), new Point(0, 1));
        brush.Freeze(); return brush;
    }
    private static Geometry Shape(bool petal)
    {
        var shape = new StreamGeometry();
        using (var context = shape.Open())
        {
            context.BeginFigure(new Point(0, -1), true, true);
            context.BezierTo(new Point(petal ? 1 : .85, -.7), new Point(.7, .55), new Point(0, 1), true, false);
            context.BezierTo(new Point(-.7, .5), new Point(-.7, -.55), new Point(0, -1), true, false);
        }
        shape.Freeze(); return shape;
    }
}
