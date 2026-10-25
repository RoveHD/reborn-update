using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>
/// Lightweight vector scenery for the launcher. Keep this element behind the
/// content: its palette is deliberately translucent and its decorations stay
/// around the edges of a 16:9 design canvas.
/// </summary>
public sealed class SeasonalScene : FrameworkElement
{
    private const double SceneWidth = 1920;
    private const double SceneHeight = 1080;
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
    private static readonly Brush FireworkBlue = MakeBrush(120, 199, 255, 155);
    private static readonly Brush FireworkGold = MakeBrush(255, 214, 119, 170);
    private static readonly Brush ArceusGold = MakeBrush(255, 218, 135, 120);
    private static readonly Brush ArceusPale = MakeBrush(255, 244, 204, 90);
    private static readonly Brush ArceusHalo = MakeHaloBrush();
    private static readonly Pen SunRayPen = MakePen(255, 211, 117, 32, 3);
    private static readonly Pen FireworkRayPen = MakePen(255, 229, 205, 100, 1.7);
    private static readonly Pen EggOutline = MakePen(255, 255, 255, 100, 1);
    private static readonly Pen ArceusInnerPen = MakePen(255, 241, 205, 90, 1);
    private static readonly Pen PumpkinLine = MakePen(255, 196, 103, 175, 1.5);
    private static readonly Pen GhostLine = MakePen(235, 244, 255, 135, 1.7);
    private static readonly Pen SnowLine = MakePen(220, 239, 255, 130, 2);
    private static readonly Pen SpringLine = MakePen(124, 192, 128, 145, 2);
    private static readonly Pen WaterLine = MakePen(154, 229, 241, 150, 2);
    private static readonly Pen LeafLine = MakePen(255, 211, 124, 120, 1.5);
    private static readonly Pen GoldLine = MakePen(255, 231, 170, 135, 2);

    private static readonly StreamGeometry GhostShape = BuildGhostShape();
    private static readonly StreamGeometry LeafShape = BuildLeafShape();
    private static readonly StreamGeometry PetalShape = BuildPetalShape();
    private static readonly StreamGeometry SnowEdgeLeft = BuildSnowEdge(80, 1032, 490, 1030);
    private static readonly StreamGeometry SnowEdgeRight = BuildSnowEdge(1430, 1030, 1860, 1034);

    private readonly DispatcherTimer _timer;
    private readonly ScaleTransform _scale = new();
    private string _themeId = "standard";
    private bool _reduceMotion;
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
    public void SetTheme(string id, bool reduceMotion)
    {
        string requested = (id ?? "standard").Trim().ToLowerInvariant();
        _themeId = requested switch
        {
            "halloween" or "winter" or "spring" or "summer" or "harvest" or "newyear" or "arceus" => requested,
            _ => "standard"
        };
        _reduceMotion = reduceMotion;
        _phase = 0;
        UpdateTimerState();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0 || _themeId == "standard")
            return;

