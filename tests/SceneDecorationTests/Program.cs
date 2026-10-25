using PokemonRebornLauncher.Services;

var failures = new List<string>();
var themes = new[] { "standard", "winter", "spring", "halloween", "newyear", "summer", "harvest", "arceus" };
foreach (string theme in themes)
{
    var anchors = SceneDecorationLayout.ForTheme(theme);
    Check(anchors.Count is >= 3 and <= 6, $"{theme} has {anchors.Count} visible anchors, expected 3–6.");
    Check(anchors.All(a => a.X is >= 0 and <= 1 && a.Y is >= 0 and <= 1), $"{theme} has off-stage anchors.");
    double groundBand = SceneDecorationLayout.GroundBandTop(theme);
    Check(anchors.All(a => a.GroundFootY is null || a.GroundFootY.Value >= groundBand && a.GroundFootY.Value <= .97), $"{theme} has ground decor away from its terrain band.");
    var simulation = new EventPropSimulation(7);
    simulation.Reset(theme);
    Check(anchors.GroupBy(a => a.Kind).All(g => simulation.Props.Count(p => p.Kind == g.Key) >= g.Count()), $"{theme} asks for a missing catalog prop.");
    Check(anchors.Where(a => a.ParentKind is not null).All(a => anchors.Any(parent => parent.Kind == a.ParentKind)), $"{theme} has an orphaned attached prop.");
}

var winter = SceneDecorationLayout.ForTheme("winter");
var festival = SceneDecorationLayout.ForTheme("winter", winterFestival: true);
Check(winter.Count is >= 3 and <= 6 && festival.Count is >= 3 and <= 6, "Winter variants exceed the sparse prop budget.");
Check(festival.Any(a => a.Kind == PropKind.Snowman && a.GroundFootY is not null), "Winter festival has no grounded snowman.");
Check(festival.Any(a => a.Kind == PropKind.Gift && a.ParentKind == PropKind.PineTree && a.AlignGroundToParent), "Gift is not grouped under the tree base.");
Check(festival.Count(a => a.ParentKind == PropKind.PineTree && a.Kind is PropKind.Ornament or PropKind.Bell) == 2, "Tree decorations are not attached to the pine anchor.");
Check(festival.Single(a => a.Kind == PropKind.PineTree).ScaleMultiplier > 1.4, "Winter tree has not been enlarged for its attachments.");
Check(festival.Where(a => a.ParentKind == PropKind.PineTree && a.Kind is PropKind.Ornament or PropKind.Bell).All(a => a.ScaleMultiplier < .7), "Baubles and bell are not scaled down for the tree.");
Check(festival.Any(a => a.Kind == PropKind.CandyCane && a.GroundFootY is not null), "Candy cane is not grounded in the snow.");
Check(SceneDecorationLayout.ForTheme("winter").Single(a => a.Kind == PropKind.Icicle).TetherUp, "Winter icicle has no branch connection.");
Check(SceneDecorationLayout.ForTheme("halloween").Single(a => a.Kind == PropKind.Spider).TetherUp, "Halloween spider has no hanging thread.");
var arceus = SceneDecorationLayout.ForTheme("arceus");
Check(arceus.Count(a => a.Kind == PropKind.LightCrystal) == 2, "Arceus platform crystals include a floating center crystal.");
Check(arceus.Where(a => a.Kind == PropKind.LightCrystal).Select(a => a.GroundFootY).SequenceEqual(new double?[] { .78, .82 }), "Arceus crystals are not pinned to the two terrace levels.");

