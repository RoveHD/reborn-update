namespace PokemonRebornLauncher.Services;

/// <summary>Safe, theme-specific choices for the large Pokémon shown on the launcher stage.</summary>
public static class MainPokemonSelection
{
    public sealed record Option(string Id, string DisplayName);
    private sealed record ThemeChoices(Option DayDefault, Option NightDefault, Option[] Alternatives);

    private static Option P(string id, string name, int dex) => new(id, $"{name.ToUpperInvariant()} / {dex:0000}");
    private static readonly IReadOnlyDictionary<string, ThemeChoices> Choices = new Dictionary<string, ThemeChoices>(StringComparer.OrdinalIgnoreCase)
    {
        ["standard"] = new(P("espeon", "Psiana", 196), P("umbreon", "Nachtara", 197), [P("absol", "Absol", 359), P("eevee", "Evoli", 133), P("lucario", "Lucario", 448), P("sylveon", "Feelinara", 700), P("leafeon", "Folipurba", 470)]),
        ["halloween"] = new(P("pumpkaboo", "Irrbis", 710), P("pumpkaboo", "Irrbis", 710), [P("gengar", "Gengar", 94), P("mimikyu", "Mimigma", 778), P("misdreavus", "Traunfugil", 200), P("chandelure", "Skelabra", 609), P("giratina", "Giratina", 487)]),
        ["winter"] = new(P("glaceon", "Glaziola", 471), P("glaceon", "Glaziola", 471), [P("delibird", "Botogel", 225), P("alolan-vulpix", "Vulpix", 37), P("snorunt", "Schneppke", 361), P("spheal", "Seemops", 363), P("lapras", "Lapras", 131)]),
        ["spring"] = new(P("buneary", "Haspiror", 427), P("buneary", "Haspiror", 427), [P("shaymin", "Shaymin", 492), P("togepi", "Togepi", 175), P("sylveon", "Feelinara", 700), P("ribombee", "Bandelby", 743), P("eevee", "Evoli", 133)]),
        ["summer"] = new(P("squirtle", "Schiggy", 7), P("squirtle", "Schiggy", 7), [P("vaporeon", "Aquana", 134), P("lapras", "Lapras", 131), P("psyduck", "Enton", 54), P("mantyke", "Mantirps", 458), P("spheal", "Seemops", 363)]),
        ["harvest"] = new(P("leafeon", "Folipurba", 470), P("leafeon", "Folipurba", 470), [P("sawsbuck", "Kronjuwild", 586), P("rowlet", "Bauz", 722), P("vulpix", "Vulpix", 37), P("deerling", "Sesokitz", 585), P("shaymin", "Shaymin", 492)]),
        ["newyear"] = new(P("jirachi", "Jirachi", 385), P("jirachi", "Jirachi", 385), [P("pikachu", "Pikachu", 25), P("victini", "Victini", 494), P("clefairy", "Piepi", 35), P("ampharos", "Ampharos", 181), P("togekiss", "Togekiss", 468)]),
        ["arceus"] = new(P("arceus", "Arceus", 493), P("arceus", "Arceus", 493), [P("dialga", "Dialga", 483), P("palkia", "Palkia", 484), P("giratina", "Giratina", 487), P("togekiss", "Togekiss", 468), P("victini", "Victini", 494)])
    };

    private static readonly HashSet<string> ArtworkIds = Choices.Values
        .SelectMany(theme => new[] { theme.DayDefault, theme.NightDefault }.Concat(theme.Alternatives))
        .Select(option => option.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Option> GetOptions(string theme, bool daylight)
    {
        if (!Choices.TryGetValue(theme, out var choices)) throw new ArgumentException("Unbekanntes Thema.", nameof(theme));
        return Array.AsReadOnly(new[] { daylight ? choices.DayDefault : choices.NightDefault }.Concat(choices.Alternatives).ToArray());
    }

    public static Option Resolve(string theme, bool daylight, string? selectedId)
    {
        var options = GetOptions(theme, daylight);
        return options.Skip(1).FirstOrDefault(option => string.Equals(option.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ?? options[0];
    }

    /// <summary>Returns null when the next click returns to the lighting-aware theme default.</summary>
    public static string? NextSavedSelection(string theme, bool daylight, string? selectedId)
    {
        var options = GetOptions(theme, daylight);
        var current = Resolve(theme, daylight, selectedId);
        int index = Array.FindIndex(options.ToArray(), option => option.Id == current.Id);
        int next = (index + 1) % options.Count;
        return next == 0 ? null : options[next].Id;
    }

    public static Dictionary<string, string> SanitizeSavedSelections(IReadOnlyDictionary<string, string>? selections)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (selections is null) return result;
        foreach (var (theme, id) in selections)
        {
            if (!Choices.TryGetValue(theme, out var choices) || string.IsNullOrWhiteSpace(id)) continue;
            var valid = choices.Alternatives.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase));
            if (valid is not null) result[theme] = valid.Id;
        }
        return result;
    }

    public static bool IsSafeArtworkId(string id) => ArtworkIds.Contains(id);
}
