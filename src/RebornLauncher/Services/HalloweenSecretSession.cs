namespace PokemonRebornLauncher.Services;

public sealed record HalloweenSecretDiscovery(string Name, string Hint);

/// <summary>Session discoveries survive theme changes; an unfinished recipe does not.</summary>
public sealed class HalloweenSecretSession
{
    private readonly HashSet<string> discovered = [];
    private int recipeStep;
    public IReadOnlyCollection<string> Discovered => discovered;
    public void Interrupt() => recipeStep = 0;
    public HalloweenSecretDiscovery? Activate(PropKind kind)
    {
        HalloweenSecretDiscovery? result = null;
        if (kind == PropKind.Spellbook) result = Discover("Die vergessene Formel", "Kerzenlicht, Trank, Kessel");
        if (kind == PropKind.Bat) result = Discover("Wächter der Nacht", "Ein leiser Flügelschlag über dem alten Gemäuer.");
        recipeStep = kind switch
        {
            PropKind.Candle => 1,
            PropKind.Potion when recipeStep == 1 => 2,
            PropKind.Cauldron when recipeStep == 2 => 3,
            _ => 0
        };
        if (recipeStep == 3)
        {
            recipeStep = 0;
            result = Discover("Nachtelixier", "Die vergessene Formel ist erwacht.");
        }
        return result;
    }
    private HalloweenSecretDiscovery? Discover(string name, string hint) => discovered.Add(name) ? new(name, hint) : null;
}
