using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Automation;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>Interactive, vector-drawn seasonal props on a transparent launcher stage.</summary>
public sealed class EventPropsScene : FrameworkElement
{
    private const double WorldWidth = 1000;
    private const double WorldHeight = 400;
    private const double FrameSeconds = 1.0 / 30.0;
    private const double ReducedFrameSeconds = 1.0 / 6.0;
    private readonly DispatcherTimer _timer;
    private readonly EventPropSimulation _simulation = new();
    private readonly record struct PropLayout(Point Center, double ScaleX, double ScaleY, Rect Bounds);
    private string _theme = "";
    private bool _christmas;
    private bool _enabled;
    private bool _interactive;
    private bool _active = true;
    private bool _reduceMotion;
    private double _phase;
    private long _lastFrameTimestamp;
    private int _lastEggCount;
    private int? _selectedPropId;
    private int? _hoveredPropId;
    private readonly List<Rect> _exclusionZones = [];
    private readonly Dictionary<int, Point> _authoredHomes = [];
    private readonly Dictionary<int, SceneDecorationAnchor> _authoredAnchors = [];
    private readonly Dictionary<int, int> _anchorParents = [];
    private readonly HashSet<int> _visiblePropIds = [];
    private bool _placementDirty = true;
    private int _placedCount = -1;
    private string? _placementFailure;

