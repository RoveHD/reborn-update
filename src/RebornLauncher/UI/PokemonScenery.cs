using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PokemonRebornLauncher.UI;

/// <summary>Foreground set dressing that makes the artwork sit inside the season's scene.</summary>
public sealed class PokemonScenery : FrameworkElement
{
    private const double WidthBase = 1000;
    private const double HeightBase = 400;
    private static readonly DependencyProperty DaylightProgressProperty = DependencyProperty.Register(nameof(DaylightProgress), typeof(double), typeof(PokemonScenery), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    private string _theme = "standard";
    private bool _hasDaylightTarget;
    private bool _targetDaylight;
    private double DaylightProgress { get => (double)GetValue(DaylightProgressProperty); set => SetValue(DaylightProgressProperty, value); }
    private Rect[] _actors = [];
    public void SetActorBounds(IEnumerable<Rect> bounds)
    {
        _actors = bounds.ToArray();
        InvalidateVisual();
    }

    public bool Edge { get; set; }
    public bool RightEdge { get; set; }

    public void SetTheme(string theme, bool daylight, bool animate = false)
    {
        _theme = theme;
        if (!_hasDaylightTarget || daylight != _targetDaylight || !animate)
        {
            double current = DaylightProgress;
            _targetDaylight = daylight;
            bool first = !_hasDaylightTarget;
            _hasDaylightTarget = true;
            double target = daylight ? 1 : 0;
            BeginAnimation(DaylightProgressProperty, null);
            DaylightProgress = target;
            if (animate && !first)
            {
                BeginAnimation(DaylightProgressProperty, new DoubleAnimation(current, target, TimeSpan.FromSeconds(2.8)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop }, HandoffBehavior.SnapshotAndReplace);
            }
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        // Bound the drawing before the opacity mask: snowbanks extend below the floor.
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        if (Edge)
        {
            DrawEdge(dc);
            dc.Pop();
            return;
        }
        // Soft contact pools follow the actors instead of pinning every theme
        // to identical corner stickers. Their radial edges dissolve into the set.
        foreach (var actor in _actors)
        {
            Color tint = _theme switch
            {
                "winter" => Color.FromArgb(115, 140, 191, 211),
                "summer" => Color.FromArgb(105, 66, 165, 184),
                "spring" => Color.FromArgb(110, 110, 155, 88),
                "harvest" => Color.FromArgb(115, 165, 105, 49),
                "arceus" or "newyear" => Color.FromArgb(100, 211, 180, 103),
                _ => Color.FromArgb(95, 80, 65, 100)
            };
            var pool = new RadialGradientBrush(tint, Color.FromArgb(0, tint.R, tint.G, tint.B));
            dc.DrawEllipse(pool, null, new Point(actor.X + actor.Width / 2, actor.Bottom - 4), actor.Width * .42, Math.Max(7, actor.Height * .055));
        }
        dc.PushTransform(new ScaleTransform(ActualWidth / WidthBase, ActualHeight / HeightBase));
        var shade = Brush(18, 21, 27, 145);
        var muted = Brush(44, 54, 65, 210);
        switch (_theme)
        {
            case "winter": DrawWinter(dc); break;
            case "halloween": DrawHalloween(dc); break;
            case "spring": DrawSpring(dc); break;
            case "summer": DrawSummer(dc); break;
            case "harvest": DrawHarvest(dc); break;
            case "newyear": DrawNewYear(dc); break;
            case "arceus": DrawArceus(dc); break;
            default: DrawStandard(dc, DaylightProgress, shade, muted); break;
        }
        dc.Pop();
        dc.Pop();
    }

    private void DrawEdge(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        // Use one uniform prop scale based on the narrow edge column. The old
        // full-scene coordinates were stretched to the entire tall side gutter.
        double s = Math.Clamp(Math.Min(w / 140, h / 180), .2, 1);
        double x = w * (RightEdge ? .78 : .32);
        double ground = h - 2 * s;
        Brush fill = _theme switch
        {
            "winter" => Brush(213, 234, 246, 248),
            "halloween" => Brush(60, 50, 65, 240),
            "spring" => Brush(86, 135, 71, 235),
            "summer" => Brush(40, 126, 143, 230),
            "harvest" => Brush(151, 94, 37, 235),
            "newyear" => Brush(33, 42, 74, 235),
            "arceus" => Brush(134, 112, 58, 230),
            _ => Blend(Brush(49, 44, 66, 230), Brush(91, 132, 94, 225), DaylightProgress)
        };
        dc.DrawEllipse(fill, null, new Point(x, ground + 8 * s), 82 * s, 25 * s);
        dc.DrawEllipse(Brush(20, 22, 28, 110), null, new Point(x, ground + 10 * s), 48 * s, 7 * s);
        if (_theme == "halloween")
        {
            dc.DrawRoundedRectangle(Brush(65, 61, 76, 250), Pen(125, 117, 136, 230, Math.Max(.6, s)), new Rect(x - 24 * s, ground - 28 * s, 18 * s, 32 * s), 8 * s, 8 * s);
            dc.DrawEllipse(Brush(213, 100, 42, 250), null, new Point(x + 19 * s, ground - 5 * s), 14 * s, 10 * s);
            dc.DrawEllipse(Brush(255, 207, 105, 248), null, new Point(x + 19 * s, ground - 5 * s), 3.5 * s, 2.5 * s);
        }
        else if (_theme == "winter")
        {
            dc.DrawEllipse(Brush(245, 250, 253, 255), null, new Point(x, ground - 3 * s), 48 * s, 13 * s);
            Gift(dc, x - 28 * s, ground + 1 * s, 24 * s, Brush(173, 73, 89, 245), Brush(245, 215, 153, 255));
        }
        else if (_theme == "spring")
        {
            Flower(dc, x + 20 * s, ground - 8 * s, 11 * s, Brush(240, 166, 188, 245));
            Egg(dc, x - 18 * s, ground - 7 * s, 12 * s, 16 * s, Brush(161, 210, 229, 248));
        }
        else if (_theme == "summer")
        {
            dc.DrawLine(Pen(171, 230, 231, 220, Math.Max(.6, 1.5 * s)), new Point(x - 36 * s, ground - 7 * s), new Point(x + 38 * s, ground - 8 * s));
            Shell(dc, x + 15 * s, ground - 8 * s, 8 * s);
        }
        else if (_theme == "harvest")
        {
            for (int i = 0; i < 3; i++) Leaf(dc, x + (i - 1) * 10 * s, ground - (5 + i % 2 * 3) * s, Brush(222, (byte)(152 + i * 8), 60, 240));
        }
        else if (_theme == "newyear" || _theme == "arceus")
        {
            dc.DrawEllipse(Brush(247, 215, 139, 205), null, new Point(x + 17 * s, ground - 14 * s), 2.5 * s, 2.5 * s);
            dc.DrawEllipse(Brush(247, 215, 139, 175), null, new Point(x - 19 * s, ground - 21 * s), 1.8 * s, 1.8 * s);
        }
    }

    private static void DrawWinter(DrawingContext dc)
    {
        dc.DrawEllipse(Brush(169, 207, 226, 238), null, new Point(90, 401), 310, 68);
        dc.DrawEllipse(Brush(207, 230, 242, 248), null, new Point(900, 408), 380, 80);
        dc.DrawEllipse(Brush(241, 247, 250, 235), null, new Point(738, 391), 175, 35);
        Gift(dc, 184, 371, 68, Brush(173, 73, 89, 245), Brush(245, 215, 153, 255));
        for (int i = 0; i < 5; i++)
            dc.DrawEllipse(Brush(255, 255, 255, (byte)(90 + i * 18)), null, new Point(575 + i * 66, 353 + (i % 2) * 14), 3, 3);
    }

    private static void DrawHalloween(DrawingContext dc)
    {
        // Low, translucent ground fog and a foreground tombstone/pumpkin hide the lower edge of the art.
        dc.DrawEllipse(Brush(152, 143, 173, 82), null, new Point(660, 385), 410, 36);
        dc.DrawRoundedRectangle(Brush(72, 71, 86, 248), Pen(138, 128, 147, 245, 2), new Rect(128, 316, 102, 104), 42, 42);
        dc.DrawLine(Pen(183, 174, 191, 210, 4), new Point(179, 342), new Point(179, 389));
        dc.DrawLine(Pen(183, 174, 191, 210, 4), new Point(160, 361), new Point(198, 361));
        Pumpkin(dc, 851, 370, 65);
        dc.DrawEllipse(Brush(13, 9, 23, 100), null, new Point(688, 370), 145, 18);
    }

    private static void DrawSpring(DrawingContext dc)
    {
        Grass(dc, Brush(82, 133, 76, 210), 0, 12);
        dc.DrawEllipse(Brush(89, 136, 71, 225), null, new Point(105, 402), 222, 62);
        Egg(dc, 158, 373, 40, 52, Brush(161, 210, 229, 248));
        Flower(dc, 96, 365, 12, Brush(235, 146, 176, 245));
        Flower(dc, 810, 374, 10, Brush(246, 205, 107, 245));
        dc.DrawEllipse(Brush(96, 147, 78, 210), null, new Point(932, 401), 182, 52);
    }

    private static void DrawSummer(DrawingContext dc)
    {
        dc.DrawEllipse(Brush(33, 126, 143, 205), null, new Point(80, 407), 280, 60);
        dc.DrawEllipse(Brush(62, 166, 178, 205), null, new Point(881, 400), 360, 58);
        for (int i = 0; i < 3; i++)
            dc.DrawLine(Pen(171, 230, 231, 205, 2), new Point(650 + i * 42, 345 + i * 13), new Point(731 + i * 42, 339 + i * 13));
        dc.DrawEllipse(Brush(238, 227, 176, 235), null, new Point(189, 392), 95, 18);
        Shell(dc, 225, 381, 17);
    }

    private static void DrawHarvest(DrawingContext dc)
    {
        dc.DrawEllipse(Brush(132, 82, 37, 222), null, new Point(56, 408), 280, 77);
        dc.DrawEllipse(Brush(186, 123, 53, 225), null, new Point(873, 407), 365, 79);
        for (int i = 0; i < 10; i++)
        {
            double x = 700 + i * 27;
            double y = 356 + (i % 3) * 10;
            Leaf(dc, x, y, i % 2 == 0 ? Brush(207, 82, 47, 235) : Brush(231, 163, 61, 240));
        }
    }

    private static void DrawNewYear(DrawingContext dc)
    {
        // A low ridge suggests the rooftop where the small companions watch the fireworks.
        var roof = new StreamGeometry();
        using (var g = roof.Open())
        {
            g.BeginFigure(new Point(0, 399), true, true);
            g.LineTo(new Point(153, 366), true, false);
            g.LineTo(new Point(271, 399), true, false);
            g.LineTo(new Point(1000, 399), true, false);
        }
        roof.Freeze();
        dc.DrawGeometry(Brush(34, 42, 75, 218), null, roof);
        for (int i = 0; i < 7; i++)
        {
            double x = 684 + i * 38;
            dc.DrawLine(Pen(247, 215, 139, 208, 2), new Point(x, 363), new Point(x + 3, 382));
            dc.DrawEllipse(Brush(248, 210, 119, (byte)(150 + (i % 2) * 70)), null, new Point(x + 3, 385), 4, 4);
        }
    }

    private static void DrawArceus(DrawingContext dc)
    {
        dc.DrawEllipse(Brush(123, 104, 53, 210), null, new Point(838, 405), 344, 54);
        dc.DrawEllipse(Brush(222, 195, 122, 210), null, new Point(840, 380), 198, 23);
        for (int i = 0; i < 5; i++)
        {
            double x = 755 + i * 43;
            dc.DrawEllipse(Brush(250, 229, 170, 220), null, new Point(x, 347 + (i % 2) * 9), 2.5, 2.5);
        }
    }

    private static void DrawStandard(DrawingContext dc, double daylight, Brush shade, Brush muted)
    {
        dc.DrawEllipse(Blend(muted, Brush(98, 136, 107, 200), daylight), null, new Point(68, 403), 250, 70);
        dc.DrawEllipse(Blend(shade, Brush(110, 143, 112, 195), daylight), null, new Point(889, 408), 362, 74);
        Grass(dc, Blend(Brush(55, 50, 75, 210), Brush(100, 141, 102, 205), daylight), 802, 7);
        Flower(dc, 902, 373, 9, Brush(179, 140, 217, 230));
    }

    private static Brush Blend(Brush from, Brush to, double amount)
    {
        Color a = ((SolidColorBrush)from).Color, b = ((SolidColorBrush)to).Color;
        byte Mix(byte x, byte y) => (byte)Math.Clamp(Math.Round(x + (y - x) * amount), 0, 255);
        return Brush(Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B), Mix(a.A, b.A));
    }

    private static void Gift(DrawingContext dc, double x, double y, double size, Brush fill, Brush ribbon)
    {
        dc.DrawRectangle(fill, null, new Rect(x, y - size * .55, size, size * .55));
        dc.DrawRectangle(ribbon, null, new Rect(x + size * .44, y - size * .55, size * .12, size * .55));
        dc.DrawRectangle(ribbon, null, new Rect(x, y - size * .43, size, size * .1));
        dc.DrawEllipse(null, Pen(255, 230, 181, 240, 3), new Point(x + size * .46, y - size * .62), size * .16, size * .1);
        dc.DrawEllipse(null, Pen(255, 230, 181, 240, 3), new Point(x + size * .59, y - size * .62), size * .16, size * .1);
    }

    private static void Pumpkin(DrawingContext dc, double x, double y, double r)
    {
        var orange = Brush(218, 105, 39, 248);
        dc.DrawEllipse(orange, null, new Point(x, y), r * .62, r * .43);
        dc.DrawEllipse(Brush(239, 135, 44, 255), null, new Point(x - r * .25, y), r * .3, r * .39);
        dc.DrawEllipse(Brush(198, 82, 34, 255), null, new Point(x + r * .25, y), r * .3, r * .39);
        dc.DrawLine(Pen(89, 121, 57, 255, 5), new Point(x, y - r * .38), new Point(x + 3, y - r * .65));
        dc.DrawGeometry(Brush(255, 212, 104, 255), null, Triangle(x - r * .27, y - 6, 13, 15));
        dc.DrawGeometry(Brush(255, 212, 104, 255), null, Triangle(x + r * .12, y - 6, 13, 15));
    }

    private static void Egg(DrawingContext dc, double x, double y, double w, double h, Brush fill)
    {
        dc.DrawEllipse(fill, Pen(255, 250, 238, 220, 1.5), new Point(x, y), w / 2, h / 2);
        dc.DrawEllipse(Brush(240, 170, 185, 220), null, new Point(x - 6, y), 3, 2);
        dc.DrawEllipse(Brush(242, 210, 122, 220), null, new Point(x + 7, y + 5), 3, 2);
    }

    private static void Grass(DrawingContext dc, Brush brush, double start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double x = start + i * 17;
            dc.DrawLine(Pen(brush, 2.3), new Point(x, 400), new Point(x + (i % 2 == 0 ? -5 : 4), 368 - i % 3 * 4));
        }
    }

