using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokemonRebornLauncher;
using PokemonRebornLauncher.Services;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var expected = new Dictionary<string, (string Id, string Name)>(StringComparer.OrdinalIgnoreCase)
        {
            ["standard"] = ("umbreon", "NACHTARA / 0197"),
            ["halloween"] = ("pumpkaboo", "IRRBIS / 0710"),
            ["winter"] = ("glaceon", "GLAZIOLA / 0471"),
            ["spring"] = ("buneary", "HASPIROR / 0427"),
            ["summer"] = ("squirtle", "SCHIGGY / 0007"),
            ["harvest"] = ("leafeon", "FOLIPURBA / 0470"),
            ["newyear"] = ("jirachi", "JIRACHI / 0385"),
            ["arceus"] = ("arceus", "ARCEUS / 0493")
        };

        var settings = new LauncherSettings();
        foreach (string theme in expected.Keys)
            settings.ThemeArtworkSelections[theme] = MainPokemonSelection.GetOptions(theme, true)[1].Id;
        var roundTrip = LauncherSettings.Parse(JsonSerializer.Serialize(settings));
        Check(expected.Keys.All(theme => roundTrip.ThemeArtworkSelections.ContainsKey(theme)),
            "Legacy per-theme artwork selections were removed while loading settings.");
        foreach (string theme in expected.Keys)
            Check(roundTrip.ThemeArtworkSelections[theme] == settings.ThemeArtworkSelections[theme],
                $"Legacy selection for {theme} did not survive the settings round-trip.");
        var invalid = LauncherSettings.Parse("""{"themeArtworkSelections":{"halloween":"../../evil","winter":"not-a-pokemon","unknown":"pikachu"}}""");
        Check(invalid.ThemeArtworkSelections.Count == 0, "Invalid legacy artwork selections were accepted.");

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true) { Width = 1280, Height = 800 };
        try
        {
            app.MainWindow = window;
            window.Show();
            Pump();
            window.BeginAnimation(UIElement.OpacityProperty, null);
            window.Opacity = 1;

            typeof(MainWindow).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(window, roundTrip);
            Invoke(window, "ApplySettingsToUi");
            var name = (TextBlock)window.FindName("PokemonNameText");
            var oldImages = new[] { "PokemonArtwork", "LeftPokemonTop", "LeftPokemonBottom", "RightPokemonTop", "RightPokemonBottom" }
                .Select(id => (Image)window.FindName(id)).ToArray();

            foreach (var (theme, scene) in expected)
            foreach (bool daylight in new[] { false, true })
            {
                Invoke(window, "PreviewTheme", theme, daylight);
                Pump();
                window.UpdateLayout();
                Check(name.Text == scene.Name, $"{theme}/{(daylight ? "day" : "night")} did not show its fixed scene name.");
                Check(roundTrip.ThemeArtworkSelections[theme] != scene.Id,
                    $"Test setup for {theme} does not contain an ignored alternate selection.");
                Check(oldImages.All(image => image.Visibility == Visibility.Collapsed && image.Source is null && !image.Focusable && !image.IsHitTestVisible),
                    $"A separate image or picker hit area remains active in {theme}.");

                var rows = (object[])typeof(MainWindow).GetMethod("ExportSceneComposition", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(window, null)!;
                Check(rows.Length == 1, $"{theme} must report a single integrated Pokémon scene.");
                string row = JsonSerializer.Serialize(rows[0]);
                using var document = JsonDocument.Parse(row);
                Check(document.RootElement.GetProperty("theme").GetString() == theme
                    && document.RootElement.GetProperty("pokemonId").GetString() == scene.Id
                    && document.RootElement.GetProperty("displayName").GetString() == scene.Name
                    && document.RootElement.GetProperty("integrated").GetBoolean(),
                    $"Integrated scene metadata did not match {theme}.");
            }

            Check(roundTrip.ThemeArtworkSelections.Count == expected.Count,
                "Switching scenes changed saved legacy selections.");
            Invoke(window, "VerifyPresentationControls");
            Console.WriteLine("FIXED_SCENE_POKEMON_OK 8 themes × 2 lighting states; integrated labels; hidden images/picker; legacy preferences retained");
            return 0;
        }
        finally { window.Close(); app.Shutdown(); }
    }

    private static void Invoke(MainWindow window, string name, params object[] args) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