    /// <summary>Actor/text bounds in this element's DIP coordinates, retained across theme configuration.</summary>
    public void SetExclusionZones(IEnumerable<Rect> zones)
    {
        var next = zones.Where(r => !r.IsEmpty && double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height))
            .Select(r => { r.Inflate(6, 6); return r; }).ToArray();
        if (_exclusionZones.SequenceEqual(next)) return;
        _exclusionZones.Clear(); _exclusionZones.AddRange(next);
        _placementDirty = true;
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _placementDirty = true;
    }

    public void VerifyLayout()
    {
        EnsurePlacement();
        if (_placementFailure is not null) throw new InvalidOperationException(_placementFailure);
        var available = new Rect(0, 0, ActualWidth, ActualHeight);
        var boxes = new List<(EventProp Prop, Rect Box)>();
        foreach (var prop in VisibleProps.Where(p => !p.IsTemporary))
        {
            var box = GetHitBoundsDip(prop);
            if (!available.Contains(box)) throw new InvalidOperationException($"Prop außerhalb der Bühne: {_theme}/{prop.Kind} {box}");
            if (_exclusionZones.Any(zone => zone.IntersectsWith(box))) throw new InvalidOperationException($"Prop über Pokémon oder Text: {_theme}/{prop.Kind}");
            foreach (var previous in boxes)
                if (box.IntersectsWith(previous.Box)
                    && !(_anchorParents.TryGetValue(prop.Id, out int parentId) && parentId == previous.Prop.Id))
                    throw new InvalidOperationException($"Props überlappen: {_theme}/{prop.Kind}/{previous.Prop.Kind}");
            boxes.Add((prop, box));
        }
    }

    public IReadOnlyList<(int Id, PropKind Kind, Rect Bounds)> GetPlacementBounds() => VisibleProps
        .Where(p => !p.IsTemporary).Select(p => (p.Id, p.Kind, GetHitBoundsDip(p))).ToArray();

    private static readonly Brush Ink = B(24, 29, 43);
    private static readonly Brush Snow = B(229, 242, 250);
    private static readonly Brush Ice = B(125, 193, 220);
    private static readonly Brush Pine = B(37, 107, 82);
    private static readonly Brush PineLight = B(84, 159, 112);
    private static readonly Brush Gold = B(255, 209, 103);
    private static readonly Brush Red = B(210, 74, 77);
    private static readonly Brush Orange = B(236, 130, 51);
    private static readonly Brush Violet = B(143, 105, 193);
    private static readonly Brush Pink = B(245, 151, 178);
    private static readonly Brush Cream = B(255, 229, 177);
    private static readonly Brush Water = B(69, 170, 209);
    private static readonly Pen Outline = P(25, 31, 44, 2.2);
    private static readonly Pen Fine = P(25, 31, 44, 1.35);

    public event Action<string>? SoundRequested;
    public event Action<int>? EggsFoundChanged;
    public IReadOnlyList<EventProp> Props => _simulation.Props;
    public bool IsAnimating => _timer.IsEnabled;

    public EventPropsScene()
    {
        Focusable = false;
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(FrameSeconds) };
        _timer.Tick += OnFrame;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeave += OnMouseLeave;
        _simulation.Reset("standard");
        CaptureAuthoredHomes();
        _lastEggCount = _simulation.FoundEggs;
        AutomationProperties.SetName(this, "Interaktive Saisonobjekte. Pfeiltasten wählen, Eingabetaste oder Leertaste benutzen.");
    }

    public int ActiveParticleCount => _simulation.Particles.Count;

    public void Configure(string theme, bool christmas, bool enabled, bool interactive, int particleLimit, double intensity, bool reduceMotion, bool active)
    {
        string nextTheme = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
        if (_theme != nextTheme || _christmas != christmas)
        {
            _theme = nextTheme;
            _christmas = christmas;
            _simulation.Reset(_theme, _christmas);
            CaptureAuthoredHomes();
            _placementDirty = true;
            _lastEggCount = _simulation.FoundEggs;
            _phase = 0;
        }
        _enabled = enabled;
        _interactive = interactive;
        _reduceMotion = reduceMotion;
        _active = active;
        _simulation.Configure(enabled && !reduceMotion, Math.Max(0, particleLimit), intensity);
        IsHitTestVisible = enabled && interactive;
        Focusable = enabled && interactive;
        KeyboardNavigation.SetIsTabStop(this, enabled && interactive);
        if (!Focusable) { _selectedPropId = null; _hoveredPropId = null; }
        else EnsureSelection();
        UpdateTimerState();
        InvalidateVisual();
    }

    /// <summary>Advances deterministic simulation time for diagnostics.</summary>
    public void AdvanceForTest(double deltaSeconds)
    {
        if (deltaSeconds <= 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)) return;
        EnsurePlacement();
        double remaining = deltaSeconds;
        while (remaining > 0)
        {
            double step = Math.Min(.05, remaining);
            _simulation.Step(step, _reduceMotion);
            remaining -= step;
        }
        PublishEggCountIfChanged();
        _phase = (_phase + deltaSeconds) % 60;
        InvalidateVisual();
        UpdateTimerState();
    }

    /// <summary>Activates a prop by simulation id; intended for smoke checks and keyboard integration.</summary>
    public bool ActivateForTest(int id)
    {
        EnsurePlacement();
        bool activated = _simulation.Activate(id);
        if (activated) { PublishEggCountIfChanged(); InvalidateVisual(); UpdateTimerState(); }
        return activated;
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (!_enabled || !_interactive) return null;
        Point p = hitTestParameters.HitPoint;
        Point world = ToWorld(p);
        return FindPropAt(world) is not null ? new PointHitTestResult(this, p) : null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!_enabled || ActualWidth <= 0 || ActualHeight <= 0) return;
        EnsurePlacement();
        dc.PushTransform(new ScaleTransform(ActualWidth / WorldWidth, ActualHeight / WorldHeight));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, WorldWidth, WorldHeight)));
        double sx = ActualWidth / WorldWidth, sy = ActualHeight / WorldHeight;
        foreach (PropParticle particle in _simulation.Particles) DrawParticle(dc, particle, sx, sy);
        EventProp[] visible = VisibleProps.ToArray();
        foreach (EventProp prop in visible.Where(p => !_anchorParents.ContainsKey(p.Id) && !_authoredAnchors.GetValueOrDefault(p.Id).TetherUp)) DrawProp(dc, prop);
        DrawAttachmentTethers(dc);
        foreach (EventProp prop in visible.Where(p => _anchorParents.ContainsKey(p.Id) || _authoredAnchors.GetValueOrDefault(p.Id).TetherUp)) DrawProp(dc, prop);
        if (_simulation.Flash > 0)
        {
            dc.PushOpacity(Math.Clamp(_simulation.Flash, 0, 1) * .12);
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, WorldWidth, WorldHeight));
            dc.Pop();
        }
        dc.Pop(); dc.Pop();
    }

    private IEnumerable<EventProp> VisibleProps => _simulation.Props.Where(p => p.IsTemporary || _visiblePropIds.Contains(p.Id));

    private void DrawAttachmentTethers(DrawingContext dc)
    {
        var barkPen = P(72, 57, 47, 1.05);
        var icePen = P(202, 233, 246, 1.15);
        foreach (var (childId, parentId) in _anchorParents)
        {
            EventProp? child = _simulation.Props.FirstOrDefault(p => p.Id == childId && !p.IsTemporary);
            EventProp? parent = _simulation.Props.FirstOrDefault(p => p.Id == parentId && !p.IsTemporary);
            if (child is null || parent is null) continue;
            if (!_authoredAnchors.GetValueOrDefault(childId).TetherUp) continue;
            PropLayout childLayout = GetPropLayout(child);
            double childTop = childLayout.Center.Y + childLayout.Bounds.Top * childLayout.ScaleY;
            double threadLength = 7 * WorldHeight / ActualHeight;
            dc.DrawLine(barkPen, new Point(childLayout.Center.X, childTop - threadLength),
                new Point(childLayout.Center.X, childTop));
        }
        foreach (EventProp prop in VisibleProps.Where(p => !p.IsTemporary && _authoredAnchors.GetValueOrDefault(p.Id).TetherUp))
        {
            PropLayout layout = GetPropLayout(prop);
            double top = layout.Center.Y + layout.Bounds.Top * layout.ScaleY;
            double lengthDip = prop.Kind == PropKind.Spider ? 62 : 30;
            double lengthWorld = lengthDip * WorldHeight / ActualHeight;
            dc.DrawLine(prop.Kind == PropKind.Icicle ? icePen : barkPen,
                new Point(layout.Center.X, Math.Max(0, top - lengthWorld)),
                new Point(layout.Center.X, top + 1));
        }
    }

    private void DrawProp(DrawingContext dc, EventProp prop)
    {
        dc.PushOpacity(Math.Clamp(prop.Opacity, 0, 1));
        PropLayout layout = GetPropLayout(prop);
        dc.PushTransform(new TranslateTransform(layout.Center.X, layout.Center.Y));
        // Position stays in the normalized world, while artwork keeps a circular, legible shape.
        // A nominal 45-world-unit prop stays at least 24 DIP and scales with the smaller stage axis.
        dc.PushTransform(new ScaleTransform(layout.ScaleX, layout.ScaleY));
        dc.PushTransform(new RotateTransform(prop.Rotation + (_reduceMotion ? 0 : Math.Sin(_phase * 1.1 + prop.Id) * 2)));
        dc.PushTransform(new TranslateTransform(0, prop.Id == _hoveredPropId ? -1.5 : 0));
        DrawKind(dc, prop.Kind, prop.State);
        if (prop.IsRare) DrawSpark(dc, 22, -31, 4, Gold);
        if (prop.Id == _selectedPropId && IsKeyboardFocused) dc.DrawEllipse(Gold, null, new Point(0, GetVisualBounds(prop.Kind).Bottom + 5), 2, 2);
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static void DrawKind(DrawingContext dc, PropKind kind, PropState state)
    {
        if (state == PropState.Broken) { DrawBroken(dc); return; }
        switch (kind)
        {
            case PropKind.Snowman: DrawSnowman(dc); break;
            case PropKind.Snowball: Ball(dc, Snow, Ice); break;
            case PropKind.SnowPile: dc.DrawEllipse(Snow, P(160, 205, 225, 1), new Point(0, 7), 25, 13); dc.DrawEllipse(B(250, 253, 255), null, new Point(-10, 2), 13, 8); break;
            case PropKind.Icicle: Polygon(dc, Ice, new Point(-14,-19),new Point(14,-19),new Point(8,18),new Point(0,27),new Point(-7,12)); break;
            case PropKind.PineTree: DrawTree(dc); break;
            case PropKind.IceCrystal: DrawCrystal(dc); break;
            case PropKind.Rocket: DrawRocket(dc, state == PropState.Active); break;
            case PropKind.Sparkler: DrawSparkler(dc); break;
            case PropKind.Confetti: for (int i=0;i<7;i++) dc.DrawRoundedRectangle(i%3==0?Gold:i%3==1?Pink:Water,null,new Rect(-22+i*6,-14+(i%2)*9,5,12),2,2); break;
            case PropKind.PartyHat: Polygon(dc,Violet,new Point(-20,14),new Point(0,-24),new Point(20,14)); dc.DrawEllipse(Gold,null,new Point(0,-24),4,4); dc.DrawEllipse(Pink,null,new Point(0,17),21,5); break;
            case PropKind.Egg: DrawEgg(dc); break;
            case PropKind.ChocolateBunny: DrawBunny(dc); break;
            case PropKind.Nest: DrawNest(dc); break;
            case PropKind.Flower: DrawFlower(dc); break;
            case PropKind.Butterfly: DrawButterfly(dc); break;
            case PropKind.Pumpkin: DrawPumpkin(dc); break;
            case PropKind.Scarecrow: DrawScarecrow(dc); break;
            case PropKind.Gravestone: DrawGravestone(dc); break;
            case PropKind.Spider: DrawSpider(dc); break;
            case PropKind.Cauldron: DrawCauldron(dc); break;
            case PropKind.Candle: DrawCandle(dc); break;
            case PropKind.Gift: DrawGift(dc); break;
            case PropKind.Ornament: DrawOrnament(dc); break;
            case PropKind.Bell: DrawBell(dc); break;
            case PropKind.SnowGlobe: DrawGlobe(dc); break;
            case PropKind.CandyCane: DrawCandyCane(dc); break;
            case PropKind.Bubble: dc.DrawEllipse(null,P(168,227,247,2),new Point(0,0),18,18); dc.DrawEllipse(B(235,249,255,150),null,new Point(-6,-7),3,4); break;
            case PropKind.WaterBalloon: dc.DrawEllipse(Water,Outline,new Point(0,0),17,20); dc.DrawEllipse(B(210,241,255,170),null,new Point(-5,-8),4,6); break;
            case PropKind.Firefly: DrawFirefly(dc); break;
            case PropKind.LeafPile: DrawLeafPile(dc); break;
            case PropKind.Acorn: DrawAcorn(dc); break;
            case PropKind.Mushroom: DrawMushroom(dc); break;
            case PropKind.Star: DrawStar(dc); break;
            case PropKind.LightCrystal: DrawCrystal(dc); dc.DrawEllipse(Gold,null,new Point(0,0),3,3); break;
            default: DrawAddedKind(dc, kind.ToString()); break;
        }

    }

    private static void DrawAddedKind(DrawingContext dc, string kind)
    {
        switch (kind)
        {
            case "Ghost": DrawGhost(dc); break;
            case "Bat": DrawBat(dc); break;
            case "SkeletonHand": DrawSkeletonHand(dc); break;
            case "Chick": DrawChick(dc); break;
            case "Rabbit": DrawRabbit(dc); break;
            case "LightString": DrawLightString(dc); break;
        }
    }

    private static void DrawSnowman(DrawingContext dc)
    {
        dc.DrawEllipse(Snow,Outline,new Point(0,12),18,17); dc.DrawEllipse(Snow,Outline,new Point(0,-10),14,13); dc.DrawEllipse(Snow,Outline,new Point(0,-29),10,9);
        dc.DrawEllipse(Ink,null,new Point(-3,-31),1.5,2); dc.DrawEllipse(Ink,null,new Point(3,-31),1.5,2);
        Polygon(dc,Orange,new Point(0,-27),new Point(11,-25),new Point(0,-23));
        dc.DrawRoundedRectangle(Red,null,new Rect(-14,-19,28,5),2,2); dc.DrawEllipse(Gold,null,new Point(0,3),2,2); dc.DrawEllipse(Gold,null,new Point(0,12),2,2);
        dc.DrawLine(Outline,new Point(-12,-11),new Point(-25,-20)); dc.DrawLine(Outline,new Point(12,-11),new Point(24,-18));
    }
    private static void DrawTree(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(B(117,77,51),null,new Rect(-4,12,8,20),2,2);
        Polygon(dc,Pine,new Point(0,-29),new Point(-17,-3),new Point(-8,-3),new Point(-22,14),new Point(22,14),new Point(8,-3),new Point(17,-3));
        dc.DrawEllipse(Gold,null,new Point(0,-29),3.5,3.5); dc.DrawEllipse(Red,null,new Point(-9,-3),3,3); dc.DrawEllipse(Gold,null,new Point(7,5),3,3); dc.DrawEllipse(Pink,null,new Point(-3,10),2.5,2.5);
    }
    private static void DrawCrystal(DrawingContext dc) { Polygon(dc,B(119,221,243),new Point(0,-27),new Point(17,-6),new Point(12,18),new Point(0,26),new Point(-14,12),new Point(-17,-9)); Polygon(dc,B(219,246,255,195),new Point(0,-22),new Point(-2,8),new Point(-12,10)); dc.DrawLine(P(255,255,255,2),new Point(-8,-8),new Point(0,-18)); }
    private static void DrawRocket(DrawingContext dc, bool launched)
    {
        // Paper firework shell, pointed cap and wooden guide stick; no spacecraft fins/window.
        dc.DrawRoundedRectangle(Cream, Fine, new Rect(2, 8, 2.5, 24), .5, .5);
        dc.DrawRoundedRectangle(Red, Fine, new Rect(-7, -16, 14, 26), 1, 1);
        dc.DrawRectangle(Gold, null, new Rect(-6, -10, 12, 3));
        dc.DrawRectangle(Gold, null, new Rect(-6, 0, 12, 3));
        Polygon(dc, Red, new Point(-10, -16), new Point(0, -27), new Point(10, -16));
        dc.DrawLine(Fine, new Point(0, 10), new Point(-6, 15));
        dc.DrawLine(Fine, new Point(-6, 15), new Point(-9, 13));
        if (launched)
        {
            Polygon(dc, Orange, new Point(-3, 10), new Point(-1, 26), new Point(3, 10));
            Polygon(dc, Cream, new Point(-2, 10), new Point(0, 19), new Point(2, 10));
        }
        else dc.DrawEllipse(Gold, null, new Point(-9, 13), 1.4, 1.4);
    }
    private static void DrawSparkler(DrawingContext dc) { dc.DrawLine(Fine,new Point(0,0),new Point(0,25)); for(int i=0;i<8;i++){double a=i*Math.PI/4;dc.DrawLine(P(255,206,103,1.7),new Point(Math.Cos(a)*5,Math.Sin(a)*5),new Point(Math.Cos(a)*18,Math.Sin(a)*18));} dc.DrawEllipse(Gold,null,new Point(0,0),4,4); }
    private static void DrawEgg(DrawingContext dc) { dc.PushTransform(new ScaleTransform(.8,1)); dc.DrawEllipse(Cream,Outline,new Point(0,0),16,22); dc.Pop(); dc.DrawEllipse(Pink,null,new Point(-5,-5),4,3); dc.DrawEllipse(PineLight,null,new Point(5,1),3,4); dc.DrawEllipse(Gold,null,new Point(-2,9),4,3); }
    private static void DrawBunny(DrawingContext dc) { dc.DrawEllipse(B(137,80,52),Outline,new Point(0,3),14,17); dc.DrawEllipse(B(154,93,62),Fine,new Point(0,-14),11,11); dc.DrawEllipse(B(137,80,52),Outline,new Point(-7,-28),4,13); dc.DrawEllipse(B(137,80,52),Outline,new Point(7,-29),4,12); dc.DrawEllipse(Pink,null,new Point(0,-12),2.5,2); dc.DrawEllipse(Cream,null,new Point(14,9),5,5); }
    private static void DrawNest(DrawingContext dc) { dc.DrawEllipse(B(139,89,58),Outline,new Point(0,5),24,12); for(int i=0;i<6;i++)dc.DrawLine(P(195,144,91,2),new Point(-18+i*6,-1),new Point(-12+i*6,11)); dc.DrawEllipse(Pink,Fine,new Point(-7,-2),5,7);dc.DrawEllipse(Water,Fine,new Point(7,-2),5,7); }
    private static void DrawFlower(DrawingContext dc) { dc.DrawLine(P(63,135,87,2.5),new Point(0,5),new Point(0,24)); for(int i=0;i<5;i++){double a=i*Math.PI*2/5;dc.DrawEllipse(i%2==0?Pink:B(255,213,102),null,new Point(Math.Cos(a)*8,Math.Sin(a)*8),5,5);}dc.DrawEllipse(Gold,null,new Point(0,0),4,4); }
    private static void DrawButterfly(DrawingContext dc) { dc.DrawEllipse(Violet,null,new Point(-9,-4),9,8);dc.DrawEllipse(Pink,null,new Point(9,-4),9,8);dc.DrawEllipse(Gold,null,new Point(-8,7),7,6);dc.DrawEllipse(Water,null,new Point(8,7),7,6);dc.DrawRoundedRectangle(Ink,null,new Rect(-2,-12,4,25),2,2);dc.DrawLine(Fine,new Point(-1,-11),new Point(-7,-17));dc.DrawLine(Fine,new Point(1,-11),new Point(7,-17)); }
    private static void DrawPumpkin(DrawingContext dc) { dc.DrawEllipse(Orange,Outline,new Point(0,3),20,16);dc.DrawEllipse(B(245,151,57),Fine,new Point(-9,3),10,14);dc.DrawEllipse(B(245,151,57),Fine,new Point(9,3),10,14);dc.DrawRoundedRectangle(Pine,null,new Rect(-3,-17,6,8),2,2);Polygon(dc,Ink,new Point(-10,0),new Point(-4,0),new Point(-7,7));Polygon(dc,Ink,new Point(4,0),new Point(10,0),new Point(7,7)); }
    private static void DrawScarecrow(DrawingContext dc) { dc.DrawLine(P(111,78,52,4),new Point(-23,-2),new Point(23,-2));dc.DrawLine(P(111,78,52,4),new Point(0,-22),new Point(0,26));dc.DrawRoundedRectangle(B(90,132,98),Outline,new Rect(-10,-12,20,24),3,3);dc.DrawEllipse(Cream,Outline,new Point(0,-20),10,9);Polygon(dc,Gold,new Point(-15,-26),new Point(0,-37),new Point(15,-26));dc.DrawLine(Fine,new Point(-4,-20),new Point(4,-20));dc.DrawLine(P(221,207,157,2),new Point(-7,0),new Point(7,0)); }
    private static void DrawGravestone(DrawingContext dc) { dc.DrawRoundedRectangle(B(128,142,158),Outline,new Rect(-17,-24,34,45),14,8);dc.DrawLine(P(225,230,235,1.4),new Point(-8,-12),new Point(8,-12));dc.DrawLine(P(225,230,235,1.4),new Point(0,-20),new Point(0,-4));dc.DrawEllipse(B(71,79,93),null,new Point(-6,8),2,2);dc.DrawEllipse(B(71,79,93),null,new Point(7,12),2.5,2); }
    private static void DrawSpider(DrawingContext dc) { dc.DrawEllipse(Ink,null,new Point(0,0),10,9);dc.DrawEllipse(Ink,null,new Point(0,-8),6,6);for(int i=0;i<4;i++){double y=-7+i*5;dc.DrawLine(Outline,new Point(-7,y),new Point(-21,y-5));dc.DrawLine(Outline,new Point(7,y),new Point(21,y-5));}dc.DrawEllipse(Pink,null,new Point(-2,-9),1.5,2);dc.DrawEllipse(Pink,null,new Point(2,-9),1.5,2); }
    private static void DrawCauldron(DrawingContext dc) { dc.DrawEllipse(B(62,76,87),Outline,new Point(0,4),22,15);dc.DrawRoundedRectangle(B(62,76,87),Outline,new Rect(-22,-3,44,12),5,5);dc.DrawEllipse(PineLight,Fine,new Point(-5,-5),5,2.5);dc.DrawEllipse(Violet,Fine,new Point(5,-6),5,2.5);dc.DrawLine(Outline,new Point(-11,17),new Point(-14,22));dc.DrawLine(Outline,new Point(11,17),new Point(14,22)); }
    private static void DrawCandle(DrawingContext dc) { dc.DrawRoundedRectangle(Cream,Outline,new Rect(-9,-8,18,29),3,3);dc.DrawLine(Fine,new Point(0,-8),new Point(0,-15));Polygon(dc,Gold,new Point(0,-28),new Point(-6,-17),new Point(0,-12),new Point(6,-17));dc.DrawEllipse(Orange,null,new Point(0,-18),2.5,5); }
    private static void DrawGift(DrawingContext dc) { dc.DrawRectangle(Red,Outline,new Rect(-18,-6,36,27));dc.DrawRectangle(Gold,Fine,new Rect(-3,-6,6,27));dc.DrawRectangle(Gold,Fine,new Rect(-21,-12,42,8));dc.DrawEllipse(null,P(255,220,130,2),new Point(-6,-16),7,5);dc.DrawEllipse(null,P(255,220,130,2),new Point(6,-16),7,5); }
    private static void DrawOrnament(DrawingContext dc) { dc.DrawEllipse(Red,Outline,new Point(0,3),15,16);dc.DrawRoundedRectangle(Gold,Fine,new Rect(-5,-17,10,5),2,2);dc.DrawEllipse(B(255,255,255,160),null,new Point(-5,-3),3,5);dc.DrawEllipse(Gold,null,new Point(7,11),2.5,2.5); }
    private static void DrawBell(DrawingContext dc) { Polygon(dc,Gold, new Point(-16,10),new Point(-13,-4),new Point(-8,-14),new Point(8,-14),new Point(13,-4),new Point(16,10));dc.DrawEllipse(Red,Outline,new Point(0,13),4,4);dc.DrawLine(Fine,new Point(-17,10),new Point(17,10));dc.DrawLine(Fine,new Point(0,-20),new Point(0,-15)); }
    private static void DrawGlobe(DrawingContext dc) { dc.DrawEllipse(B(118,195,218,125),Outline,new Point(0,-3),20,21);dc.DrawEllipse(B(225,245,255,180),null,new Point(-6,-11),6,7);dc.DrawEllipse(Snow,null,new Point(0,7),13,6);DrawTree(dc);dc.DrawRoundedRectangle(B(127,77,53),Outline,new Rect(-15,16,30,7),3,3); }
    private static void DrawCandyCane(DrawingContext dc) { dc.DrawLine(P(255,255,255,7),new Point(-9,20),new Point(-9,-6));dc.DrawLine(P(230,69,78,4),new Point(-9,9),new Point(-9,16));dc.DrawLine(P(230,69,78,4),new Point(-9,-2),new Point(-9,5));dc.DrawLine(P(255,255,255,7),new Point(-9,-6),new Point(-9,-13));dc.DrawLine(P(255,255,255,7),new Point(-9,-13),new Point(0,-19));dc.DrawLine(P(230,69,78,4),new Point(-5,-16),new Point(-1,-19));dc.DrawLine(P(230,69,78,4),new Point(-9,-6),new Point(-9,-1)); }
    private static void DrawFirefly(DrawingContext dc) { dc.DrawEllipse(B(255,224,126,40),null,new Point(0,0),17,17);dc.DrawEllipse(Gold,null,new Point(0,0),5,5);dc.DrawEllipse(null,P(255,240,174,1.4),new Point(0,0),10,10); }
    private static void DrawLeafPile(DrawingContext dc) { for(int i=0;i<8;i++){double x=-18+(i%4)*11,y=5-(i/4)*9;dc.PushTransform(new RotateTransform(-25+i*9,x,y));dc.DrawEllipse(i%3==0?Red:i%3==1?Gold:Orange,null,new Point(x,y),11,5);dc.DrawLine(P(126,72,50,1),new Point(x-7,y),new Point(x+7,y));dc.Pop();} }
    private static void DrawAcorn(DrawingContext dc) { dc.DrawEllipse(B(187,126,69),Outline,new Point(0,6),12,14);dc.DrawEllipse(B(117,75,49),Outline,new Point(0,-4),14,7);for(int i=-1;i<=1;i++)dc.DrawLine(P(210,158,93,1),new Point(i*5,-8),new Point(i*5,0));dc.DrawLine(Fine,new Point(0,-10),new Point(4,-17)); }
    private static void DrawMushroom(DrawingContext dc) { dc.DrawRoundedRectangle(Cream,Outline,new Rect(-8,-1,16,22),5,5);dc.DrawEllipse(Red,Outline,new Point(0,-5),19,13);dc.DrawEllipse(Cream,null,new Point(-8,-9),3,2);dc.DrawEllipse(Cream,null,new Point(5,-12),3,2);dc.DrawEllipse(Cream,null,new Point(12,-4),2,2); }
    private static void DrawStar(DrawingContext dc) { var pts=new Point[10];for(int i=0;i<10;i++){double a=-Math.PI/2+i*Math.PI/5,r=i%2==0?22:10;pts[i]=new Point(Math.Cos(a)*r,Math.Sin(a)*r);}Polygon(dc,Gold,pts);dc.DrawEllipse(B(255,241,182),null,new Point(-5,-7),4,3); }
    private static void DrawGhost(DrawingContext dc)
    {
        var g = new StreamGeometry(); using (var c = g.Open())
        {
            c.BeginFigure(new Point(-17,18), true, true);
            c.BezierTo(new Point(-22,-1), new Point(-16,-23), new Point(0,-23), true, false);
            c.BezierTo(new Point(17,-23), new Point(22,-2), new Point(17,18), true, false);
            c.LineTo(new Point(8,12), true, false); c.LineTo(new Point(0,19), true, false); c.LineTo(new Point(-8,12), true, false);
        }
        g.Freeze(); dc.DrawGeometry(B(224,237,250),Outline,g);
        dc.DrawEllipse(Ink,null,new Point(-6,-5),2.2,3.4); dc.DrawEllipse(Ink,null,new Point(6,-5),2.2,3.4);
        dc.DrawEllipse(Pink,null,new Point(0,3),2,1.5);
    }
    private static void DrawBat(DrawingContext dc)
    {
        Polygon(dc,Violet,new Point(-2,-7),new Point(-12,-21),new Point(-17,-5),new Point(-28,-11),new Point(-24,4),new Point(-15,11),new Point(-7,5),new Point(0,13),new Point(7,5),new Point(15,11),new Point(24,4),new Point(28,-11),new Point(17,-5),new Point(12,-21),new Point(2,-7));
        dc.DrawEllipse(Ink,null,new Point(0,-1),8,12); dc.DrawEllipse(Pink,null,new Point(-3,-4),1.6,2.2); dc.DrawEllipse(Pink,null,new Point(3,-4),1.6,2.2);
    }
    private static void DrawSkeletonHand(DrawingContext dc)
    {
        Brush bone = B(235,229,203); Pen bonePen = P(91,83,71,1.5);
        dc.DrawEllipse(bone,bonePen,new Point(0,9),9,11);
        double[] x = [-8,-3,3,8], len = [19,25,24,18];
        for (int i=0;i<x.Length;i++)
        {
            dc.DrawLine(new Pen(bone,5),new Point(x[i],4),new Point(x[i],4-len[i]));
            dc.DrawEllipse(bone,bonePen,new Point(x[i],4-len[i]),3.1,3.1);
            dc.DrawLine(bonePen,new Point(x[i]-2.2,4-len[i]*.55),new Point(x[i]+2.2,4-len[i]*.55));
        }
        dc.DrawLine(new Pen(bone,5),new Point(-7,12),new Point(-20,2));
        dc.DrawEllipse(bone,bonePen,new Point(-20,2),3.1,3.1);
    }
    private static void DrawChick(DrawingContext dc)
    {
        dc.DrawEllipse(B(255,213,92),Outline,new Point(0,4),17,15);
        dc.DrawEllipse(B(255,221,111),Fine,new Point(0,-10),12,11);
        Polygon(dc,Orange,new Point(10,-10),new Point(20,-7),new Point(10,-4));
        dc.DrawEllipse(Ink,null,new Point(-4,-12),1.5,2); dc.DrawEllipse(Ink,null,new Point(4,-12),1.5,2);
        dc.DrawLine(P(236,130,58,2),new Point(-6,17),new Point(-9,21)); dc.DrawLine(P(236,130,58,2),new Point(6,17),new Point(9,21));
    }
    private static void DrawRabbit(DrawingContext dc)
    {
        Brush fur = B(247,241,225);
        dc.DrawEllipse(fur,Outline,new Point(0,3),15,17); dc.DrawEllipse(fur,Fine,new Point(0,-15),11,11);
        dc.DrawEllipse(fur,Outline,new Point(-7,-31),4.5,15); dc.DrawEllipse(fur,Outline,new Point(7,-31),4.5,15);
        dc.DrawEllipse(Pink,null,new Point(-7,-31),2,10); dc.DrawEllipse(Pink,null,new Point(7,-31),2,10);
        dc.DrawEllipse(Ink,null,new Point(-4,-17),1.4,1.8); dc.DrawEllipse(Ink,null,new Point(4,-17),1.4,1.8);
        dc.DrawEllipse(Pink,null,new Point(0,-12),2,1.8); dc.DrawEllipse(fur,null,new Point(15,9),5,5);
    }
    private static void DrawLightString(DrawingContext dc)
    {
        var g=new StreamGeometry(); using(var c=g.Open()) { c.BeginFigure(new Point(-29,-10),false,false); c.BezierTo(new Point(-17,-3),new Point(-8,-17),new Point(0,-9),false,false); c.BezierTo(new Point(10,0),new Point(19,-15),new Point(29,-7),false,false); }
        g.Freeze(); dc.DrawGeometry(null,P(41,54,70,2),g);
        Brush[] bulbs=[Red,Gold,PineLight,Water,Pink];
        Point[] pts=[new(-24,-6),new(-12,-8),new(0,-9),new(14,-7),new(25,-8)];
        for(int i=0;i<pts.Length;i++){dc.DrawLine(Fine,pts[i],new Point(pts[i].X,pts[i].Y+6));dc.DrawEllipse(bulbs[i],P(255,247,227,1),new Point(pts[i].X,pts[i].Y+10),4.8,5.8);dc.DrawEllipse(B(255,255,237,190),null,new Point(pts[i].X-1.5,pts[i].Y+8.5),1.4,1.8);}
    }
    private static void DrawBroken(DrawingContext dc) { dc.DrawLine(Outline,new Point(-17,12),new Point(-5,-1));dc.DrawLine(Outline,new Point(-5,-1),new Point(1,10));dc.DrawLine(Outline,new Point(1,10),new Point(17,-13));dc.DrawEllipse(Gold,null,new Point(-20,-10),2.5,2.5);dc.DrawEllipse(Pink,null,new Point(17,14),2,2); }
    private static void DrawSpark(DrawingContext dc,double x,double y,double r,Brush b) { dc.DrawLine(P(255,232,155,1),new Point(x-r,y),new Point(x+r,y));dc.DrawLine(P(255,232,155,1),new Point(x,y-r),new Point(x,y+r));dc.DrawEllipse(b,null,new Point(x,y),Math.Max(1,r*.32),Math.Max(1,r*.32)); }
    private static void Ball(DrawingContext dc,Brush fill,Brush shine) { dc.DrawEllipse(fill,Outline,new Point(0,0),14,14);dc.DrawLine(Fine,new Point(-13,0),new Point(13,0));dc.DrawEllipse(shine,Fine,new Point(0,0),4,4);dc.DrawEllipse(B(255,255,255,160),null,new Point(-5,-6),3,2); }
    private static void Polygon(DrawingContext dc,Brush fill,params Point[] points) { var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(points[0],true,true);for(int i=1;i<points.Length;i++)c.LineTo(points[i],true,false);}g.Freeze();dc.DrawGeometry(fill,Outline,g); }

    private void DrawParticle(DrawingContext dc, PropParticle p, double sx, double sy)
    {
        double life = p.MaxLife <= 0 ? 0 : Math.Clamp(p.Life / p.MaxLife, 0, 1);
        dc.PushOpacity(life);
        Brush color = new SolidColorBrush(Color.FromArgb((byte)(p.Color >> 24), (byte)(p.Color >> 16), (byte)(p.Color >> 8), (byte)p.Color));
        dc.PushTransform(new TranslateTransform(p.X, p.Y));
        dc.PushTransform(new ScaleTransform(1 / sx, 1 / sy));
        dc.PushTransform(new RotateTransform(p.Rotation));
        double size = Math.Max(1, p.Size);
        switch (p.Kind.ToLowerInvariant())
        {
            case "spark": case "star": DrawSpark(dc,0,0,Math.Max(2,size),color); break;
            case "snow":
                dc.DrawLine(P(239,249,255,1.2),new Point(-size,0),new Point(size,0));
                dc.DrawLine(P(239,249,255,1.2),new Point(0,-size),new Point(0,size));
                dc.DrawEllipse(color,null,new Point(0,0),Math.Max(1,size*.3),Math.Max(1,size*.3)); break;
            case "snowball":
                dc.DrawEllipse(color,P(175,209,225,1),new Point(0,0),size,size);
                dc.DrawEllipse(B(255,255,255,180),null,new Point(-size*.32,-size*.34),size*.25,size*.17); break;
            case "shard":
                Polygon(dc,color,new Point(0,-size*1.35),new Point(size*.8,-size*.25),new Point(size*.45,size),new Point(-size*.5,size*.75),new Point(-size*.75,-size*.25)); break;
            case "leaf": DrawParticleLeaf(dc,color,size); break;
            case "confetti":
                dc.DrawRoundedRectangle(color,null,new Rect(-size*.7,-size*1.2,size*1.4,size*2.4),Math.Max(.8,size*.18),Math.Max(.8,size*.18)); break;
            case "smoke":
                dc.DrawEllipse(color,null,new Point(0,0),size*1.25,size*.95);
                dc.DrawEllipse(B(236,227,249,55),null,new Point(-size*.4,-size*.3),size*.55,size*.5); break;
            case "water": case "bubble":
                dc.DrawEllipse(null,new Pen(color,Math.Max(1,size*.2)),new Point(0,0),size,size);
                dc.DrawEllipse(B(255,255,255,185),null,new Point(-size*.35,-size*.35),size*.18,size*.25); break;
            case "petal":
                dc.DrawEllipse(color,null,new Point(0,0),size*.62,size*.38); break;
            case "chick":
                dc.PushTransform(new ScaleTransform(size/16,size/16)); DrawChick(dc); dc.Pop(); break;
            default: dc.DrawEllipse(color,null,new Point(0,0),size,size); break;
        }
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static void DrawParticleLeaf(DrawingContext dc, Brush color, double size)
    {
        var g = new StreamGeometry(); using (var c = g.Open())
        {
            c.BeginFigure(new Point(-size,0),true,true);
            c.BezierTo(new Point(-size*.35,-size*.85),new Point(size*.6,-size*.85),new Point(size,0),true,false);
            c.BezierTo(new Point(size*.45,size*.8),new Point(-size*.45,size*.7),new Point(-size,0),true,false);
        }
        g.Freeze(); dc.DrawGeometry(color,null,g);
        dc.DrawLine(P(103,70,42,1),new Point(-size*.72,0),new Point(size*.72,0));
    }

    private Point ToWorld(Point p) => new(p.X * WorldWidth / Math.Max(1, ActualWidth), p.Y * WorldHeight / Math.Max(1, ActualHeight));
    private Rect GetHitRect(EventProp prop)
    {
        Rect dip = GetHitBoundsDip(prop);
        double sx = Math.Max(.001, ActualWidth / WorldWidth), sy = Math.Max(.001, ActualHeight / WorldHeight);
        return new Rect(dip.X / sx, dip.Y / sy, dip.Width / sx, dip.Height / sy);
    }

    private Rect LocalReservedBounds(EventProp prop)
    {
        Rect bounds = GetVisualBounds(prop.Kind);
        bounds.Inflate(7, 7);
        if (prop.IsRare) bounds.Union(new Rect(16, -37, 12, 12));
        // Idle sway and the small keyboard marker stay inside the reserved footprint.
        return new RotateTransform(5).TransformBounds(bounds);
    }

    private double PropDipScale(EventProp prop)
    {
        double multiplier = _authoredAnchors.TryGetValue(prop.Id, out SceneDecorationAnchor anchor) ? anchor.ScaleMultiplier : 1;
        double minimum = multiplier < 1 ? .25 : .54;
        return Math.Clamp(Math.Min(ActualWidth / 1700, ActualHeight / 1000) * prop.Scale * multiplier, minimum, 1.25);
    }

    private Rect BoxAt(EventProp prop, Point center, bool reserve = false)
    {
        double scale = PropDipScale(prop);
        Rect local = LocalReservedBounds(prop);
        Rect box = new(center.X + local.X * scale, center.Y + local.Y * scale, local.Width * scale, local.Height * scale);
        double width = Math.Max(26, box.Width), height = Math.Max(26, box.Height);
        box = new Rect(box.X - (width - box.Width) / 2, box.Y - (height - box.Height) / 2, width, height);
        if (reserve)
        {
            bool drifting = prop.Kind is PropKind.Ghost or PropKind.Bat or PropKind.Butterfly or PropKind.Firefly or PropKind.Bubble or PropKind.Spider;
            if (StandsOnGround(prop.Kind))
            {
                box.Inflate(3, 3);
                // Rabbits hop upward from the floor, never below it.
                if (prop.Kind is PropKind.ChocolateBunny or PropKind.Rabbit)
                {
                    double hop = 9 * ActualHeight / WorldHeight;
                    box = new Rect(box.X, box.Y - hop, box.Width, box.Height + hop);
                }
                return box;
            }
            double verticalMotion = 2.2 + (drifting ? 15 : 0);
            box.Inflate(3 + (drifting ? 14 * ActualWidth / WorldWidth : 0), 3 + verticalMotion * ActualHeight / WorldHeight);
        }
        return box;
    }

    private Point ClampCenter(EventProp prop, Point center)
    {
        Rect zero = BoxAt(prop, new Point(), true);
        double minX = 2 - zero.Left, maxX = ActualWidth - 2 - zero.Right;
        double minY = 2 - zero.Top, maxY = ActualHeight - 2 - zero.Bottom;
        return new Point(minX <= maxX ? Math.Clamp(center.X, minX, maxX) : ActualWidth / 2,
            minY <= maxY ? Math.Clamp(center.Y, minY, maxY) : ActualHeight / 2);
    }

    private static bool StandsOnGround(PropKind kind) => kind is not
        (PropKind.Ghost or PropKind.Bat or PropKind.Butterfly or PropKind.Firefly or PropKind.Bubble or
        PropKind.Spider or PropKind.Icicle or PropKind.Ornament or PropKind.Bell or PropKind.LightString or PropKind.Star or PropKind.Confetti);

    private bool Fits(EventProp prop, Point center, IReadOnlyList<Rect> occupied)
    {
        Rect box = BoxAt(prop, center, true);
        double groundBandTop = _authoredAnchors.TryGetValue(prop.Id, out SceneDecorationAnchor anchor) && anchor.GroundBandTop is double customBand
            ? customBand : SceneDecorationLayout.GroundBandTop(_theme);
        return new Rect(0, 0, ActualWidth, ActualHeight).Contains(box)
            && (!StandsOnGround(prop.Kind) || box.Bottom >= ActualHeight * groundBandTop)
            && !_exclusionZones.Any(r => r.IntersectsWith(box)) && !occupied.Any(r => r.IntersectsWith(box));
    }

    private Point? NearestFree(EventProp prop, Point preferred, IReadOnlyList<Rect> occupied)
    {
        Point clamped = ClampCenter(prop, preferred);
        if (Fits(prop, clamped, occupied)) return clamped;
        Point? best = null; double score = double.PositiveInfinity;
        // Resolve collisions locally around the authored semantic anchor. Never send
        // a prop to an unrelated free patch across the launcher.
        const double turn = 2.399963229728653;
        for (int ring = 1; ring <= 18; ring++)
        for (int spoke = 0; spoke < 20; spoke++)
        {
            double angle = spoke * turn;
            double radiusX = ActualWidth * .0045 * ring;
            double radiusY = ActualHeight * .0035 * ring;
            var candidate = ClampCenter(prop, new Point(preferred.X + Math.Cos(angle) * radiusX,
                preferred.Y + Math.Sin(angle) * radiusY));
            double distance = (candidate - preferred).LengthSquared;
            if (distance >= score || !Fits(prop, candidate, occupied)) continue;
            best = candidate; score = distance;
        }
        return best;
    }

    private void CaptureAuthoredHomes()
    {
        _authoredHomes.Clear();
        _authoredAnchors.Clear();
        _anchorParents.Clear();
        _visiblePropIds.Clear();
        var available = _simulation.Props.Where(p => !p.IsTemporary).GroupBy(p => p.Kind)
            .ToDictionary(g => g.Key, g => new Queue<EventProp>(g.OrderBy(p => p.Id)));
        var firstByKind = new Dictionary<PropKind, int>();
        foreach (SceneDecorationAnchor anchor in SceneDecorationLayout.ForTheme(_theme, _christmas))
        {
            if (!available.TryGetValue(anchor.Kind, out Queue<EventProp>? queue) || queue.Count == 0) continue;
            EventProp prop = queue.Dequeue();
            _visiblePropIds.Add(prop.Id);
            _authoredAnchors[prop.Id] = anchor;
            _authoredHomes[prop.Id] = new Point(anchor.X * WorldWidth, anchor.Y * WorldHeight);
            if (anchor.ParentKind is PropKind parentKind && firstByKind.TryGetValue(parentKind, out int parentId))
                _anchorParents[prop.Id] = parentId;
            firstByKind.TryAdd(anchor.Kind, prop.Id);
        }
    }

    private void EnsurePlacement()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        int permanentCount = _visiblePropIds.Count;
        if (!_placementDirty && _placedCount == permanentCount) return;
        _placementFailure = null;
        var occupied = new List<Rect>();
        foreach (var prop in VisibleProps.Where(p => !p.IsTemporary)
            .OrderByDescending(p => StandsOnGround(p.Kind))
            .ThenByDescending(p => BoxAt(p, new Point(), true).Height))
        {
            Point authored = _authoredHomes.GetValueOrDefault(prop.Id, new Point(prop.HomeX, prop.HomeY));
            Point preferred = new(authored.X / WorldWidth * ActualWidth, authored.Y / WorldHeight * ActualHeight);
            SceneDecorationAnchor anchor = _authoredAnchors[prop.Id];
            bool attached = _anchorParents.TryGetValue(prop.Id, out int parentId);
            if (attached)
            {
                EventProp parent = _simulation.Props.First(p => p.Id == parentId);
                double parentScale = PropDipScale(parent);
                preferred = new Point(parent.HomeX / WorldWidth * ActualWidth + anchor.ParentOffsetX * parentScale,
                    parent.HomeY / WorldHeight * ActualHeight + anchor.ParentOffsetY * parentScale);
                if (anchor.AlignGroundToParent)
                    preferred.Y = parent.HomeY / WorldHeight * ActualHeight
                        + (GetVisualBounds(parent.Kind).Bottom * parentScale - GetVisualBounds(prop.Kind).Bottom * PropDipScale(prop));
            }
            else if (anchor.GroundFootY is double footY)
                preferred.Y = ActualHeight * footY - GetVisualBounds(prop.Kind).Bottom * PropDipScale(prop);
            // Parent-relative decorations stay on their authored branch point. Overlap
            // with the parent is intentional and children render/hit-test above it.
            Point? found = attached ? ClampCenter(prop, preferred) : NearestFree(prop, preferred, occupied);
            var center = found ?? ClampCenter(prop, preferred);
            if (found is null) _placementFailure = $"Keine freie Prop-Fläche: {_theme}/{prop.Kind}, Bühne {ActualWidth:0}×{ActualHeight:0}";
            // One authoritative location for drawing, hit testing, particles, children and respawn.
            _simulation.RelocateHome(prop.Id, center.X / ActualWidth * WorldWidth, center.Y / ActualHeight * WorldHeight);
            if (!attached) occupied.Add(BoxAt(prop, center, true));
        }
        _placedCount = permanentCount; _placementDirty = false;
    }

    private Point RenderCenterDip(EventProp prop)
    {
        EnsurePlacement();
        // Reserve idle movement when placing homes, rather than clamping each animation frame.
        // Active flights and temporary children retain the simulation's physical trajectory.
        return new Point(prop.X / WorldWidth * ActualWidth,
            (prop.Y + GetPropBob(prop)) / WorldHeight * ActualHeight);
    }

    private Rect GetHitBoundsDip(EventProp prop) => BoxAt(prop, RenderCenterDip(prop));

    private PropLayout GetPropLayout(EventProp prop)
    {
        double sx = Math.Max(.001, ActualWidth / WorldWidth), sy = Math.Max(.001, ActualHeight / WorldHeight);
        Point center = RenderCenterDip(prop);
        double scale = PropDipScale(prop);
        return new PropLayout(new Point(center.X / sx, center.Y / sy), scale / sx, scale / sy, GetVisualBounds(prop.Kind));
    }

    private double GetPropBob(EventProp prop) => _reduceMotion || prop.State != PropState.Idle || StandsOnGround(prop.Kind) ? 0 : Math.Sin(_phase * 1.4 + prop.Id * 1.73) * 2.2;

    private static Rect GetVisualBounds(PropKind kind) => kind switch
    {
        PropKind.Snowman => new Rect(-25, -39, 50, 69),
        PropKind.Rabbit or PropKind.ChocolateBunny => new Rect(-19, -47, 38, 65),
        PropKind.SkeletonHand => new Rect(-24, -29, 35, 49),
        PropKind.LightString => new Rect(-35, -20, 70, 39),
        PropKind.Ghost => new Rect(-22, -26, 44, 50),
        PropKind.Bat => new Rect(-30, -24, 60, 45),
        PropKind.Chick => new Rect(-22, -25, 43, 50),
        PropKind.PineTree => new Rect(-24, -34, 48, 67),
        PropKind.Icicle => new Rect(-17, -24, 34, 54),
        PropKind.Gift => new Rect(-22, -22, 44, 44),
        PropKind.Ornament => new Rect(-15, -17, 30, 36),
        PropKind.Bell => new Rect(-18, -21, 36, 40),
        PropKind.CandyCane => new Rect(-14, -25, 28, 48),
        PropKind.Scarecrow => new Rect(-26, -41, 52, 70),
        PropKind.Candle => new Rect(-12, -32, 24, 56),
        PropKind.Rocket => new Rect(-12, -29, 24, 63),
        _ => new Rect(-24, -24, 48, 48)
    };

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_enabled || !_interactive) return;
        Point p = ToWorld(e.GetPosition(this));
        EventProp? hit = FindPropAt(p);
        if (hit is not null)
        {
            _selectedPropId = hit.Id;
            Focus();
            InvalidateVisual();
            if (TryActivateProp(hit)) e.Handled = true;
        }
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_enabled || !_interactive) return;
        Point p = ToWorld(e.GetPosition(this));
        int? hovered = FindPropAt(p)?.Id;
        if (_hoveredPropId == hovered) return;
        _hoveredPropId = hovered;
        InvalidateVisual();
    }
    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoveredPropId is null) return;
        _hoveredPropId = null;
        InvalidateVisual();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_enabled || !_interactive) { base.OnKeyDown(e); return; }
        switch (e.Key)
        {
            case Key.Left: MoveSelection(-1); e.Handled = true; break;
            case Key.Right: MoveSelection(1); e.Handled = true; break;
            case Key.Enter: case Key.Space:
                EventProp? selected = VisibleProps.FirstOrDefault(x => x.Id == _selectedPropId && x.State == PropState.Idle);
                if (selected is null) { EnsureSelection(); selected = VisibleProps.FirstOrDefault(x => x.Id == _selectedPropId && x.State == PropState.Idle); }
                if (selected is not null) TryActivateProp(selected);
                e.Handled = true;
                break;
            default: base.OnKeyDown(e); return;
        }
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); EnsureSelection(); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }
    private void EnsureSelection()
    {
        if (_selectedPropId is not null && VisibleProps.Any(x => x.Id == _selectedPropId && x.State == PropState.Idle)) return;
        _selectedPropId = VisibleProps.FirstOrDefault(x => x.State == PropState.Idle)?.Id;
    }
    private EventProp? FindPropAt(Point world) => VisibleProps
        .Where(x => x.State == PropState.Idle && GetHitRect(x).Contains(world))
        .OrderBy(x => _anchorParents.ContainsKey(x.Id) ? 0 : 1)
        .ThenBy(x => Math.Pow(GetPropLayout(x).Center.X - world.X, 2) + Math.Pow(GetPropLayout(x).Center.Y - world.Y, 2))
        .FirstOrDefault();
    private void MoveSelection(int direction)
    {
        EventProp[] idle = VisibleProps.Where(x => x.State == PropState.Idle).OrderBy(x => x.X).ThenBy(x => x.Y).ToArray();
        if (idle.Length == 0) { _selectedPropId = null; InvalidateVisual(); return; }
        int current = Array.FindIndex(idle, x => x.Id == _selectedPropId);
        if (current < 0) current = direction > 0 ? -1 : 0;
        _selectedPropId = idle[(current + direction + idle.Length) % idle.Length].Id;
        InvalidateVisual();
    }
    private bool TryActivateProp(EventProp prop)
    {
        EnsurePlacement();
        if (!_simulation.Activate(prop.Id)) return false;
        PublishEggCountIfChanged();
        InvalidateVisual();
        UpdateTimerState();
        EnsureSelection();
        return true;
    }
    private void OnFrame(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = _lastFrameTimestamp == 0 ? _timer.Interval.TotalSeconds : (now - _lastFrameTimestamp) / (double)Stopwatch.Frequency;
        _lastFrameTimestamp = now;
        AdvanceForTest(elapsed);
    }
    private void OnLoaded(object sender, RoutedEventArgs e) { _simulation.SoundRequested += OnSimulationSoundRequested; UpdateTimerState(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { _timer.Stop(); _simulation.SoundRequested -= OnSimulationSoundRequested; }
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateTimerState();
    private void OnSimulationSoundRequested(string sound) => SoundRequested?.Invoke(sound);
    private void PublishEggCountIfChanged() { int count = _simulation.FoundEggs; if (count == _lastEggCount) return; _lastEggCount = count; EggsFoundChanged?.Invoke(count); }
    private void UpdateTimerState()
    {
        bool hasTransientState = _simulation.Props.Any(p => p.State != PropState.Idle) || _simulation.Particles.Count > 0;
        bool shouldRun = IsLoaded && IsVisible && _active && _enabled && (!_reduceMotion || hasTransientState);
        if (shouldRun)
        {
            _timer.Interval = TimeSpan.FromSeconds(_reduceMotion ? ReducedFrameSeconds : FrameSeconds);
            if (!_timer.IsEnabled) { _lastFrameTimestamp = Stopwatch.GetTimestamp(); _timer.Start(); }
        }
        else { _timer.Stop(); _lastFrameTimestamp = 0; }
    }
    private static Brush B(byte r,byte g,byte b,byte a=255) { var x=new SolidColorBrush(Color.FromArgb(a,r,g,b));x.Freeze();return x; }
    private static Pen P(byte r,byte g,byte b,double width) { var x=new Pen(B(r,g,b),width);x.Freeze();return x; }
}
