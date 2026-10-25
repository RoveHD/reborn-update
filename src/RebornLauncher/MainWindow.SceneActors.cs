using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    internal sealed record IntegratedScenePokemon(string Id, string DisplayName);

    private static readonly IReadOnlyDictionary<string, IntegratedScenePokemon> IntegratedScenePokemonByTheme =
        new Dictionary<string, IntegratedScenePokemon>(StringComparer.OrdinalIgnoreCase)
        {
            ["standard"] = new("umbreon", "NACHTARA / 0197"),
            ["halloween"] = new("pumpkaboo", "IRRBIS / 0710"),
            ["winter"] = new("glaceon", "GLAZIOLA / 0471"),
            ["spring"] = new("buneary", "HASPIROR / 0427"),
            ["summer"] = new("squirtle", "SCHIGGY / 0007"),
            ["harvest"] = new("leafeon", "FOLIPURBA / 0470"),
            ["newyear"] = new("jirachi", "JIRACHI / 0385"),
            ["arceus"] = new("arceus", "ARCEUS / 0493")
        };

    private IntegratedScenePokemon ScenePokemonFor(string theme) =>
        IntegratedScenePokemonByTheme.TryGetValue(theme, out var scenePokemon)
            ? scenePokemon
            : throw new ArgumentException("Unbekanntes Saisonthema.", nameof(theme));

    private void HideSeparatePokemonActors()
    {
        foreach (Image image in new[] { PokemonArtwork, LeftPokemonTop, LeftPokemonBottom, RightPokemonTop, RightPokemonBottom })
        {
            image.Source = null;
            image.Tag = null;
            image.Opacity = 0;
            image.Visibility = Visibility.Collapsed;
            image.IsHitTestVisible = false;
            image.Focusable = false;
            KeyboardNavigation.SetIsTabStop(image, false);
        }

        // The new illustrations already contain the Pokémon and their shadows.
        // Clear the legacy shadow layer so it cannot double-draw them.
        PokemonSceneryForeground.SetActorBounds(Array.Empty<Rect>());
        PokemonSceneryForeground.Opacity = 0;
    }

    internal object[] ExportSceneComposition()
    {
        var scenePokemon = ScenePokemonFor(_activeTheme);
        return [new { theme = _activeTheme, pokemonId = scenePokemon.Id, displayName = scenePokemon.DisplayName, integrated = true }];
    }
}
