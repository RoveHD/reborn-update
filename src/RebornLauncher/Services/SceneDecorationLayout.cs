namespace PokemonRebornLauncher.Services;

/// <summary>
/// One authored screen-relative anchor. X/Y are normalized to the scene; GroundFootY
/// pins the prop to terrain, while parent offsets are local DIPs scaled with that prop.
/// </summary>
public readonly record struct SceneDecorationAnchor(
    PropKind Kind, double X, double Y, double? GroundFootY = null,
    PropKind? ParentKind = null, double ParentOffsetX = 0, double ParentOffsetY = 0,
    bool AlignGroundToParent = false, double ScaleMultiplier = 1,
    bool TetherUp = false, double? GroundBandTop = null);

/// <summary>Small authored compositions that keep decoration on the matching scenery.</summary>
public static class SceneDecorationLayout
{
    // Underside of the upper-left branch in anime/halloween-foreground.png.
    // Image coordinates stay attached across cover crops and camera transforms.
    public const double HalloweenSpiderBranchX = 240d / 1671;
    public const double HalloweenSpiderBranchY = 224d / 941;
    // Ground contacts refer to stable, visible stone shelves in halloween-terrain.png
    // (1672 x 941). Keep them in source pixels so the backdrop's cover crop and
    // parallax transform move each prop with the stone beneath it.
    public static IReadOnlyList<(PropKind Kind, double X, double Y)> HalloweenLandscapeAnchors { get; } =
    [
        (PropKind.Pumpkin, 240d / 1672, 750d / 941),
        (PropKind.Gravestone, 1450d / 1672, 755d / 941),
        (PropKind.Cauldron, 1360d / 1672, 755d / 941),
        (PropKind.Candle, 260d / 1672, 642d / 941),
        (PropKind.Ghost, 1420d / 1672, 355d / 941),
        (PropKind.Spellbook, 350d / 1672, 750d / 941),
        (PropKind.Potion, 1300d / 1672, 820d / 941),
        (PropKind.Bat, 1307d / 1672, 429d / 941)
    ];
    private static readonly (PropKind Kind, double X, double Y)[] HalloweenTallAnchors =
    [
        (PropKind.Pumpkin, 450d / 1672, 760d / 941),
        (PropKind.Gravestone, 1240d / 1672, 830d / 941),
        (PropKind.Cauldron, 1150d / 1672, 830d / 941),
        (PropKind.Candle, 450d / 1672, 620d / 941),
        (PropKind.Ghost, 1225d / 1672, 370d / 941),
        (PropKind.Spellbook, 520d / 1672, 830d / 941),
        (PropKind.Potion, 1160d / 1672, 880d / 941),
        (PropKind.Bat, 1205d / 1672, 488d / 941)
    ];
    private static readonly (PropKind Kind, double X, double Y)[] HalloweenWideAnchors =
    [
        (PropKind.Pumpkin, 240d / 1672, 700d / 941),
        (PropKind.Gravestone, 1240d / 1672, 755d / 941),
        (PropKind.Cauldron, 1360d / 1672, 755d / 941),
        (PropKind.Candle, 260d / 1672, 642d / 941),
        (PropKind.Ghost, 1520d / 1672, 500d / 941),
        (PropKind.Spellbook, 350d / 1672, 750d / 941),
        (PropKind.Potion, 1300d / 1672, 820d / 941),
        (PropKind.Bat, 1307d / 1672, 429d / 941)
    ];
    public static IReadOnlyList<(PropKind Kind, double X, double Y)> HalloweenAnchorsForAspect(double aspect) =>
        aspect < 1.35 ? HalloweenTallAnchors : aspect > 2.35 ? HalloweenWideAnchors : HalloweenLandscapeAnchors;
    private static readonly SceneDecorationAnchor[] Standard =
    [
        new(PropKind.Star, .19, .24, ScaleMultiplier: .6), new(PropKind.Star, .81, .31, ScaleMultiplier: 1.25),
        new(PropKind.LightCrystal, .31, .91, ScaleMultiplier: 1.45), new(PropKind.LightCrystal, .69, .91, ScaleMultiplier: .8)
    ];
    private static readonly SceneDecorationAnchor[] Winter =
    [
        new(PropKind.Snowman, .25, .90, ScaleMultiplier: 1.25),
        new(PropKind.Snowball, .39, .92, ParentKind: PropKind.Snowman, ParentOffsetX: 24, AlignGroundToParent: true, ScaleMultiplier: .5),
        new(PropKind.SnowPile, .52, .92, ParentKind: PropKind.Snowman, ParentOffsetX: -55, AlignGroundToParent: true, ScaleMultiplier: .65),
        new(PropKind.Icicle, .27, .12, ScaleMultiplier: .9, TetherUp: true),
        new(PropKind.PineTree, .75, .87, ScaleMultiplier: 1.4), new(PropKind.IceCrystal, .63, .90, ScaleMultiplier: .8)
    ];
    private static readonly SceneDecorationAnchor[] WinterFestival =
    [
        new(PropKind.Snowman, .25, .91, ScaleMultiplier: 1.25),
        new(PropKind.PineTree, .12, .90, ScaleMultiplier: 1.7),
        new(PropKind.Gift, .63, .86, ParentKind: PropKind.PineTree, ParentOffsetX: -24, AlignGroundToParent: true, ScaleMultiplier: .7),
        new(PropKind.Ornament, .63, .77, ParentKind: PropKind.PineTree, ParentOffsetX: -15, ParentOffsetY: -38, ScaleMultiplier: .58, TetherUp: true),
        new(PropKind.Bell, .63, .77, ParentKind: PropKind.PineTree, ParentOffsetX: 17, ParentOffsetY: -30, ScaleMultiplier: .45, TetherUp: true),
        new(PropKind.CandyCane, .43, .90, ScaleMultiplier: .8)
    ];
    private static readonly SceneDecorationAnchor[] Spring =
    [
        new(PropKind.Egg, .28, .89, ScaleMultiplier: .55), new(PropKind.ChocolateBunny, .70, .88, ScaleMultiplier: 1.35),
        new(PropKind.Nest, .39, .90, ScaleMultiplier: 1.55), new(PropKind.Flower, .69, .90, ScaleMultiplier: .9),
        new(PropKind.Butterfly, .77, .56, ScaleMultiplier: 1.1),
        new(PropKind.Egg, .50, .93, ParentKind: PropKind.Nest, ParentOffsetX: 8, ParentOffsetY: -13, ScaleMultiplier: .35)
    ];
    private static readonly SceneDecorationAnchor[] Halloween =
    [
        new(PropKind.Pumpkin, .18, .90, .92), new(PropKind.Gravestone, .82, .90, .92),
        new(PropKind.Cauldron, .64, .90, .92), new(PropKind.Candle, .89, .90, .92),
        new(PropKind.Spider, .17, .27, TetherUp: true), new(PropKind.Ghost, .77, .45),
        new(PropKind.Spellbook, .08, .90, .94), new(PropKind.Potion, .92, .80, .90),
        new(PropKind.Bat, .84, .62)
    ];
    private static readonly SceneDecorationAnchor[] NewYear =
    [
        new(PropKind.Rocket, .27, .90, ScaleMultiplier: .95), new(PropKind.Rocket, .73, .90, ScaleMultiplier: 1.6),
        new(PropKind.Sparkler, .32, .88, ScaleMultiplier: 1.2), new(PropKind.Confetti, .17, .60, ScaleMultiplier: .7),
        new(PropKind.PartyHat, .69, .89, ScaleMultiplier: 1.35), new(PropKind.Star, .83, .27, ScaleMultiplier: .85)
    ];
    private static readonly SceneDecorationAnchor[] Summer =
    [
        new(PropKind.Bubble, .20, .68, ScaleMultiplier: 1.3), new(PropKind.Bubble, .80, .73, ScaleMultiplier: .6),
        new(PropKind.WaterBalloon, .27, .90, ScaleMultiplier: .75), new(PropKind.WaterBalloon, .73, .91, ScaleMultiplier: 1.85),
        new(PropKind.Firefly, .17, .40, ScaleMultiplier: .65), new(PropKind.Firefly, .83, .48, ScaleMultiplier: 1.1)
    ];
    private static readonly SceneDecorationAnchor[] Harvest =
    [
        new(PropKind.LeafPile, .27, .89, ScaleMultiplier: 1.4), new(PropKind.LeafPile, .73, .89, ScaleMultiplier: .8),
        new(PropKind.Acorn, .39, .91, ScaleMultiplier: .45), new(PropKind.Acorn, .61, .91, ScaleMultiplier: .65),
        new(PropKind.Mushroom, .48, .93, ScaleMultiplier: 1.1), new(PropKind.Mushroom, .72, .91, ScaleMultiplier: .65)
    ];
    private static readonly SceneDecorationAnchor[] Arceus =
    [
        new(PropKind.Star, .18, .28, ScaleMultiplier: .55), new(PropKind.Star, .82, .34, ScaleMultiplier: 1.25),
        new(PropKind.LightCrystal, .18, .83, ScaleMultiplier: .7), new(PropKind.LightCrystal, .82, .83, ScaleMultiplier: 1.65)
    ];

