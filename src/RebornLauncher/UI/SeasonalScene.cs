using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>
/// Lightweight vector scenery for the launcher. Keep this element behind the
/// artwork inside its own clipped stage, separate from all text and controls.
/// </summary>
public sealed class SeasonalScene : FrameworkElement
{
    private const double SceneWidth = 1000;
    private const double SceneHeight = 400;
    private const double FrameSeconds = 1.0 / 30.0;

    private static readonly Brush Pumpkin = MakeBrush(225, 111, 44, 208);
    private static readonly Brush PumpkinLight = MakeBrush(255, 177, 72, 190);
    private static readonly Brush Ghost = MakeBrush(218, 235, 255, 175);
    private static readonly Brush GhostEye = MakeBrush(30, 34, 50, 210);
    private static readonly Brush Snow = MakeBrush(220, 239, 255, 210);
    private static readonly Brush SnowSoft = MakeBrush(190, 220, 239, 130);
    private static readonly Brush Ice = MakeBrush(151, 202, 225, 145);
    private static readonly Brush SpringPink = MakeBrush(255, 161, 190, 195);
    private static readonly Brush SpringYellow = MakeBrush(255, 221, 139, 195);
    private static readonly Brush SpringGreen = MakeBrush(95, 171, 112, 170);
    private static readonly Brush SpringMint = MakeBrush(131, 210, 158, 165);
    private static readonly Brush EggBlue = MakeBrush(141, 207, 226, 185);
    private static readonly Brush EggCream = MakeBrush(248, 225, 171, 190);
    private static readonly Brush Sun = MakeBrush(255, 208, 112, 82);
    private static readonly Brush LeafGold = MakeBrush(222, 147, 55, 190);
    private static readonly Brush LeafRed = MakeBrush(190, 72, 64, 185);
    private static readonly Brush LeafYellow = MakeBrush(242, 190, 76, 185);
    private static readonly Brush ArceusGold = MakeBrush(255, 218, 135, 120);
    private static readonly Brush ArceusPale = MakeBrush(255, 244, 204, 90);
    private static readonly Brush ArceusHalo = MakeHaloBrush();
    private static readonly Pen SunRayPen = MakePen(255, 211, 117, 32, 3);
    private static readonly Pen EggOutline = MakePen(255, 255, 255, 100, 1);
    private static readonly Pen ArceusInnerPen = MakePen(255, 241, 205, 90, 1);
    private static readonly Pen PumpkinLine = MakePen(255, 196, 103, 175, 1.5);
    private static readonly Pen GhostLine = MakePen(235, 244, 255, 135, 1.7);
    private static readonly Pen SnowLine = MakePen(220, 239, 255, 130, 2);
    private static readonly Pen SpringLine = MakePen(124, 192, 128, 145, 2);
    private static readonly Pen WaterLine = MakePen(154, 229, 241, 150, 2);
    private static readonly Pen LeafLine = MakePen(255, 211, 124, 120, 1.5);
    private static readonly Pen GoldLine = MakePen(255, 231, 170, 135, 2);
    private static readonly DependencyProperty DaylightProgressProperty = DependencyProperty.Register(
        nameof(DaylightProgress), typeof(double), typeof(SeasonalScene),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly StreamGeometry GhostShape = BuildGhostShape();
    private static readonly StreamGeometry LeafShape = BuildLeafShape();
    private static readonly StreamGeometry PetalShape = BuildPetalShape();
    private static readonly StreamGeometry SnowEdgeLeft = BuildSnowEdge(80, 1032, 490, 1030);
    private static readonly StreamGeometry SnowEdgeRight = BuildSnowEdge(1430, 1030, 1860, 1034);

    private readonly DispatcherTimer _timer;
    private readonly ScaleTransform _scale = new();
    private string _themeId = "standard";
    private bool _reduceMotion;
    private bool _hasDaylightTarget;
    private bool _targetDaylight;
    private double DaylightProgress { get => (double)GetValue(DaylightProgressProperty); set => SetValue(DaylightProgressProperty, value); }
    public bool Edge { get; set; }
    private double _phase;

    public SeasonalScene()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(FrameSeconds)
        };
        _timer.Tick += OnFrame;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
    }

    /// <summary>Sets the seasonal palette and whether decorative motion is reduced.</summary>
    public void SetTheme(string id, bool reduceMotion, bool daylight = false, bool animateLighting = true)
    {
        string requested = (id ?? "standard").Trim().ToLowerInvariant();
        _themeId = requested switch
        {
            "halloween" or "winter" or "spring" or "summer" or "harvest" or "newyear" or "arceus" => requested,
            _ => "standard"
        };
        _reduceMotion = reduceMotion;
        SetDaylight(daylight, animateLighting && !_reduceMotion);
        UpdateTimerState();
        InvalidateVisual();
    }

    private void SetDaylight(bool daylight, bool animate)
    {
        if (_hasDaylightTarget && daylight == _targetDaylight)
        {
            if (!animate)
            {
                BeginAnimation(DaylightProgressProperty, null);
                DaylightProgress = daylight ? 1 : 0;
            }
            return;
        }
        double current = DaylightProgress;
        _targetDaylight = daylight;
        bool first = !_hasDaylightTarget;
        _hasDaylightTarget = true;
        double target = daylight ? 1 : 0;
        BeginAnimation(DaylightProgressProperty, null);
        DaylightProgress = target;
        if (!animate || first)
        {
            return;
        }
        BeginAnimation(DaylightProgressProperty, new DoubleAnimation(current, target, TimeSpan.FromSeconds(2.8))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        if (Edge) { DrawEdgeScene(drawingContext); return; }
        _scale.ScaleX = ActualWidth / SceneWidth;
        _scale.ScaleY = ActualHeight / SceneHeight;
        drawingContext.PushTransform(_scale);
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, SceneWidth, SceneHeight)));

        switch (_themeId)
        {
            case "standard": DrawNight(drawingContext); break;
            case "halloween": DrawHalloween(drawingContext); break;
            case "winter": DrawWinter(drawingContext); break;
            case "spring": DrawSpring(drawingContext); break;
            case "summer": DrawSummer(drawingContext); break;
            case "harvest": DrawHarvest(drawingContext); break;
            case "newyear": DrawNewYear(drawingContext); break;
            case "arceus": DrawArceus(drawingContext); break;
        }

        drawingContext.Pop();
        drawingContext.Pop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateTimerState();
    private void OnUnloaded(object sender, RoutedEventArgs e) => _timer.Stop();
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateTimerState();

    private void OnFrame(object? sender, EventArgs e)
    {
        _phase = (_phase + FrameSeconds) % 60;
        InvalidateVisual();
    }

    private void UpdateTimerState()
    {
        if (IsLoaded && IsVisible && !_reduceMotion )
        {
            if (!_timer.IsEnabled)
                _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void DrawEdgeScene(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 180, ActualHeight / 800));
        for (int i = 0; i < 12; i++)
        {
            double x = 12 + Pseudo(i * 13 + 4) * 150;
            double y = (Pseudo(i * 17 + 7) * 800 + _phase * (8 + i % 3 * 3)) % 800;
            switch (_themeId)
            {
                case "winter": DrawSnowflake(dc, x, y, 3 + i % 3, Snow); break;
                case "spring": DrawLeaf(dc, x, y, 14 + i % 3 * 3, SpringGreen, i * 20 + _phase * 10); break;
                case "harvest": DrawLeaf(dc, x, y, 18 + i % 3 * 3, i % 2 == 0 ? LeafGold : LeafRed, i * 25 + _phase * 12); break;
                case "summer": dc.DrawEllipse(null, WaterLine, new Point(x, 800 - y), 4 + i % 4 * 2, 4 + i % 4 * 2); break;
                case "halloween":
                    if (i < 3) DrawGhost(dc, x, y, 21, 29);
                    else dc.DrawEllipse(PumpkinLight, null, new Point(x, y), 2, 2);
                    break;
                case "arceus" or "newyear": DrawStar(dc, x, y, 3 + i % 3, ArceusPale); break;
                default:
                    dc.PushOpacity(1 - DaylightProgress);
                    dc.DrawEllipse(SnowSoft, null, new Point(x, y), 1.7, 1.7);
                    dc.Pop();
                    dc.PushOpacity(DaylightProgress);
                    DrawLeaf(dc, x, y, 10, SpringMint, i * 30);
                    dc.Pop();
                    break;
            }
        }
        dc.Pop();
    }

    private void DrawNight(DrawingContext dc)
    {
        dc.PushOpacity(0.7 * (1 - DaylightProgress));
        double moonRadius = 34 - DaylightProgress * 8;
        dc.DrawEllipse(ArceusPale, null, new Point(845, 78), moonRadius, moonRadius);
        dc.DrawEllipse(MakeBrush(14, 12, 24, 255), null, new Point(860 - DaylightProgress * 5, 68), moonRadius * .85, moonRadius * .85);
        for (int i = 0; i < 24; i++)
        {
            double x = Pseudo(i * 17 + 8) * SceneWidth;
            double y = 18 + Pseudo(i * 13 + 5) * 200;
            dc.PushOpacity(0.3 + (Math.Sin(_phase + i) + 1) * 0.3);
            dc.DrawEllipse(SnowSoft, null, new Point(x, y), 1.6 + i % 2, 1.6 + i % 2);
            dc.Pop();
        }
        dc.Pop();
        dc.PushOpacity(DaylightProgress);
        dc.DrawEllipse(MakeBrush(255, 198, 116, 82), null, new Point(845, 78), 34 + DaylightProgress * 20, 34 + DaylightProgress * 20);
        for (int i = 0; i < 8; i++) DrawFlower(dc, 40 + i * 134, 360, 12, SpringPink);
        dc.Pop();
    }

    private void DrawHalloween(DrawingContext dc)
    {
        DrawPumpkin(dc, 135, 325, 115);
        DrawPumpkin(dc, 880, 330, 82);
        DrawPumpkin(dc, 230, 345, 53);
        DrawGhost(dc, 135 + Drift(12, 1.2), 72, 57, 65);
        DrawGhost(dc, 820 - Drift(16, 1.4), 105, 49, 58);
        for (int i = 0; i < 12; i++)
        {
            double x = 70 + Pseudo(i * 17 + 2) * 860;
            double y = 30 + (Pseudo(i * 11 + 5) * 300 + _phase * 10) % 300;
            dc.PushOpacity(0.2 + (Math.Sin(_phase + i) + 1) * 0.2);
            dc.DrawEllipse(PumpkinLight, null, new Point(x, y), 2.8, 2.8);
            dc.Pop();
        }
    }

    private void DrawWinter(DrawingContext dc)
    {
        dc.PushOpacity(0.35);
        dc.DrawEllipse(Ice, null, new Point(140, 385), 280, 33);
        dc.DrawEllipse(Ice, null, new Point(900, 390), 370, 40);
        dc.Pop();
        for (int i = 0; i < 44; i++)
        {
            double y = (Pseudo(i * 29 + 3) * SceneHeight + _phase * (10 + i % 5 * 4)) % SceneHeight;
            double x = Pseudo(i * 17 + 5) * SceneWidth + Math.Sin(_phase * 0.72 + i * 1.7) * 12;
            DrawSnowflake(dc, x, y, 2.2 + i % 4 * 1.2, i % 3 == 0 ? Snow : SnowSoft);
        }
    }

    private void DrawSpring(DrawingContext dc)
    {
        DrawEgg(dc, 128, 320, 56, 72, EggBlue, 0.12);
        DrawEgg(dc, 870, 335, 45, 59, EggCream, -0.14);
        DrawEgg(dc, 200, 348, 31, 42, EggCream, 0.3);
        for (int i = 0; i < 18; i++)
        {
            double x = 25 + i * 57;
            DrawFlower(dc, x, 365 + Math.Sin(i * 1.7) * 12, 9 + i % 3 * 2, i % 2 == 0 ? SpringPink : SpringYellow);
            double px = (Pseudo(i * 11 + 8) * 1000 + Math.Sin(_phase * .5 + i) * 25) % 1000;
            double py = (Pseudo(i * 23 + 2) * 400 + _phase * (8 + i % 3)) % 400;
            if (i % 3 == 0) DrawLeaf(dc, px, py, 18, SpringMint, _phase * 12 + i * 22);
            else
            {
                dc.PushTransform(new TranslateTransform(px, py));
                dc.PushTransform(new RotateTransform(_phase * 10 + i * 26));
                dc.DrawGeometry(SpringPink, null, PetalShape);
                dc.Pop(); dc.Pop();
            }
        }
    }

    private void DrawSummer(DrawingContext dc)
    {
        dc.PushOpacity(1 - DaylightProgress);
        {
            double radiusY = 36 * (ActualWidth / SceneWidth) / (ActualHeight / SceneHeight);
            dc.DrawEllipse(ArceusPale, null, new Point(140, 85), 36 * (1 - DaylightProgress * .22), radiusY * (1 - DaylightProgress * .22));
            for (int i = 0; i < 12; i++)
                DrawStar(dc, 220 + Pseudo(i * 17 + 2) * 720, 25 + Pseudo(i * 11 + 7) * 160, 2 + i % 2, SnowSoft);
        }
        dc.Pop();
        dc.PushOpacity(DaylightProgress);
        double sunRadius = 36 + DaylightProgress * 18;
        dc.DrawEllipse(MakeBrush(255, 198, 116, 72), null, new Point(140, 85), sunRadius, sunRadius);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6 + _phase * 0.05;
            dc.DrawLine(SunRayPen, new Point(140 + Math.Cos(a) * (sunRadius + 12), 85 + Math.Sin(a) * (sunRadius + 12)), new Point(140 + Math.Cos(a) * (sunRadius + 27), 85 + Math.Sin(a) * (sunRadius + 27)));
        }
        dc.Pop();
        for (int i = 0; i < 4; i++) DrawWave(dc, 0, 325 + i * 19, 1000, 9 - i, i * 1.7);
        for (int i = 0; i < 16; i++)
        {
            double x = 45 + Pseudo(i * 17 + 4) * 920;
            double y = 375 - (Pseudo(i * 13 + 2) * 300 + _phase * (13 + i % 4)) % 300;
            dc.DrawEllipse(null, WaterLine, new Point(x, y), 3 + i % 4 * 2, 3 + i % 4 * 2);
        }
    }

    private void DrawHarvest(DrawingContext dc)
    {
        for (int i = 0; i < 24; i++)
        {
            double x = Pseudo(i * 11 + 10) * 1000 + Math.Sin(_phase * .7 + i) * 22;
            double y = (Pseudo(i * 7 + 16) * SceneHeight + _phase * (13 + i % 4 * 5)) % SceneHeight;
            DrawLeaf(dc, x, y, 19 + i % 5 * 4, i % 3 == 0 ? LeafRed : i % 3 == 1 ? LeafGold : LeafYellow, Math.Sin(_phase * .8 + i) * 32 + i * 13);
        }
    }

    private void DrawNewYear(DrawingContext dc)
    {
        for (int i = 0; i < 18; i++)
            DrawStar(dc, Pseudo(i * 17) * 1000, (Pseudo(i * 7) * 380 + _phase * 12) % 380, 2 + i % 3, ArceusPale);
    }

    private void DrawArceus(DrawingContext dc)
    {
        Point center = new(500, 195);
        dc.DrawEllipse(ArceusHalo, null, center, 310, 170);
        dc.DrawEllipse(null, GoldLine, center, 230, 155);
        dc.DrawEllipse(null, ArceusInnerPen, center, 250, 172);
        for (int i = 0; i < 24; i++)
        {
            double a = i * Math.PI / 12 + _phase * .05;
            double x = center.X + Math.Cos(a) * 270;
            double y = center.Y + Math.Sin(a) * 178;
            DrawStar(dc, x, y, i % 4 == 0 ? 10 : 4, ArceusPale);
        }
    }

    private void DrawPumpkin(DrawingContext dc, double x, double y, double size)
    {
        double w = size, h = size * 0.77;
        dc.DrawEllipse(Pumpkin, PumpkinLine, new Point(x, y), w * 0.43, h * 0.43);
        dc.DrawEllipse(Pumpkin, PumpkinLine, new Point(x - w * 0.22, y), w * 0.25, h * 0.37);
        dc.DrawEllipse(Pumpkin, PumpkinLine, new Point(x + w * 0.22, y), w * 0.25, h * 0.37);
        dc.DrawLine(PumpkinLine, new Point(x, y - h * 0.42), new Point(x + 5, y - h * 0.64));
        dc.DrawEllipse(PumpkinLight, null, new Point(x - 4, y - h * 0.63), 6, 4);
        dc.DrawGeometry(PumpkinLight, null, Triangle(x - w * 0.22, y - 7, 13, 14));
        dc.DrawGeometry(PumpkinLight, null, Triangle(x + w * 0.11, y - 7, 13, 14));
        dc.DrawGeometry(PumpkinLight, null, Triangle(x - 8, y + 8, 16, 13));
    }

    private void DrawGhost(DrawingContext dc, double x, double y, double width, double height)
    {
        double floatY = Math.Sin(_phase * 1.4 + x) * 7;
        dc.PushTransform(new TranslateTransform(x, y + floatY));
        dc.PushTransform(new ScaleTransform(width / 70, height / 76));
        dc.DrawGeometry(Ghost, GhostLine, GhostShape);
        dc.DrawEllipse(GhostEye, null, new Point(27, 31), 3.1, 4.6);
        dc.DrawEllipse(GhostEye, null, new Point(44, 31), 3.1, 4.6);
        dc.Pop(); dc.Pop();
    }

    private static void DrawSnowflake(DrawingContext dc, double x, double y, double r, Brush brush)
    {
        dc.DrawLine(SnowLine, new Point(x - r, y), new Point(x + r, y));
        dc.DrawLine(SnowLine, new Point(x, y - r), new Point(x, y + r));
        dc.DrawEllipse(brush, null, new Point(x, y), Math.Max(1.2, r * 0.38), Math.Max(1.2, r * 0.38));
    }

    private static void DrawEgg(DrawingContext dc, double x, double y, double w, double h, Brush fill, double angle)
    {
        dc.PushTransform(new RotateTransform(angle * 57.2958, x, y));
        dc.DrawEllipse(fill, EggOutline, new Point(x, y), w / 2, h / 2);
        dc.DrawEllipse(SpringPink, null, new Point(x - 8, y - 3), 5.2, 3.1);
        dc.DrawEllipse(SpringYellow, null, new Point(x + 9, y + 7), 4.8, 3.2);
        dc.DrawEllipse(SpringGreen, null, new Point(x + 2, y - 14), 3.2, 4.4);
        dc.DrawEllipse(SpringPink, null, new Point(x - 4, y + 13), 3.6, 2.5);
        dc.Pop();
    }

    private static void DrawFlower(DrawingContext dc, double x, double y, double r, Brush petal)
    {
        for (int i = 0; i < 5; i++)
        {
            double a = i * Math.PI * 2 / 5;
            dc.DrawEllipse(petal, null, new Point(x + Math.Cos(a) * r * 0.62, y + Math.Sin(a) * r * 0.62), r * 0.42, r * 0.42);
        }
        dc.DrawEllipse(SpringYellow, null, new Point(x, y), r * 0.28, r * 0.28);
    }

    private void DrawWave(DrawingContext dc, double x, double y, double width, double amplitude, double offset)
    {
        double phase = _phase * 1.25 + offset;
        Point previous = new(x, y + Math.Sin(phase) * amplitude);
        for (int i = 1; i <= 24; i++)
        {
            double px = x + width * i / 24;
            double py = y + Math.Sin(phase + i * 0.39) * amplitude;
            dc.DrawLine(WaterLine, previous, new Point(px, py));
            previous = new Point(px, py);
        }
    }

    private static void DrawStar(DrawingContext dc, double x, double y, double r, Brush brush)
    {
        dc.DrawLine(GoldLine, new Point(x - r, y), new Point(x + r, y));
        dc.DrawLine(GoldLine, new Point(x, y - r), new Point(x, y + r));
        dc.DrawLine(GoldLine, new Point(x - r * 0.58, y - r * 0.58), new Point(x + r * 0.58, y + r * 0.58));
        dc.DrawLine(GoldLine, new Point(x + r * 0.58, y - r * 0.58), new Point(x - r * 0.58, y + r * 0.58));
        dc.DrawEllipse(brush, null, new Point(x, y), 3.2, 3.2);
    }

    private static void DrawLeaf(DrawingContext dc, double x, double y, double size, Brush fill, double rotation)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new RotateTransform(rotation));
        dc.PushTransform(new ScaleTransform(size / 22, size / 22));
        dc.DrawGeometry(fill, LeafLine, LeafShape);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    private double Drift(double distance, double rate) => Math.Sin(_phase * rate) * distance;

    private static double Pseudo(int seed)
    {
        double v = Math.Sin(seed * 127.1 + 311.7) * 43758.5453123;
        return v - Math.Floor(v);
    }

    private static StreamGeometry BuildGhostShape()
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(8, 72), true, true);
            c.BezierTo(new Point(3, 58), new Point(7, 23), new Point(24, 12), true, false);
            c.BezierTo(new Point(37, 1), new Point(61, 8), new Point(65, 28), true, false);
            c.BezierTo(new Point(70, 44), new Point(64, 61), new Point(67, 72), true, false);
            c.LineTo(new Point(55, 64), true, false);
            c.LineTo(new Point(43, 74), true, false);
            c.LineTo(new Point(30, 64), true, false);
            c.LineTo(new Point(18, 74), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry BuildLeafShape()
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(0, 0), true, true);
            c.BezierTo(new Point(3, -11), new Point(14, -17), new Point(23, -12), true, false);
            c.BezierTo(new Point(23, -2), new Point(15, 9), new Point(0, 0), true, false);
            c.LineTo(new Point(18, -10), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry BuildPetalShape()
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(0, 0), true, true);
            c.BezierTo(new Point(2, -7), new Point(9, -10), new Point(12, -4), true, false);
            c.BezierTo(new Point(11, 2), new Point(4, 7), new Point(0, 0), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry BuildSnowEdge(double left, double y1, double right, double y2)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(left, y1), false, false);
            c.BezierTo(new Point(left + 120, y1 + 16), new Point(left + 250, y2 - 12), new Point(right, y2), false, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry Triangle(double x, double y, double width, double height)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(x, y), true, true);
            c.LineTo(new Point(x + width / 2, y + height), true, false);
            c.LineTo(new Point(x + width, y), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Brush MakeBrush(byte r, byte g, byte b, byte a)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen MakePen(byte r, byte g, byte b, byte a, double thickness)
    {
        var pen = new Pen(MakeBrush(r, g, b, a), thickness);
        pen.Freeze();
        return pen;
    }

    private static Brush MakeHaloBrush()
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(38, 255, 223, 142), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 255, 204, 101), 0.68));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 204, 101), 1));
        brush.Freeze();
        return brush;
    }
}