    private static void Flower(DrawingContext dc, double x, double y, double r, Brush petal)
    {
        for (int i = 0; i < 5; i++)
        {
            double angle = i * Math.PI * 2 / 5;
            dc.DrawEllipse(petal, null, new Point(x + Math.Cos(angle) * r * .6, y + Math.Sin(angle) * r * .6), r * .37, r * .37);
        }
        dc.DrawEllipse(Brush(255, 225, 122, 245), null, new Point(x, y), r * .25, r * .25);
    }

    private static void Leaf(DrawingContext dc, double x, double y, Brush fill)
    {
        dc.DrawEllipse(fill, null, new Point(x, y), 14, 5);
        dc.DrawLine(Pen(83, 63, 45, 195, 1), new Point(x - 12, y), new Point(x + 12, y));
    }

    private static void Shell(DrawingContext dc, double x, double y, double r)
    {
        dc.DrawEllipse(Brush(228, 163, 112, 245), Pen(255, 238, 207, 215, 1.5), new Point(x, y), r, r * .6);
        for (int i = -1; i <= 1; i++) dc.DrawLine(Pen(143, 94, 67, 205, 1), new Point(x + i * 7, y + 7), new Point(x, y - r * .6));
    }

    private static StreamGeometry Triangle(double x, double y, double w, double h)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(x, y), true, true);
            g.LineTo(new Point(x + w / 2, y + h), true, false);
            g.LineTo(new Point(x + w, y), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b, byte a)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen Pen(byte r, byte g, byte b, byte a, double thickness) => new(Brush(r, g, b, a), thickness);
    private static Pen Pen(Brush brush, double thickness) => new(brush, thickness);
}
