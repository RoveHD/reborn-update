using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    private readonly DispatcherTimer _cameoTimer = new() { Interval = TimeSpan.FromMilliseconds(2100) };
    private bool _cameoRevealed;

    private void InitializePresentation()
    {
        if (_presentationReady) return;
        _presentationReady = true;
        _volumeSaveTimer.Tick += async (_, _) => { _volumeSaveTimer.Stop(); await SavePresentationAsync(); };
        _cameoTimer.Tick += (_, _) => { _cameoTimer.Stop(); SetCameoRevealed(false, animate: true); };
        StateChanged += (_, _) => { ApplyMusic(); ApplyMotion(); };
        Closed += (_, _) => { _presentationClosed = true; _volumeSaveTimer.Stop(); _cameoTimer.Stop(); _music?.Dispose(); _updates.Dispose(); };
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged;
        InitializePointer();
        InitializeCalendar();
        InitializeEvents();
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
        foreach (FrameworkElement control in new FrameworkElement[] { StartGameButton, CheckUpdatesButton, RepairButton, ChooseFolderButton, CancelButton, ReleasePageButton, OpenGameFolderButton, LocalVersionText, RemoteVersionText, StatusText, ProgressPanel, DownloadProgressBar, LatestVersionButton, PreviousVersionButton, AutoUpdateCheckBox, CloseAfterStartCheckBox, MusicCheckBox, MusicVolumeSlider, ReduceMotionCheckBox, MouseTrailCheckBox, ThemeInfoButton, LightingModeButton, EventSettingsButton })
        {
            if (!control.IsVisible) continue;
            var bounds = control.TransformToAncestor(this).TransformBounds(new Rect(control.RenderSize));
            if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > ActualWidth + 1 || bounds.Bottom > ActualHeight + 1)
                throw new InvalidOperationException($"Bedienelement außerhalb des Fensters: {control.Name}; Bounds={bounds}; Fenster={ActualWidth:0.##}×{ActualHeight:0.##}; Transform={control.RenderTransform.Value}");
        }
    }

    internal void VerifySceneryLayout()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        UpdateLayout();
        if (HeroScroller.ScrollableHeight > 1 || HeroScroller.ScrollableWidth > 1 || HeroScroller.VerticalOffset > 1)
            throw new InvalidOperationException($"Die Hauptansicht benötigt Scrollen: {HeroScroller.ScrollableHeight:0.0}px.");
        Rect WindowBounds(FrameworkElement element) => element.TransformToAncestor(this).TransformBounds(new Rect(element.RenderSize));
        var stage = WindowBounds(StageArtArea);
        if (StageArtArea.ActualWidth < 500 || StageArtArea.ActualHeight < 190 || StageArtArea.ActualHeight > 321)
            throw new InvalidOperationException($"Die Saisonbühne ist zu klein: {StageArtArea.ActualWidth:0}×{StageArtArea.ActualHeight:0}.");
        if (ContentLayout.ActualWidth > 1441 || StartGameButton.ActualWidth > 281 || new[] { CheckUpdatesButton, RepairButton, ChooseFolderButton, CancelButton, ReleasePageButton, OpenGameFolderButton }.Any(button => button.IsVisible && button.ActualWidth > 201))
            throw new InvalidOperationException("Bühne oder Bedienung wird übermäßig in die Breite gezogen.");
        foreach (FrameworkElement control in new FrameworkElement[] { HeroPanel, ScenePanel, StatusText, LocalVersionText, RemoteVersionText, StartGameButton, StageCaptionText })
            if (stage.IntersectsWith(WindowBounds(control)))
                throw new InvalidOperationException($"Saisonbühne überschneidet Text oder Bedienung: {control.Name}");
        var actors = new[] { PokemonArtwork, LeftPokemonBottom, RightPokemonTop };
        foreach (var image in actors)
        {
            var bounds = image.TransformToAncestor(StageArtArea).TransformBounds(new Rect(image.RenderSize));
            if (image.Source is null || image.Visibility != Visibility.Visible || bounds.Width < 40 || bounds.Height < 40 || bounds.Left < 2 || bounds.Top < 2 || bounds.Right > StageArtArea.ActualWidth - 2 || bounds.Bottom > StageArtArea.ActualHeight - 2)
                throw new InvalidOperationException($"Ein Pokémon ist abgeschnitten oder fehlt: {image.Name} ({bounds}).");
        }
        for (int i = 0; i < actors.Length; i++)
            for (int j = i + 1; j < actors.Length; j++)
                if (WindowBounds(actors[i]).IntersectsWith(WindowBounds(actors[j])))
                    throw new InvalidOperationException("Die Pokémon verdecken sich gegenseitig.");
        if (SeasonalBackdrop.Parent != StageArtArea || PokemonSceneryForeground.Parent != StageArtArea || !StageArtArea.ClipToBounds || PatchNotesText.FontSize < 16 || PatchNotesText.LineHeight < 26)
            throw new InvalidOperationException("Saisonbegrenzung oder Leseschrift fehlt.");
        var logo = WindowBounds(LogoFrame);
        var viewport = WindowBounds(HeroScroller);
        if (!viewport.Contains(logo) || logo.IntersectsWith(WindowBounds(SeasonSubtitleText)))
            throw new InvalidOperationException("Das Reborn-Logo ist abgeschnitten oder überschneidet den Untertitel.");
        if (LeftPokemonTop.Visibility != Visibility.Collapsed || RightPokemonBottom.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Die Szene zeigt zu viele Pokémon.");
        int propsLayer = StageArtArea.Children.IndexOf(EventPropsView);
        if (StageArtArea.Children.IndexOf(PokemonSceneryForeground) >= propsLayer
            || actors.Any(actor => StageArtArea.Children.IndexOf(actor) <= propsLayer))
            throw new InvalidOperationException("Deko verdeckt anklickbare Objekte oder Effekte verdecken Pokémon.");
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
        bool reduce = _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || WindowState == WindowState.Minimized || !IsActive;
        Tag = reduce;
        SeasonalBackdrop.SetTheme(_activeTheme, reduce, _daylight, animateLighting);
        LeftEdgeScene.Visibility = RightEdgeScene.Visibility = Visibility.Collapsed;
        LeftPokemonOcclusion.SetTheme(_activeTheme, _daylight, animateLighting && !reduce);
        RightPokemonOcclusion.SetTheme(_activeTheme, _daylight, animateLighting && !reduce);
        if (reduce)
        {
            CompleteLightingMorph();
            PokemonSceneryForeground.SetTheme(_activeTheme, _daylight, false);
        }
        ApplyEventPresentation();
        MouseTrailView.Configure(((SolidColorBrush)FindResource("Violet")).Color, _settings.MouseTrailEnabled, reduce);
        HeroTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        ArtworkFloat.BeginAnimation(TranslateTransform.YProperty, null);

        AccentGlow.BeginAnimation(OpacityProperty, null);

        foreach (var transform in new[] { LeftTopFloat, LeftBottomFloat, RightTopFloat, RightBottomFloat })
            transform.BeginAnimation(TranslateTransform.YProperty, null);
        ApplyCameoPosition(reduce);
        if (reduce) _cameoTimer.Stop();
        if (reduce) return;
        int phase = 0;
        foreach (var transform in new[] { LeftTopFloat, LeftBottomFloat, RightTopFloat, RightBottomFloat })
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-1.2, 1.2, TimeSpan.FromSeconds(3.5 + phase++ * .6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        if (restartEntrance)
            HeroTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        ArtworkFloat.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-1.5, 1.5, TimeSpan.FromSeconds(4.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });

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

    private void ShowArtwork(bool animate)
    {
        _cameoTimer.Stop();
        _cameoRevealed = false;
        var art = ThemeCast[_activeTheme];
        int main = _activeTheme == "standard" && _daylight ? 1 : 0;
        MorphArtwork(PokemonArtwork, ArtworkSource(art[main]), animate);
        PokemonArtwork.Opacity = 1;
        PokemonNameText.Text = ThemeLooks[_activeTheme].Names[main];
        var remaining = Enumerable.Range(0, art.Length).Where(i => i != main).ToArray();
        var images = new[] { LeftPokemonTop, LeftPokemonBottom, RightPokemonTop, RightPokemonBottom };
        for (int i = 0; i < images.Length; i++)
        {
            MorphArtwork(images[i], ArtworkSource(art[remaining[i]]), animate);
            images[i].Opacity = 1;
        }
        UpdateResponsiveLayout();
    }

    private static BitmapImage ArtworkSource(string id) => new(new Uri($"pack://application:,,,/assets/artwork/{id}.png"));

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
        var dialog = new LauncherDialog(this, message, title, buttons) { Tag = Tag };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void TrackGameMusic(Process? game)
    {
        _gameRunning = true;
        ApplyMusic();
        ApplyEventPresentation();
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
            if (!_presentationClosed) { ApplyMusic(); ApplyEventPresentation(); }
        }
    }

    internal void VerifyPresentationControls()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        MusicCheckBox.IsChecked = false;
        MusicVolumeSlider.Value = 0.4;
        ReduceMotionCheckBox.IsChecked = true;
        if (_settings.MusicEnabled || _settings.MusicVolume != 0.4 || Tag is not true || ArtworkFloat.HasAnimatedProperties)
            throw new InvalidOperationException("Musik- oder Animationsschalter reagieren nicht.");
        if (new[] { PokemonArtwork, LeftPokemonBottom, RightPokemonTop }.Any(image => image.Source is null || image.Opacity != 1))
            throw new InvalidOperationException("Ein sichtbares Pokémon fehlt oder ist durchsichtig.");
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
        RightPokemonTop.RaiseEvent(click);
        if (!_cameoRevealed || _cameoTimer.IsEnabled || RightPokemonTop.HasAnimatedProperties || Math.Abs(((TranslateTransform)RightPokemonTop.RenderTransform).X) > .1)
            throw new InvalidOperationException("Der Cameo ist bei reduzierter Bewegung nicht statisch erreichbar.");
        RightPokemonTop.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        if (new[] { ArtworkFloat, LeftBottomFloat, RightTopFloat }.Any(transform => transform.HasAnimatedProperties))
            throw new InvalidOperationException("Reduzierte Bewegung lässt eine Szenenanimation aktiv.");
        ReduceMotionCheckBox.IsChecked = false;
    }

    private void Cameo_Click(object sender, MouseButtonEventArgs e)
    {
        SetCameoRevealed(!_cameoRevealed, animate: !IsMotionReduced);
        if (_cameoRevealed && !IsMotionReduced) _cameoTimer.Start();
        else _cameoTimer.Stop();
        e.Handled = true;
    }

    private void Cameo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        SetCameoRevealed(!_cameoRevealed, animate: !IsMotionReduced);
        if (_cameoRevealed && !IsMotionReduced) _cameoTimer.Start();
        else _cameoTimer.Stop();
        e.Handled = true;
    }

    private bool IsMotionReduced => _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || WindowState == WindowState.Minimized;

    private void SetCameoRevealed(bool revealed, bool animate)
    {
        _cameoRevealed = revealed;
        ApplyCameoPosition(!animate || IsMotionReduced);
    }

    private void ApplyCameoPosition(bool reduceMotion)
    {
        var transform = (TranslateTransform)RightPokemonTop.RenderTransform;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        double target = 0; // Curious companions are always completely inside the scene.
        RightPokemonTop.ToolTip = _cameoRevealed ? "Dein Begleiter grüßt zurück." : "Ein neugieriger Begleiter – anklicken";
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.Y = reduceMotion ? 0 : (_cameoRevealed ? -3 : 0);
        if (reduceMotion) transform.X = target;
        else transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(230)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