        _scale.ScaleX = ActualWidth / SceneWidth;
        _scale.ScaleY = ActualHeight / SceneHeight;
        drawingContext.PushTransform(_scale);
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, SceneWidth, SceneHeight)));

        switch (_themeId)
        {
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
        if (IsLoaded && IsVisible && !_reduceMotion && _themeId != "standard")
        {
            if (!_timer.IsEnabled)
                _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void DrawHalloween(DrawingContext dc)
    {
        DrawPumpkin(dc, 455, 146, 78);
        DrawPumpkin(dc, 1362, 145, 55);
        DrawGhost(dc, 556 + Drift(4, 1.2), 170, 45, 50);
        DrawGhost(dc, 1365 - Drift(4, 1.4), 239, 40, 45);
    }

    private void DrawWinter(DrawingContext dc)
    {
        dc.PushOpacity(0.18);
        dc.DrawRectangle(Ice, null, new Rect(0, 1032, SceneWidth, 48));
        dc.DrawEllipse(Ice, null, new Point(-75, 1052), 500, 18);
        dc.DrawEllipse(Ice, null, new Point(1995, 1052), 500, 18);
        dc.DrawGeometry(null, SnowLine, SnowEdgeLeft);
        dc.DrawGeometry(null, SnowLine, SnowEdgeRight);
        dc.Pop();

        for (int i = 0; i < 44; i++)
        {
            double baseX = Pseudo(i * 17 + 5) * SceneWidth;
            double y = (Pseudo(i * 29 + 3) * SceneHeight + _phase * (18 + (i % 5) * 5)) % SceneHeight;
            double x = baseX + Math.Sin(_phase * 0.72 + i * 1.7) * (7 + i % 9);
            double centerFade = x > 480 && x < 1450 && y > 160 && y < 870 ? 0.12 : 1;
            dc.PushOpacity(centerFade);
            DrawSnowflake(dc, x, y, 3.4 + (i % 4) * 1.3, i % 3 == 0 ? Snow : SnowSoft);
            dc.Pop();
        }
    }

    private void DrawSpring(DrawingContext dc)
    {
        DrawEgg(dc, 485, 145, 39, 50, EggBlue, 0.12);
        DrawEgg(dc, 1365, 150, 34, 44, EggCream, -0.14);
        for (int i = 0; i < 10; i++)
        {
            double x = i < 5 ? 405 + i * 49 : 1305 + (i - 5) * 19;
            double y = 203 + (i % 3) * 29 + Math.Sin(_phase * 0.7 + i) * 2;
            DrawFlower(dc, x, y, 8 + i % 3 * 1.5, i % 2 == 0 ? SpringPink : SpringYellow);
        }
        for (int i = 0; i < 9; i++)
        {
            double x = i % 2 == 0 ? 405 + Pseudo(i * 11 + 8) * 230 : 1305 + Pseudo(i * 11 + 8) * 72;
            double y = 105 + Pseudo(i * 23 + 2) * 150 + _phase * (2 + i % 3);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(Math.Sin(_phase + i) * 20));
            dc.DrawGeometry(SpringPink, null, PetalShape);
            dc.Pop(); dc.Pop();
            dc.DrawEllipse(SpringYellow, null, new Point(x + 4, y + 3), 2.2, 2.2);
        }

        // Soft green leaves drift in the open gap between the logo, artwork and divider.
        // Their movement is bounded so they never fall through the controls below.
        for (int i = 0; i < 7; i++)
        {
            double x = i % 2 == 0
                ? 420 + Pseudo(i * 13 + 4) * 42
                : 1320 + Pseudo(i * 13 + 4) * 22;
            x += Math.Sin(_phase * 0.45 + i * 1.7) * 7;
            double y = 106 + Pseudo(i * 17 + 9) * 112 + Math.Sin(_phase * 0.65 + i) * 8;
            DrawLeaf(dc, x, y, 15 + i % 3 * 3, SpringMint, Math.Sin(_phase * 0.55 + i) * 14 + i * 21);
        }
    }

    private void DrawSummer(DrawingContext dc)
    {
        dc.DrawEllipse(Sun, null, new Point(520, 135), 80, 80);
        for (int i = 0; i < 8; i++)
        {
            double angle = (-1.05 + i * 0.14) + Math.Sin(_phase * 0.15) * 0.025;
            double x1 = 520 + Math.Cos(angle) * 100;
            double y1 = 135 + Math.Sin(angle) * 100;
            double x2 = 520 + Math.Cos(angle) * 200;
            double y2 = 135 + Math.Sin(angle) * 200;
            dc.DrawLine(SunRayPen, new Point(x1, y1), new Point(x2, y2));
        }

        DrawWave(dc, 410, 352, 220, 10, 0.1);
        DrawWave(dc, 1295, 340, 100, 8, 1.8);
        DrawWave(dc, 430, 386, 190, 7, 2.8);
        for (int i = 0; i < 8; i++)
        {
            double x = 430 + i * 28;
            double bob = Math.Sin(_phase * 1.4 + i * 1.9);
            double y = 292 + (i % 3) * 19 + bob * 4;
            double r = 2.5 + i % 3;
            dc.DrawEllipse(null, WaterLine, new Point(x, y), r, r);
        }
    }

    private void DrawHarvest(DrawingContext dc)
    {
        for (int i = 0; i < 18; i++)
        {
            bool inLeftEdge = i % 2 == 0;
            double x = inLeftEdge
                ? 18 + Pseudo(i + 10) * 18
                : 1882 + Pseudo(i + 31) * 20;
            double y = (Pseudo(i * 7 + 16) * SceneHeight + _phase * (18 + i % 4 * 5)) % SceneHeight;
            double size = 20 + i % 5 * 3;
            Brush leaf = i % 3 == 0 ? LeafRed : i % 3 == 1 ? LeafGold : LeafYellow;
            DrawLeaf(dc, x, y, size, leaf, Math.Sin(_phase * 0.8 + i) * 32 + i * 13);
        }
    }

    private void DrawNewYear(DrawingContext dc)
    {
        DrawFirework(dc, 455, 169, 43, FireworkGold, 0.2);
        DrawFirework(dc, 1362, 173, 35, FireworkBlue, 1.3);
    }

    private void DrawArceus(DrawingContext dc)
    {
        Point haloCenter = new(520, 150);
        dc.DrawEllipse(ArceusHalo, null, haloCenter, 108, 108);
        dc.DrawEllipse(null, GoldLine, haloCenter, 83, 83);
        dc.DrawEllipse(null, ArceusInnerPen, haloCenter, 94, 94);
        dc.DrawEllipse(ArceusGold, null, haloCenter, 5, 5);
        for (int i = 0; i < 16; i++)
        {
            double a = i * Math.PI / 8 + _phase * 0.08;
            double r1 = 96 + (i % 3) * 6;
            double r2 = r1 + 8 + i % 4 * 2;
            dc.DrawLine(i % 4 == 0 ? GoldLine : SnowLine,
                new Point(haloCenter.X + Math.Cos(a) * r1, haloCenter.Y + Math.Sin(a) * r1),
                new Point(haloCenter.X + Math.Cos(a) * r2, haloCenter.Y + Math.Sin(a) * r2));
        }
        for (int i = 0; i < 8; i++)
        {
            double x = 410 + Pseudo(i * 9 + 14) * 220;
            double y = 95 + Pseudo(i * 7 + 25) * 125;
            DrawStar(dc, x, y, 4 + i % 3 * 2, ArceusPale);
        }
        DrawStar(dc, 1360, 150, 10, ArceusPale);
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

    private static void DrawFirework(DrawingContext dc, double x, double y, double radius, Brush color, double offset)
    {
        for (int i = 0; i < 16; i++)
        {
            double a = i * Math.PI / 8 + offset;
            double inner = radius * 0.32;
            double outer = radius * (0.72 + (i % 3) * 0.09);
            Point start = new(x + Math.Cos(a) * inner, y + Math.Sin(a) * inner);
            Point end = new(x + Math.Cos(a) * outer, y + Math.Sin(a) * outer);
            dc.DrawLine(FireworkRayPen, start, end);
            dc.DrawEllipse(color, null, end, 2.6, 2.6);
        }
        dc.DrawEllipse(FireworkGold, null, new Point(x, y), 4, 4);
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
