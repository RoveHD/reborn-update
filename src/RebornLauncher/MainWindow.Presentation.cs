using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private LauncherMusic? _music;
    private bool _presentationReady;
    private bool _presentationClosed;
    private bool _gameRunning;
    private bool _previewAudio;
    private readonly DispatcherTimer _volumeSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };

    private void InitializePresentation()
    {
        if (_presentationReady) return;
        _presentationReady = true;
        _volumeSaveTimer.Tick += async (_, _) => { _volumeSaveTimer.Stop(); await SavePresentationAsync(); };
        StateChanged += (_, _) => { ApplyMusic(); ApplyMotion(); };
        Closed += (_, _) => { _presentationClosed = true; _volumeSaveTimer.Stop(); _music?.Dispose(); _updates.Dispose(); };
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged;
        InitializeNavigation();
        InitializePointer();
        InitializeCalendar();
        InitializeEvents();
        InitializeBloodEasterEgg();
        InitializeAtmosphere();
        InitializeGengarEncounter();
        Activated += (_, _) => { ApplyMotion(); ApplyEventPresentation(); };
        Deactivated += (_, _) => { ApplyMotion(); ApplyEventPresentation(); };
        if (!_smokeTest)
        {
            _music = new LauncherMusic();
            _music.StatusChanged += ShowMusicStatus;
            ShowMusicStatus(_music.Status);
        }
        ApplyMusic();
    }

    private void ShowMusicStatus(string status)
    {
        MusicStatusText.Text = status.Contains("nicht") ? "Fehler" : status.Contains("pausiert") ? "Pause" : status.Contains("aus") ? "Aus" : status.Contains("geladen") ? "Lädt" : "An";
        MusicStatusText.ToolTip = status;
    }

    internal void VerifyVisibleControls()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        VerifySceneryLayout();
        foreach (FrameworkElement control in new FrameworkElement[] { HomeNavButton, OpenPatchNotesButton, ExtrasNavButton, SettingsNavButton, LogoFrame, StartGameButton, LocalVersionText, RemoteVersionText, HomeVersionSummary, StatusText, UpdateBadgeBorder, LauncherStatusText })
        {
            if (!control.IsVisible) throw new InvalidOperationException($"Hauptansicht-Control ist nicht sichtbar: {control.Name}");
            var bounds = control.TransformToAncestor(this).TransformBounds(new Rect(control.RenderSize));
            if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > ActualWidth + 1 || bounds.Bottom > ActualHeight + 1)
                throw new InvalidOperationException($"Bedienelement außerhalb des Fensters: {control.Name}; Bounds={bounds}; Fenster={ActualWidth:0.##}×{ActualHeight:0.##}; Transform={control.RenderTransform.Value}");
        }
        if (ExtrasHost.IsVisible || SettingsHost.IsVisible || PatchNotesHost.IsVisible)
            throw new InvalidOperationException("Ein Dialoghost liegt im Startbildschirm offen.");
        foreach (FrameworkElement control in new FrameworkElement[] { CancelButton, ProgressPanel, DownloadProgressBar })
        {
            if (!control.IsVisible) continue;
            var bounds = control.TransformToAncestor(this).TransformBounds(new Rect(control.RenderSize));
            if (!new Rect(0, 0, ActualWidth, ActualHeight).Contains(bounds))
                throw new InvalidOperationException("Download-Bedienung abgeschnitten: " + control.Name);
        }
    }

    internal void VerifySceneryLayout()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        UpdateLayout();
        if (HeroScroller.ScrollableHeight > 1 || HeroScroller.ScrollableWidth > 1 || HeroScroller.VerticalOffset > 1)
            throw new InvalidOperationException($"Die Hauptansicht benötigt Scrollen: {HeroScroller.ScrollableHeight:0.0}px.");
        Rect WindowBounds(FrameworkElement element) => element.TransformToAncestor(this).TransformBounds(new Rect(element.RenderSize));
        if (StageArtArea.ActualWidth < ActualWidth - 2 || StageArtArea.ActualHeight < ActualHeight - 104)
            throw new InvalidOperationException($"Die Atmosphärenszene füllt den mittleren Viewport nicht: {StageArtArea.ActualWidth:0}×{StageArtArea.ActualHeight:0} von {ActualWidth:0}×{ActualHeight:0}.");
        if (HeroPanel.ActualWidth > 500.5 || StartGameButton.ActualWidth > 480.5 || StartGameButton.ActualWidth < 320)
            throw new InvalidOperationException("Die mittlere Spalte oder der Startknopf hat die falsche Breite.");
        var scenePokemon = ScenePokemonFor(_activeTheme);
        var oldActorImages = new[] { PokemonArtwork, LeftPokemonTop, LeftPokemonBottom, RightPokemonTop, RightPokemonBottom };
        if (PokemonNameText.Text != scenePokemon.DisplayName
            || oldActorImages.Any(image => image.Visibility != Visibility.Collapsed || image.Source is not null || image.IsHitTestVisible || image.Focusable))
            throw new InvalidOperationException("Das feste Pokémon muss Teil der Szenenillustration sein; alte Bild- oder Picker-Ebenen sind noch aktiv.");
        if (SeasonalBackdrop.Parent != StageArtArea || PokemonSceneryForeground.Parent != StageArtArea || !StageArtArea.ClipToBounds || PatchNotesText.FontSize < 16 || PatchNotesText.LineHeight < 26)
            throw new InvalidOperationException("Saisonbegrenzung oder Leseschrift fehlt.");
        if (Math.Abs(SeasonalBackdrop.Opacity) > .001 || Math.Abs(PokemonSceneryForeground.Opacity) > .001)
            throw new InvalidOperationException("Eine alte separate Bühnenebene liegt über der integrierten Illustration.");
        var logo = WindowBounds(LogoFrame);
        var operations = WindowBounds(OperationsPanel);
        var start = WindowBounds(StartGameButton);
        var hero = WindowBounds(HeroPanel);
        var viewport = WindowBounds(HeroScroller);
        double centre = ActualWidth / 2;
        if (LogoFrame.ActualWidth < 240 || LogoFrame.ActualWidth > 440 || Math.Abs(logo.Left + logo.Width / 2 - centre) > 2
            || Math.Abs(operations.Left + operations.Width / 2 - centre) > 2
            || Math.Abs(start.Left + start.Width / 2 - centre) > 2
            || Math.Abs(hero.Left + hero.Width / 2 - centre) > 2)
            throw new InvalidOperationException($"Logo, Hauptspalte oder Startknopf sind nicht exakt zentriert. Fenster={ActualWidth:0.##}; Logo={logo}; Operations={operations}; Start={start}; Hero={hero}; StartTransform={StartGameButton.RenderTransform.Value}.");
        if (!viewport.Contains(logo) || !viewport.Contains(WindowBounds(StartGameButton)) || !viewport.Contains(WindowBounds(OperationsPanel)))
            throw new InvalidOperationException("Logo, Startknopf oder Versionsstatus sind abgeschnitten.");
        int propsLayer = StageArtArea.Children.IndexOf(EventPropsView);
        if (StageArtArea.Children.IndexOf(PokemonSceneryForeground) >= propsLayer)
            throw new InvalidOperationException("Interaktive Saisonobjekte müssen vor der transparenten Legacy-Szenenebene liegen.");
        EventPropsView.VerifyLayout();
    }

    internal void PreviewDownload()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        _operationCts = new CancellationTokenSource();
        SetBusy(true, "Spielupdate wird heruntergeladen …");
        ResetProgress();
        ApplyProgress(new PokemonRebornLauncher.Services.UpdateProgress(PokemonRebornLauncher.Services.UpdateStage.Downloading, 42, 42000000, 100000000, 8000000, "Die neue Spielversion wird heruntergeladen. Deine bisherige Version bleibt erhalten."));
        PatchNotesText.Text = string.Join("\n\n", Enumerable.Repeat("Neue Pokémon, deutsche Beschreibungen und überarbeitete Menüs. Die vorherige Spielversion bleibt über den Versionswechsel verfügbar. Spielstände und persönliche Einstellungen werden beibehalten.", 8));
        UpdateLayout();
        ProgressPanel.BringIntoView();
        if (StartGameButton.IsEnabled || !CancelButton.IsEnabled || CancelButton.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Download-Bedienelemente haben einen falschen Zustand.");
    }

    internal void EndDownloadPreview()
    {
        _operationCts?.Dispose();
        _operationCts = null;
        SetBusy(false);
    }

    private void SystemPreferenceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Dispatcher.Invoke(() => ApplyMotion());
    }

    private void ApplyMotion(bool animateLighting = true, bool restartEntrance = true)
    {
        if (!_presentationReady) return;
        bool reduce = _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || WindowState == WindowState.Minimized || !IsActive || _gameRunning || !IsVisible;
        Tag = reduce;
        SeasonalBackdrop.SetTheme(_activeTheme, reduce, _daylight, animateLighting);
        SeasonalBackdrop.Opacity = 0;
        AtmosphereView.Configure(_activeTheme, _daylight, IsActive && WindowState != WindowState.Minimized && !_gameRunning && !_presentationClosed, reduce);
        LeftEdgeScene.Visibility = RightEdgeScene.Visibility = Visibility.Collapsed;
        LeftPokemonOcclusion.SetTheme(_activeTheme, _daylight, animateLighting && !reduce);
        RightPokemonOcclusion.SetTheme(_activeTheme, _daylight, animateLighting && !reduce);
        PokemonSceneryForeground.Opacity = 0;
        if (reduce)
        {
            CompleteLightingMorph();
            PokemonSceneryForeground.SetTheme(_activeTheme, _daylight, false);
        }
        ApplyEventPresentation();
        MouseTrailView.Configure(((SolidColorBrush)FindResource("Violet")).Color, _settings.MouseTrailEnabled, reduce);
        HeroTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        HeroTranslate.Y = 0;
        AccentGlow.BeginAnimation(OpacityProperty, null);
        if (reduce) return;
        AccentGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.24, 0.57, TimeSpan.FromSeconds(6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });

    }

    internal void AllowPreviewMusic()
    {
        if (!_smokeTest) return;
        _previewAudio = true;
        _music = new LauncherMusic();
        _music.StatusChanged += ShowMusicStatus;
        ApplyMusic();
    }

    private void ApplyMusic()
    {
        if (!_presentationReady || _presentationClosed) return;
        if (_smokeTest && !_previewAudio) { MusicStatusText.Text = "Vorschau"; return; }
        _music?.Apply(_settings.MusicEnabled, _settings.MusicVolume, _gameRunning || WindowState == WindowState.Minimized, _activeTheme);
    }

    private async void MusicToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_presentationReady || _suppressSettingsEvents) return;
        _settings.MusicEnabled = MusicCheckBox.IsChecked == true;
        ApplyMusic();
        await SavePresentationAsync();
    }

    private void MusicVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_presentationReady || _suppressSettingsEvents) return;
        _settings.MusicVolume = Math.Clamp(e.NewValue, 0, 1);
        MusicVolumeSlider.ToolTip = $"Lautstärke: {_settings.MusicVolume:P0}";
        ApplyMusic();
        _volumeSaveTimer.Stop();
        _volumeSaveTimer.Start();
    }

    private async void ReduceMotion_Changed(object sender, RoutedEventArgs e)
    {
        if (!_presentationReady || _suppressSettingsEvents) return;
        _settings.ReduceMotion = ReduceMotionCheckBox.IsChecked == true;
        ApplyMotion();
        await SavePresentationAsync();
    }

    private async Task SavePresentationAsync()
    {
        if (!_settingsLoaded || _smokeTest) return;
        try { await _settings.SaveAsync(); }
        catch (Exception ex) { SetStatus($"Die Darstellung wurde geändert, konnte aber nicht gespeichert werden: {ex.Message}"); }
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => Close();

    private MessageBoxResult ShowPrompt(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        // Settings actions can open a prompt while the settings window is modal.
        // Parent it to that active window so the prompt remains in front and modal
        // input is nested correctly.
        var owner = _settingsWindow ?? _extrasWindow ?? _patchNotesWindow ?? this;
        var dialog = new LauncherDialog(owner, message, title, buttons) { Tag = Tag };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void TrackGameMusic(Process? game)
    {
        _gameRunning = true;
        ApplyMusic();
        ApplyMotion(restartEntrance: false);
        if (game is null) return;
        _ = ObserveGameExitAsync(game);
    }

    private async Task ObserveGameExitAsync(Process game)
    {
        try { await game.WaitForExitAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        finally
        {
            game.Dispose();
            _gameRunning = false;
            if (!_presentationClosed) { ApplyMusic(); ApplyMotion(restartEntrance: false); }
        }
    }

    internal void VerifyPresentationControls()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        MusicCheckBox.IsChecked = false;
        MusicVolumeSlider.Value = 0.4;
        ReduceMotionCheckBox.IsChecked = true;
        if (_settings.MusicEnabled || _settings.MusicVolume != 0.4 || Tag is not true || HeroTranslate.HasAnimatedProperties)
            throw new InvalidOperationException($"Musik- oder Animationsschalter reagieren nicht: Musik={_settings.MusicEnabled}, Lautstärke={_settings.MusicVolume}, reduziert={Tag}, HeroAnimation={HeroTranslate.HasAnimatedProperties}.");
        if (new[] { PokemonArtwork, LeftPokemonTop, LeftPokemonBottom, RightPokemonTop, RightPokemonBottom }
            .Any(image => image.Visibility != Visibility.Collapsed || image.Source is not null || image.IsHitTestVisible || image.Focusable))
            throw new InvalidOperationException("Separate Pokémonbilder oder alte Picker-Hitbereiche sind noch aktiv.");
        if (PokemonNameText.Text != ScenePokemonFor(_activeTheme).DisplayName)
            throw new InvalidOperationException("Das Namensschild passt nicht zum festen Pokémon der Szene.");
        ReduceMotionCheckBox.IsChecked = false;
    }

    private bool IsMotionReduced => _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || WindowState == WindowState.Minimized;
}
