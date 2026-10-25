using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private sealed record ThemeLook(string Title, string Subtitle, string Accent, string Highlight, string Ink, string Surface, string Line, string[] Artwork, string[] Names);
    private static readonly Dictionary<string, ThemeLook> ThemeLooks = new()
    {
        ["standard"] = new("ZURÜCK IN REBORN", "Deine Geschichte geht weiter.", "#C284EB", "#9BE6D2", "#0B090F", "#17131D", "#463B4E", ["umbreon", "absol"], ["NACHTARA / 0197", "ABSOL / 0359"]),
        ["halloween"] = new("DIE NACHT DER GEISTER", "Irrbis leuchtet. Die Geister erwachen.", "#F5A34B", "#DEC3FA", "#130B08", "#281A13", "#6A4226", ["pumpkaboo", "gengar"], ["IRRBIS / 0710", "GENGAR / 0094"]),
        ["winter"] = new("WINTERZAUBER", "Leise fällt der Schnee über Reborn.", "#9AD9F0", "#FFE1AA", "#08121D", "#152A3A", "#345768", ["delibird", "glaceon"], ["BOTOGEL / 0225", "GLAZIOLA / 0471"]),
        ["spring"] = new("REBORN BLÜHT AUF", "Neue Abenteuer zwischen Blüten und Sonnenstrahlen.", "#EBA5C7", "#B9E6A1", "#10160F", "#253122", "#486544", ["buneary", "shaymin"], ["HASPIROR / 0427", "SHAYMIN / 0492"]),
        ["summer"] = new("AB INS WASSER", "Sonne, Wellen und ein Sprung ins nächste Abenteuer.", "#6ED8EB", "#FFE391", "#07151D", "#12323C", "#306575", ["squirtle", "vaporeon"], ["SCHIGGY / 0007", "AQUANA / 0134"]),
        ["harvest"] = new("GOLDENE TAGE", "Das Laub tanzt. Reborn wartet.", "#E9AD6C", "#C4D89D", "#151109", "#302719", "#685338", ["leafeon", "sawsbuck"], ["FOLIPURBA / 0470", "KRONJUWILD / 0586"]),
        ["newyear"] = new("EIN NEUES ABENTEUER", "Ein Wunsch. Ein neues Jahr. Unzählige Möglichkeiten.", "#EFD082", "#B9C6FF", "#0D1020", "#1D2540", "#57577A", ["jirachi", "pikachu"], ["JIRACHI / 0385", "PIKACHU / 0025"]),
        ["arceus"] = new("DER URSPRUNG ERWACHT", "Ein seltener Augenblick jenseits der Zeit.", "#F4DF9D", "#F6F1DF", "#14120C", "#302A19", "#806D38", ["arceus"], ["ARCEUS / 0493"])
    };
    private readonly DispatcherTimer _calendarTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private string _activeTheme = "standard";
    private DateOnly _themeDay;
    private bool _alternateSeasonArtwork;

    private void InitializeCalendar()
    {
        if (!Guid.TryParse(_settings.ThemeDiscoverySeed, out _))
        {
            _settings.ThemeDiscoverySeed = Guid.NewGuid().ToString("D");
            if (!_smokeTest) _ = SavePresentationAsync();
        }
        RefreshCalendar();
        SizeChanged += (_, _) => UpdateSeasonClip();
        UpdateSeasonClip();
        if (!_smokeTest)
        {
            _calendarTimer.Tick += (_, _) => RefreshCalendar();
            _calendarTimer.Start();
        }
        Closed += (_, _) => _calendarTimer.Stop();
    }

    private void UpdateSeasonClip()
    {
        SeasonalBackdrop.Clip = new RectangleGeometry(new Rect(0, 42, Math.Max(0, ActualWidth - 348), Math.Max(0, ActualHeight - 90)));
    }
    private void RefreshCalendar(DateOnly? previewDate = null)
    {
        if (previewDate.HasValue && !_smokeTest) throw new InvalidOperationException("Nur für die Kalenderprüfung.");
        var today = previewDate ?? (_smokeTest ? new DateOnly(2026, 2, 1) : DateOnly.FromDateTime(DateTime.Now));
        if (today == _themeDay) return;
        _themeDay = today;
        ApplyTheme(ThemeCalendar.Resolve(today, _smokeTest ? null : _settings.ThemeDiscoverySeed), today);
    }

    private void ApplyTheme(string id, DateOnly day)
    {
        _activeTheme = id;
        _alternateSeasonArtwork = false;
        var theme = ThemeLooks[id];
        foreach (var (key, color) in new[] { ("Ink", theme.Ink), ("Surface", theme.Surface), ("Line", theme.Line), ("Violet", theme.Accent), ("Mint", theme.Highlight) })
            Resources[key] = BrushFromHex(color);
        Background = BrushFromHex(theme.Ink);
        UpdatePointer((Color)ColorConverter.ConvertFromString(theme.Accent));
        var glow = new RadialGradientBrush();
        var accent = (Color)ColorConverter.ConvertFromString(theme.Accent);
        glow.GradientStops.Add(new GradientStop(accent, 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(90, accent.R, accent.G, accent.B), 0.55));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1));
        AccentGlow.Background = glow;
        AmbientRing.Opacity = id == "standard" ? 0.23 : id == "arceus" ? 0.3 : 0.07;
        SeasonTitleText.Text = id == "spring" && ThemeCalendar.IsEasterSeason(day) ? "OSTERZAUBER IN REBORN" : theme.Title;
        SeasonSubtitleText.Text = id == "spring" && ThemeCalendar.IsEasterSeason(day) ? "Haspiror hat ein paar Überraschungen versteckt." : theme.Subtitle;
        ThemeInfoButton.Content = id == "arceus" ? "✦ EIN SELTENER BESUCH" : $"SAISON · {ThemeLabel(id).ToUpperInvariant()}";
        ThemeInfoButton.ToolTip = $"Automatisch nach Datum: {day:dd.MM.yyyy} · Kalender und Musikquellen";
        ShowArtwork(false);
        ApplyMotion();
        ApplyMusic();
        UpdateVersionSelectionUi();
    }

    private static string ThemeLabel(string id) => id switch
    {
        "halloween" => "Halloween", "winter" => "Winter", "spring" => "Frühling & Ostern", "summer" => "Sommer", "harvest" => "Herbst", "newyear" => "Neujahr", "arceus" => "Arceus", _ => "Standard"
    };

    private void ThemeInfo_Click(object sender, RoutedEventArgs e)
    {
        string message = "Das Design wechselt automatisch nach dem lokalen Datum – jedes Jahr.\n\n" +
            "07.01.–Ende Februar · Standard\nMärz–Mai · Frühling, rund um Ostern mit Ostergruß\nJuni–August · Sommer\nSeptember–14.10. und 03.–30.11. · Herbst\n15.10.–02.11. · Halloween\n01.–30.12. und 03.–06.01. · Winter/Weihnachten\n31.12.–02.01. · Silvester/Neujahr\n\n" +
            "Manchmal hält Reborn eine seltene Überraschung bereit …\n\nMusik dieser Saison:\n" + LauncherMusic.TrackCredit(_activeTheme) +
            "\n\nGrafiken: Pokémon-Artwork aus dem PokeAPI-Sprite-Repository (github.com/PokeAPI/sprites).\nSchrift: Barlow von Jeremy Tribby / The Barlow Project Authors, SIL Open Font License 1.1. Vollständige Lizenz und Musik-Credits sind in der Anwendung eingebettet und im Setup enthalten.";
        ShowPrompt(message, "Die Jahreszeiten von Reborn", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    internal void VerifyFreshInstall()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Installationsprüfung.");
        _previewMissingGame = true;
        _versionSelection = null;
        _localVersion = "0.0.0";
        UpdateVersionLabels();
        if (StartGameButton.Content.ToString() != "SPIELORDNER WÄHLEN" || RepairButton.IsEnabled)
            throw new InvalidOperationException("Neuinstallation bietet die Ordnerwahl nicht an.");
    }
    internal void EndFreshInstallPreview()
    {
        _previewMissingGame = false;
        UpdateVersionLabels();
    }
    internal void VerifyCalendarRollover()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Kalenderprüfung.");
        RefreshCalendar(new DateOnly(2027, 10, 31));
        if (_activeTheme != "halloween") throw new InvalidOperationException("Halloween nicht aktiviert.");
        RefreshCalendar(new DateOnly(2027, 11, 3));
        if (_activeTheme != "harvest") throw new InvalidOperationException("Saisonwechsel nicht übernommen.");
        RefreshCalendar(ThemeCalendar.EasterSunday(2028));
        if (_activeTheme != "spring" || !SeasonTitleText.Text.Contains("OSTER")) throw new InvalidOperationException("Ostertermin nicht übernommen.");
        RefreshCalendar(new DateOnly(2028, 1, 1));
        if (_activeTheme != "newyear") throw new InvalidOperationException("Jahreswechsel nicht übernommen.");
    }

    internal void PreviewTheme(string id)
    {
        if (!_smokeTest || !ThemeLooks.ContainsKey(id)) throw new InvalidOperationException("Ungültige Themenvorschau.");
        ProgressPanel.Visibility = Visibility.Collapsed;
        _versionSelection = new VersionSelection("7.0.0", "6.1.0", false);
        _localVersion = "7.0.0";
        UpdateVersionLabels();
        TopStatusText.Text = "Bereit";
        SetStatus("Bereit für dein nächstes Abenteuer.");
        PatchNotesText.Text = "Reborn im Wandel der Jahreszeiten.\n\nPassende Pokémon, lebendige Effekte und eigene Musik begleiten dich durchs Jahr. Das Design wechselt automatisch nach Datum.\n\nDeine Spiele und der Versionswechsel bleiben wie gewohnt erreichbar.\n\nDen Saisonkalender und Musikquellen findest du oben über die Saisonanzeige.";
        HeroScroller.ScrollToTop();
        ApplyTheme(id, id == "spring" ? ThemeCalendar.EasterSunday(2027) : new DateOnly(2027, 10, 31));
        if (PokemonArtwork.Source is null || _activeTheme != id) throw new InvalidOperationException("Themenwechsel fehlgeschlagen.");
    }
}