    public static double GroundBandTop(string? theme) => (theme ?? "standard").Trim().ToLowerInvariant() switch
    {
        "arceus" => .72,
        "harvest" => .78,
        "winter" => .82,
        "spring" => .82,
        "summer" => .82,
        "halloween" => .82,
        "newyear" => .82,
        _ => .84
    };

    public static IReadOnlyList<SceneDecorationAnchor> ForTheme(string? theme, bool winterFestival = false)
    {
        string key = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
        return key switch
        {
            "winter" when winterFestival => WinterFestival,
            "winter" => Winter,
            "spring" => Spring,
            "halloween" => Halloween,
            "newyear" => NewYear,
            "summer" => Summer,
            "harvest" => Harvest,
            "arceus" => Arceus,
            _ => Standard
        };
    }

    /// <summary>Stable indices for mapping authored theme anchors to individual props, including duplicates.</summary>
    public static IReadOnlyList<(int Index, PropKind Kind)> ForThemeIndices(string? theme, bool winterFestival = false) =>
        ForTheme(theme, winterFestival).Select((anchor, index) => (index, anchor.Kind)).ToArray();

    /// <summary>Near foliage carries its discoveries with the faster camera plane.</summary>
    public static bool AttachesToForeground(string theme, int index, bool winterFestival = false) =>
        theme == "winter" && !winterFestival && index == 3 || theme == "spring" && index == 4
        || theme == "standard" && index == 0 || theme == "summer" && index is 4 or 5;

