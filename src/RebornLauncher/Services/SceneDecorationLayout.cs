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
    // Feet on the actual courtyard stones/wall ledges; the ghost hovers by the castle.
    // Coordinates refer to inhabited/halloween-landscape.png (1672 x 941).
    public static IReadOnlyList<(PropKind Kind, double X, double Y)> HalloweenLandscapeAnchors { get; } =
    [
        (PropKind.Pumpkin, 380d / 1672, 720d / 941),
        (PropKind.Gravestone, 1450d / 1672, 800d / 941),
        (PropKind.Cauldron, 1295d / 1672, 790d / 941),
        (PropKind.Candle, 262d / 1672, 620d / 941),
        (PropKind.Ghost, 1460d / 1672, 355d / 941),
        (PropKind.Spellbook, 230d / 1672, 730d / 941),
        (PropKind.Potion, 1490d / 1672, 690d / 941),
        (PropKind.Bat, 1290d / 1672, 540d / 941)
    ];
    private static readonly (PropKind Kind, double X, double Y)[] HalloweenTallAnchors =
    [
        (PropKind.Pumpkin, 440d / 1672, 785d / 941),
        (PropKind.Gravestone, 1270d / 1672, 735d / 941),
        (PropKind.Cauldron, 1170d / 1672, 835d / 941),
        (PropKind.Candle, 450d / 1672, 700d / 941),
        (PropKind.Ghost, 1255d / 1672, 370d / 941),
        (PropKind.Spellbook, 415d / 1672, 850d / 941),
        (PropKind.Potion, 1285d / 1672, 640d / 941),
        (PropKind.Bat, 1290d / 1672, 540d / 941)
    ];
    private static readonly (PropKind Kind, double X, double Y)[] HalloweenWideAnchors =
    [
        (PropKind.Pumpkin, 380d / 1672, 720d / 941),
        (PropKind.Gravestone, 1450d / 1672, 800d / 941),
        (PropKind.Cauldron, 1295d / 1672, 790d / 941),
        (PropKind.Candle, 262d / 1672, 620d / 941),
        (PropKind.Ghost, 1520d / 1672, 500d / 941),
        (PropKind.Spellbook, 230d / 1672, 730d / 941),
        (PropKind.Potion, 1490d / 1672, 690d / 941),
        (PropKind.Bat, 1290d / 1672, 570d / 941)
    ];
    public static IReadOnlyList<(PropKind Kind, double X, double Y)> HalloweenAnchorsForAspect(double aspect) =>
        aspect < 1.35 ? HalloweenTallAnchors : aspect > 2.35 ? HalloweenWideAnchors : HalloweenLandscapeAnchors;
    private static readonly SceneDecorationAnchor[] Standard =
    [
        new(PropKind.Star, .19, .24), new(PropKind.Star, .81, .31),
        new(PropKind.LightCrystal, .31, .91, .93), new(PropKind.LightCrystal, .69, .91, .93)
    ];
    private static readonly SceneDecorationAnchor[] Winter =
    [
        new(PropKind.Snowman, .25, .90, .92), new(PropKind.Snowball, .39, .92, .94),
        new(PropKind.SnowPile, .52, .92, .94), new(PropKind.Icicle, .16, .23, TetherUp: true),
        new(PropKind.PineTree, .75, .87, .91, ScaleMultiplier: 1.65), new(PropKind.IceCrystal, .63, .90, .92)
    ];
    private static readonly SceneDecorationAnchor[] WinterFestival =
    [
        new(PropKind.Snowman, .25, .91, .94),
        new(PropKind.PineTree, .12, .90, .94, ScaleMultiplier: 1.65),
        new(PropKind.Gift, .63, .86, ParentKind: PropKind.PineTree, ParentOffsetX: -70, AlignGroundToParent: true, ScaleMultiplier: .62),
        new(PropKind.Ornament, .63, .77, ParentKind: PropKind.PineTree, ParentOffsetX: -15, ParentOffsetY: -5, ScaleMultiplier: .58, TetherUp: true),
        new(PropKind.Bell, .63, .77, ParentKind: PropKind.PineTree, ParentOffsetX: 15, ParentOffsetY: -5, ScaleMultiplier: .58, TetherUp: true),
        new(PropKind.CandyCane, .43, .90, .92)
    ];
    private static readonly SceneDecorationAnchor[] Spring =
    [
        new(PropKind.Egg, .34, .90, .92), new(PropKind.ChocolateBunny, .08, .88, .90),
        new(PropKind.Nest, .64, .90, .92), new(PropKind.Flower, .82, .91, .93),
        new(PropKind.Butterfly, .77, .56), new(PropKind.Egg, .46, .92, .94)
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
        new(PropKind.Rocket, .32, .90, .93), new(PropKind.Rocket, .68, .90, .93),
        new(PropKind.Sparkler, .17, .90, .93), new(PropKind.Confetti, .17, .60),
        new(PropKind.PartyHat, .83, .89, .92), new(PropKind.Star, .83, .27)
    ];
    private static readonly SceneDecorationAnchor[] Summer =
    [
        new(PropKind.Bubble, .20, .68), new(PropKind.Bubble, .80, .73),
        new(PropKind.WaterBalloon, .36, .90, .93), new(PropKind.WaterBalloon, .65, .91, .94),
        new(PropKind.Firefly, .17, .40), new(PropKind.Firefly, .83, .48)
    ];
    private static readonly SceneDecorationAnchor[] Harvest =
    [
        new(PropKind.LeafPile, .16, .88, .91), new(PropKind.LeafPile, .84, .88, .91),
        new(PropKind.Acorn, .36, .91, .93), new(PropKind.Acorn, .64, .91, .93),
        new(PropKind.Mushroom, .46, .92, .94), new(PropKind.Mushroom, .75, .91, .93)
    ];
    private static readonly SceneDecorationAnchor[] Arceus =
    [
        new(PropKind.Star, .18, .28), new(PropKind.Star, .82, .34),
        new(PropKind.LightCrystal, .18, .83, .78), new(PropKind.LightCrystal, .82, .83, .82)
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
}