for (int w = 960; w <= 1920; w += 160)
foreach (int h in new[] { 640, 800, 1080 })
foreach (string theme in themes)
{
    var anchors = SceneDecorationLayout.ForTheme(theme, theme == "winter");
    var simulation = new EventPropSimulation(7);
    simulation.Reset(theme, theme == "winter");
    var placed = new List<(PropKind Kind, RectD Bounds)>();
    for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
    {
        SceneDecorationAnchor anchor = anchors[anchorIndex];
        int occurrence = anchors.Take(anchorIndex).Count(a => a.Kind == anchor.Kind);
        EventProp prop = simulation.Props.Where(p => p.Kind == anchor.Kind).ElementAt(occurrence);
        double scaleMin = anchor.ScaleMultiplier < 1 ? .25 : .54;
        double scale = Math.Clamp(Math.Min(w / 1700d, h / 1000d) * prop.Scale * anchor.ScaleMultiplier, scaleMin, 1.25);
        RectD visual = VisualBounds(anchor.Kind);
        double centerX = anchor.X * w;
        double centerY = anchor.Y * h;
        if (anchor.ParentKind is PropKind parentKind)
        {
            var parent = placed.Last(x => x.Kind == parentKind);
            EventProp parentProp = simulation.Props.First(p => p.Kind == parentKind);
            SceneDecorationAnchor parentAnchor = anchors.First(a => a.Kind == parentKind);
            double parentScaleMin = parentAnchor.ScaleMultiplier < 1 ? .25 : .54;
            double parentScale = Math.Clamp(Math.Min(w / 1700d, h / 1000d) * parentProp.Scale * parentAnchor.ScaleMultiplier, parentScaleMin, 1.25);
            centerX = parent.Bounds.X + parent.Bounds.Width / 2 + anchor.ParentOffsetX * parentScale;
            centerY = parent.Bounds.Y + parent.Bounds.Height / 2 + anchor.ParentOffsetY * parentScale;
            if (anchor.AlignGroundToParent)
            {
                RectD parentVisual = VisualBounds(parentKind);
                centerY = parent.Bounds.Y + parent.Bounds.Height / 2 + parentVisual.Bottom * parentScale - visual.Bottom * scale;
            }
        }
        else if (anchor.GroundFootY is double footY)
            centerY = footY * h - visual.Bottom * scale;
        var box = new RectD(centerX + visual.X * scale, centerY + visual.Y * scale,
            visual.Width * scale, visual.Height * scale);
        Check(box.X >= 0 && box.Y >= 0 && box.Right <= w && box.Bottom <= h,
            $"{theme}/{anchor.Kind} visual bounds are outside {w}x{h}: {box}.");
        placed.Add((anchor.Kind, box));
    }
}

void Check(bool condition, string message)
{
    if (condition) return;
    failures.Add(message); Console.Error.WriteLine("FAIL " + message);
}

Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {themes.Length} themes, winter/festival grouping, and responsive visual bounds checked.");
return failures.Count == 0 ? 0 : 1;

static RectD VisualBounds(PropKind kind) => kind switch
{
    PropKind.Snowman => new(-25,-39,50,69), PropKind.Rabbit or PropKind.ChocolateBunny => new(-19,-47,38,65),
    PropKind.SkeletonHand => new(-24,-29,35,49), PropKind.LightString => new(-35,-20,70,39),
    PropKind.Ghost => new(-22,-26,44,50), PropKind.Bat => new(-30,-24,60,45), PropKind.Chick => new(-22,-25,43,50),
    PropKind.PineTree => new(-24,-34,48,67), PropKind.Icicle => new(-17,-24,34,54), PropKind.Gift => new(-22,-22,44,44),
    PropKind.Ornament => new(-15,-17,30,36), PropKind.Bell => new(-18,-21,36,40), PropKind.CandyCane => new(-14,-25,28,48),
    PropKind.Scarecrow => new(-26,-41,52,70), PropKind.Candle => new(-12,-32,24,56), PropKind.Rocket => new(-12,-29,24,63),
    PropKind.Pumpkin => new(-25,-22,50,45), PropKind.Gravestone => new(-22,-34,44,62), PropKind.Cauldron => new(-27,-25,54,48),
    PropKind.Spider => new(-27,-20,54,40), PropKind.Star => new(-22,-22,44,44), PropKind.LightCrystal => new(-20,-30,40,58),
    PropKind.Egg => new(-17,-22,34,44), PropKind.Nest => new(-29,-21,58,42), PropKind.Flower => new(-20,-34,40,62),
    PropKind.Butterfly => new(-26,-22,52,44), PropKind.LeafPile => new(-31,-22,62,45), PropKind.Acorn => new(-17,-21,34,43),
    PropKind.Mushroom => new(-23,-27,46,51), PropKind.Bubble => new(-16,-16,32,32), PropKind.WaterBalloon => new(-18,-21,36,42),
    PropKind.Firefly => new(-12,-12,24,24), PropKind.Sparkler => new(-13,-31,26,55), PropKind.Confetti => new(-24,-22,48,44),
    PropKind.PartyHat => new(-20,-25,40,48), PropKind.Snowball => new(-19,-19,38,38), PropKind.SnowPile => new(-25,-6,50,26),
    PropKind.IceCrystal => new(-19,-29,38,56), _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
};

readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