    /// <summary>Authored surface contacts in each scene's normalized 1672x941 frame.
    /// Portrait crops need inner shelves; wide crops put the fixed interface over
    /// the middle path, so those compositions use its side paving or nearer stones.</summary>
    public static IReadOnlyList<(int Index, double X, double Y)> SeasonalAnchorsForAspect(
        string? theme, double aspect, bool winterFestival = false)
    {
        bool portrait = aspect < 1.35;
        bool wide = aspect > 2.35;
        if (!string.Equals(theme?.Trim(), "winter", StringComparison.OrdinalIgnoreCase))
        {
            string key = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
            // Source pixels refer to painted horizontal paving, sand or grass.
            // They are never moved by the free-space placement search.
            return key switch
            {
                "standard" => [
                    (0, (portrait ? 460d : wide ? 400d : 330d) / 1672, (wide ? 500d : portrait ? 220d : 250d) / 941),
                    (1, (portrait ? 1200d : 1260d) / 1672, (wide ? 540d : 470d) / 941),
                    (2, (portrait ? 480d : 210d) / 1672, (portrait ? 760d : 595d) / 941),
                    (3, (portrait ? 1220d : 1270d) / 1672, (portrait ? 810d : 685d) / 941)],
                // The upper faces of the two Arceus platforms are the walkable
                // planes; points below y~640 land on their vertical stone walls.
                "arceus" => [
                    (0, (portrait ? 470d : wide ? 340d : 260d) / 1672, (wide ? 520d : 300d) / 941),
                    (1, (portrait ? 1200d : wide ? 1350d : 1440d) / 1672, (wide ? 480d : portrait ? 450d : 250d) / 941),
                    (2, (portrait ? 570d : wide ? 560d : 220d) / 1672, (wide ? 620d : 650d) / 941),
                    (3, (portrait ? 1140d : wide ? 1250d : 1280d) / 1672, (portrait ? 860d : 830d) / 941)],
                "spring" => [
                    (0, (portrait ? 440d : wide ? 450d : 425d) / 1672, 790d / 941),
                    (1, (portrait ? 480d : 1280d) / 1672, (portrait ? 760d : 675d) / 941),
                    (2, (portrait ? 600d : 555d) / 1672, (portrait ? 850d : 855d) / 941),
                    (3, (portrait ? 1200d : 1450d) / 1672, (portrait ? 745d : 645d) / 941),
                    (4, (portrait ? 1220d : 1400d) / 1672, (wide ? 520d : portrait ? 210d : 260d) / 941)],
                "summer" => [
                    (0, (portrait ? 500d : 400d) / 1672, 680d / 941),
                    (1, (portrait ? 1170d : 1330d) / 1672, (wide ? 670d : 620d) / 941),
                    (2, (portrait ? 480d : 240d) / 1672, (portrait ? 830d : 720d) / 941),
                    (3, (portrait ? 1160d : 1140d) / 1672, (portrait ? 835d : 860d) / 941),
                    (4, (portrait ? 460d : wide ? 330d : 340d) / 1672, (wide ? 540d : portrait ? 220d : 250d) / 941),
                    (5, (portrait ? 1210d : 1440d) / 1672, (wide ? 600d : portrait ? 730d : 600d) / 941)],
                "harvest" => [
                    (0, (portrait ? 450d : 230d) / 1672, (portrait ? 800d : 670d) / 941),
                    (1, (portrait ? 1220d : 1450d) / 1672, (portrait ? 790d : 720d) / 941),
                    (2, (portrait ? 480d : 350d) / 1672, (portrait ? 850d : 545d) / 941),
                    (3, (portrait ? 1200d : 1310d) / 1672, (portrait ? 830d : 675d) / 941),
                    (4, (portrait ? 510d : 230d) / 1672, (portrait ? 820d : 500d) / 941),
                    (5, (portrait ? 1160d : 1240d) / 1672, (portrait ? 765d : 680d) / 941)],
                "newyear" => [
                    (0, (portrait ? 560d : 420d) / 1672, (portrait ? 830d : 810d) / 941),
                    (1, (portrait ? 1210d : 1330d) / 1672, (portrait ? 810d : 770d) / 941),
                    (2, (portrait ? 470d : 390d) / 1672, (portrait ? 785d : 690d) / 941),
                    (3, (portrait ? 470d : 330d) / 1672, (wide ? 520d : 480d) / 941),
                    (4, (portrait ? 1180d : 1390d) / 1672, (portrait ? 740d : 640d) / 941),
                    (5, (portrait ? 1200d : 1300d) / 1672, (wide ? 510d : 250d) / 941)],
                _ => Array.Empty<(int, double, double)>()
            };
        }

        // Snow contacts avoid the lake and the two painted Pokémon groups. The
        // inner snowbank is the authored alternative when portrait cover removes
        // the outer banks. Icicles use the corresponding visible near-frame twig.
        return winterFestival
            ? [
                (0, (portrait ? 500d : 400d) / 1672, (portrait ? 820d : 800d) / 941),
                (1, (portrait ? 1170d : 1420d) / 1672, (portrait ? 780d : 650d) / 941),
                (5, (portrait ? 440d : 330d) / 1672, (portrait ? 850d : 840d) / 941)
            ]
            : [
                (0, (portrait ? 500d : 400d) / 1672, (portrait ? 820d : 800d) / 941),
                (3, (portrait ? 440d : wide ? 100d : 340d) / 1672,
                    (portrait ? 95d : wide ? 540d : 140d) / 941),
                (4, (portrait ? 1170d : 1420d) / 1672, (portrait ? 780d : 650d) / 941),
                (5, (portrait ? 1230d : 1320d) / 1672, (portrait ? 830d : 840d) / 941)
            ];
    }
}
