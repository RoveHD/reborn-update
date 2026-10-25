using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        ["standard"] = new("ZURÜCK IN REBORN", "Deine Geschichte geht weiter.", "#C284EB", "#9BE6D2", "#0B090F", "#17131D", "#463B4E", ["umbreon", "absol"], ["NACHTARA / 0197", "PSIANA / 0196"]),
        ["halloween"] = new("DIE NACHT DER GEISTER", "Irrbis leuchtet. Die Geister erwachen.", "#F5A34B", "#DEC3FA", "#130B08", "#281A13", "#6A4226", ["pumpkaboo", "gengar"], ["IRRBIS / 0710", "GENGAR / 0094"]),
        ["winter"] = new("WINTERZAUBER", "Leise fällt der Schnee über Reborn.", "#9AD9F0", "#FFE1AA", "#08121D", "#152A3A", "#345768", ["delibird", "glaceon"], ["GLAZIOLA / 0471", "BOTOGEL / 0225"]),
        ["spring"] = new("REBORN BLÜHT AUF", "Neue Abenteuer zwischen Blüten und Sonnenstrahlen.", "#EBA5C7", "#B9E6A1", "#10160F", "#253122", "#486544", ["buneary", "shaymin"], ["HASPIROR / 0427", "SHAYMIN / 0492"]),
        ["summer"] = new("AB INS WASSER", "Sonne, Wellen und ein Sprung ins nächste Abenteuer.", "#6ED8EB", "#FFE391", "#07151D", "#12323C", "#306575", ["squirtle", "vaporeon"], ["SCHIGGY / 0007", "AQUANA / 0134"]),
        ["harvest"] = new("GOLDENE TAGE", "Das Laub tanzt. Reborn wartet.", "#E9AD6C", "#C4D89D", "#151109", "#302719", "#685338", ["leafeon", "sawsbuck"], ["FOLIPURBA / 0470", "KRONJUWILD / 0586"]),
        ["newyear"] = new("EIN NEUES ABENTEUER", "Ein Wunsch. Ein neues Jahr. Unzählige Möglichkeiten.", "#EFD082", "#B9C6FF", "#0D1020", "#1D2540", "#57577A", ["jirachi", "pikachu"], ["JIRACHI / 0385", "PIKACHU / 0025"]),
        ["arceus"] = new("DER URSPRUNG ERWACHT", "Ein seltener Augenblick jenseits der Zeit.", "#F4DF9D", "#F6F1DF", "#14120C", "#302A19", "#806D38", ["arceus"], ["ARCEUS / 0493"])
    };
    private readonly DispatcherTimer _calendarTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private string _activeTheme = "standard";
    private DateOnly _themeDay;
    private bool _daylight;
    private bool _themeApplied;
    private static readonly Dictionary<string, string[]> ThemeCast = new()
    {
        ["standard"] = ["umbreon", "espeon", "absol", "eevee", "lucario"],
        ["halloween"] = ["pumpkaboo", "gengar", "mimikyu", "misdreavus", "chandelure"],
        ["winter"] = ["glaceon", "delibird", "alolan-vulpix", "snorunt", "spheal"],
        ["spring"] = ["buneary", "shaymin", "togepi", "sylveon", "ribombee"],
        ["summer"] = ["squirtle", "vaporeon", "lapras", "psyduck", "mantyke"],
        ["harvest"] = ["leafeon", "sawsbuck", "rowlet", "vulpix", "deerling"],
        ["newyear"] = ["jirachi", "pikachu", "victini", "clefairy", "ampharos"],
        ["arceus"] = ["arceus", "dialga", "palkia", "giratina", "togekiss"]
    };
    private static readonly Dictionary<string, string[]> DayPalettes = new()
    {
        ["standard"] = ["#302C36", "#403946", "#706176", "#C99CE1", "#A0CFC2"],
        ["halloween"] = ["#392C27", "#49372F", "#806247", "#E8AC72", "#CFB2E2"],
        ["winter"] = ["#293943", "#344852", "#617F8D", "#A0CEDD", "#DFCAA1"],
        ["spring"] = ["#303C2D", "#3F4C38", "#6D8260", "#DBABC2", "#B4CF99"],
        ["summer"] = ["#263B40", "#304B51", "#60828A", "#8DCAD5", "#DDCA93"],
        ["harvest"] = ["#3C3226", "#4C4030", "#897150", "#DDB284", "#BEC99B"],
        ["newyear"] = ["#323442", "#414455", "#78768B", "#DDC58F", "#BAC4E7"],
        ["arceus"] = ["#3B372A", "#4B4533", "#8B7C55", "#DECA96", "#E1DAC2"]
    };

    // Coordinates describe a scene, not window gutters: x is the centre, y the
    // foot line, and size the fraction of the stage height. All three actors
    // stay inside the stage, including the curious companion during its greeting.
    private sealed record ActorPlace(double X, double Foot, double Size);
    private sealed record SceneComposition(ActorPlace Main, ActorPlace Companion, ActorPlace Cameo);
    private static readonly Dictionary<string, SceneComposition> Compositions = new()
    {
        ["standard"] = new(new(.24,.94,.83), new(.76,.86,.50), new(.52,.56,.32)),
        ["winter"] = new(new(.20,.94,.86), new(.81,.88,.49), new(.55,.72,.36)),
        ["halloween"] = new(new(.51,.95,.78), new(.16,.65,.45), new(.85,.87,.38)),
        ["spring"] = new(new(.79,.93,.85), new(.24,.90,.53), new(.48,.53,.34)),
        ["summer"] = new(new(.27,.96,.73), new(.73,.91,.66), new(.50,.42,.28)),
        ["harvest"] = new(new(.43,.94,.85), new(.84,.72,.42), new(.13,.90,.36)),
        ["newyear"] = new(new(.72,.68,.60), new(.18,.96,.63), new(.46,.94,.36)),
        ["arceus"] = new(new(.49,.91,.88), new(.13,.63,.40), new(.85,.85,.40))
    };

    private void UpdateResponsiveLayout()
    {
        if (!_presentationReady) return;
        double contentWidth = Math.Min(1440, Math.Max(0, ActualWidth - 48));
        bool compact = ActualWidth < 1200;
        bool wide = ActualWidth >= 1520;
        HeroTextColumn.Width = new GridLength(compact ? 220 : wide ? 340 : 300);
        NotesColumn.Width = new GridLength(Math.Clamp(contentWidth - 704, 280, 712));
        LogoFrame.Width = compact ? 200 : wide && ActualHeight >= 720 ? 300 : 260;
        LogoFrame.Height = compact ? 112 : wide && ActualHeight >= 720 ? 168 : 146;
        // Leave enough room for all controls at the minimum size and stop the
        // artwork from taking over a tall or maximized window.
        double stageHeight = Math.Clamp(202 + (ActualHeight - 640) * .30, 202, 320);
        HeroRow.Height = new GridLength(stageHeight + 40);
        LogoArtwork.Margin = new Thickness(4);
        if (StageArtArea.ActualWidth < 1 || StageArtArea.ActualHeight < 1) return;
        var composition = Compositions[_activeTheme];
        PlaceActor(PokemonArtwork, composition.Main, .30);
        PlaceActor(LeftPokemonBottom, composition.Companion, .23);
        PlaceActor(RightPokemonTop, composition.Cameo, .16);
        LeftPokemonTop.Visibility = RightPokemonBottom.Visibility = Visibility.Collapsed;
        ApplyCameoPosition(true);
        PokemonSceneryForeground.SetActorBounds(new[] { PokemonArtwork, LeftPokemonBottom, RightPokemonTop }
            .Select(image => new Rect(image.Margin.Left, image.Margin.Top, image.Width, image.Height)));
        // Exclusions are in the same DIP space as the props host. Props can use
        // the open ground and sky without covering an actor or stealing its hit.
        EventPropsView.SetExclusionZones(new[] { PokemonArtwork, LeftPokemonBottom, RightPokemonTop }
            .Select(image => new Rect(image.Margin.Left - 6, image.Margin.Top - 8, image.Width + 12, image.Height + 16)));
    }

    private void PlaceActor(System.Windows.Controls.Image image, ActorPlace place, double maxWidth)
    {
        double width = StageArtArea.ActualWidth, height = StageArtArea.ActualHeight;
        double size = Math.Min(height * place.Size, width * maxWidth);
        double x = Math.Clamp(width * place.X - size / 2, 8, Math.Max(8, width - size - 8));
        double y = Math.Clamp(height * place.Foot - size, 8, Math.Max(8, height - size - 8));
        image.Width = image.Height = size;
        image.HorizontalAlignment = HorizontalAlignment.Left;
        image.VerticalAlignment = VerticalAlignment.Top;
        image.Margin = new Thickness(x, y, 0, 0);
        image.Visibility = Visibility.Visible;
    }

    private void InitializeCalendar()
    {
        if (!Guid.TryParse(_settings.ThemeDiscoverySeed, out _))
        {
            _settings.ThemeDiscoverySeed = Guid.NewGuid().ToString("D");
            if (!_smokeTest) _ = SavePresentationAsync();
        }
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        StageArtArea.SizeChanged += (_, _) => UpdateResponsiveLayout();
        SyncLightingButton();
        RefreshCalendar();
        if (!_smokeTest)
        {
            _calendarTimer.Tick += (_, _) => RefreshCalendar();
            _calendarTimer.Start();
        }
        Closed += (_, _) => _calendarTimer.Stop();
    }

    private string LightingLabel => _settings.ThemeLightingMode switch { "day" => "Immer Tag", "auto" => "Automatisch", _ => "Immer Nacht" };
    private bool LightingForHour(int hour) => _settings.ThemeLightingMode switch { "day" => true, "auto" => hour is >= 7 and < 19, _ => false };

    private void SyncLightingButton()
    {
        LightingModeButton.Content = LightingLabel.ToUpperInvariant() + " ▾";
        if (_themeDay != default)
            ThemeInfoButton.ToolTip = $"Saison nach Datum: {_themeDay:dd.MM.yyyy} · Licht: {LightingLabel} · Kalender und Musikquellen";
    }

    private void LightingModeButton_Click(object sender, RoutedEventArgs e)
    {
        LightingModeButton.ContextMenu.PlacementTarget = LightingModeButton;
        LightingModeButton.ContextMenu.Placement = PlacementMode.Bottom;
        LightingModeButton.ContextMenu.IsOpen = true;
    }

    private async void LightingMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string mode } || mode is not ("night" or "day" or "auto")) return;
        _settings.ThemeLightingMode = mode;
        SyncLightingButton();
        RefreshCalendar();
        await SavePresentationAsync();
    }

    internal void VerifyLightingControls()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        string original = _settings.ThemeLightingMode, theme = _activeTheme;
        DateOnly day = _themeDay;
        bool daylight = _daylight;
        foreach (string mode in new[] { "night", "day", "auto" })
        {
            var item = LightingModeButton.ContextMenu.Items.OfType<MenuItem>().Single(item => item.Tag as string == mode);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (_settings.ThemeLightingMode != mode) throw new InvalidOperationException("Die Licht-Menüoption reagiert nicht.");
            bool expected = LightingForHour(DateTime.Now.Hour);
            if (_daylight != expected) throw new InvalidOperationException("Lichtauswahl wurde nicht angewendet: " + mode);
            RefreshCalendar();
            if (_daylight != expected || mode == "night" && (LightingForHour(2) || LightingForHour(12)) || mode == "day" && (!LightingForHour(2) || !LightingForHour(12)) || mode == "auto" && (LightingForHour(2) || !LightingForHour(12)))
                throw new InvalidOperationException("Der Kalender überschreibt die Lichtauswahl: " + mode);
        }
        _settings.ThemeLightingMode = original;
        SyncLightingButton();
        _daylight = daylight;
        _themeDay = day;
        ApplyTheme(theme, day);
    }

    private void RefreshCalendar(DateOnly? previewDate = null)
    {
        if (previewDate.HasValue && !_smokeTest) throw new InvalidOperationException("Nur für die Kalenderprüfung.");
        var today = previewDate ?? (_smokeTest ? new DateOnly(2026, 2, 1) : DateOnly.FromDateTime(DateTime.Now));
        bool daylight = LightingForHour(DateTime.Now.Hour);
        if (today == _themeDay && daylight == _daylight) return;
        _daylight = daylight;
        _themeDay = today;
        ApplyTheme(ThemeCalendar.Resolve(today, _smokeTest ? null : _settings.ThemeDiscoverySeed), today, animate: true);
    }

    private void ApplyTheme(string id, DateOnly day, bool animate = false)
    {
        bool lightingOnly = _themeApplied && _activeTheme == id;
        animate = animate && _themeApplied && IsLoaded && !_settings.ReduceMotion && SystemParameters.ClientAreaAnimation;
        _activeTheme = id;

        var theme = ThemeLooks[id];
        var palette = _daylight ? DayPalettes[id] : [theme.Ink, theme.Surface, theme.Line, theme.Accent, theme.Highlight];
        foreach (var (key, color) in new[] { ("Ink", palette[0]), ("Surface", palette[1]), ("Line", palette[2]), ("Violet", palette[3]), ("Mint", palette[4]), ("Text", _daylight ? "#F0E9E2" : "#F4F0F0"), ("Muted", _daylight ? "#D0C7D0" : "#B4AEBB"), ("Faint", _daylight ? "#C7BDC8" : "#918797"), ("CtaText", "#100B15") })
            MorphResource(key, color, animate);
        Background = (Brush)Resources["Ink"];
        UpdatePointer((Color)ColorConverter.ConvertFromString(palette[3]));
        var accent = (Color)ColorConverter.ConvertFromString(palette[3]);
        MorphGlow(accent, animate);
        StageCaptionText.Text = id switch
        {
            "halloween" => "ZU BESUCH BEI DEN GEISTERN", "winter" => "SPUREN IM FRISCHEN SCHNEE",
            "spring" => "ZWISCHEN BLÜTEN UND OSTERZAUBER", "summer" => "EIN SOMMER AM WASSER",
            "harvest" => "EIN ABENTEUER IM GOLDENEN LAUB", "newyear" => "WÜNSCH DIR EIN ABENTEUER",
            "arceus" => "EIN AUGENBLICK FÜR DIE EWIGKEIT", _ => _daylight ? "DEINE BEGLEITER DURCH DEN TAG" : "DEINE BEGLEITER DURCH DIE NACHT"
        };
        SeasonTitleText.Text = id == "spring" && ThemeCalendar.IsEasterSeason(day) ? "OSTERZAUBER IN REBORN" : id == "halloween" && _daylight ? "DIE ZEIT DER GEISTER" : theme.Title;
        SeasonSubtitleText.Text = id == "spring" && ThemeCalendar.IsEasterSeason(day) ? "Haspiror hat ein paar Überraschungen versteckt." :
            id == "summer" && !_daylight ? "Mondlicht, Wellen und ein Sprung ins nächste Abenteuer." : theme.Subtitle;
        ThemeInfoButton.Content = id == "arceus" ? (_daylight ? "✦ ARCEUS · TAG" : "✦ ARCEUS · NACHT") : $"{ThemeLabel(id).ToUpperInvariant()} · {(_daylight ? "TAG" : "NACHT")}";
        ThemeInfoButton.ToolTip = $"Saison nach Datum: {day:dd.MM.yyyy} · Licht: {LightingLabel} · Kalender und Musikquellen";
        PokemonSceneryForeground.SetTheme(id, _daylight, animate);
        ShowArtwork(animate);
        ApplyMotion(animateLighting: animate, restartEntrance: !lightingOnly);
        ApplyMusic();
        UpdateVersionLabels();
        _themeApplied = true;
    }

    private static string ThemeLabel(string id) => id switch
    {
        "halloween" => "Halloween", "winter" => "Winter", "spring" => "Frühling & Ostern", "summer" => "Sommer", "harvest" => "Herbst", "newyear" => "Neujahr", "arceus" => "Arceus", _ => "Standard"
    };

    private void ThemeInfo_Click(object sender, RoutedEventArgs e)
    {
        string message = "Die Saison wechselt automatisch nach dem lokalen Datum – jedes Jahr.\nLicht: " + LightingLabel + ". Über die Lichtauswahl kannst du immer Nacht, immer Tag oder automatisch wählen (Tag: 07–19 Uhr).\n\n" +
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

    internal void PreviewTheme(string id, bool? daylight = null)
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
        if (daylight.HasValue)
        {
            _settings.ThemeLightingMode = daylight.Value ? "day" : "night";
            SyncLightingButton(); // Preview only: match the switch label without saving settings.
        }
        _daylight = daylight ?? LightingForHour(DateTime.Now.Hour);
        _themeDay = id == "spring" ? ThemeCalendar.EasterSunday(2027) : id == "winter" ? new DateOnly(2027, 12, 15) : new DateOnly(2027, 10, 31);
        ApplyTheme(id, _themeDay);
        if (PokemonArtwork.Source is null || _activeTheme != id) throw new InvalidOperationException("Themenwechsel fehlgeschlagen.");
    }
}
