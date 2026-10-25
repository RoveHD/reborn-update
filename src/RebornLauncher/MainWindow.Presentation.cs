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
        InitializePointer();
        InitializeCalendar();
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
        foreach (FrameworkElement control in new FrameworkElement[] { StartGameButton, LatestVersionButton, PreviousVersionButton, AutoUpdateCheckBox, CloseAfterStartCheckBox, MusicCheckBox, MusicVolumeSlider, ReduceMotionCheckBox, MouseTrailCheckBox, ThemeInfoButton })
        {
            var bounds = control.TransformToAncestor(this).TransformBounds(new Rect(control.RenderSize));
            if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > ActualWidth + 1 || bounds.Bottom > ActualHeight + 1)
                throw new InvalidOperationException($"Bedienelement außerhalb des Fensters: {control.Name}");
        }
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
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Dispatcher.Invoke(ApplyMotion);
    }

    private void ApplyMotion()
    {
        if (!_presentationReady) return;
        bool reduce = _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || WindowState == WindowState.Minimized;
        Tag = reduce;
        SeasonalBackdrop.SetTheme(_activeTheme, reduce);
        MouseTrailView.Configure(((SolidColorBrush)FindResource("Violet")).Color, _settings.MouseTrailEnabled, reduce);
        HeroTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        SceneTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        ArtworkFloat.BeginAnimation(TranslateTransform.YProperty, null);
        AccentGlow.BeginAnimation(OpacityProperty, null);
        RingRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        if (reduce) return;
        HeroTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        var drift = new DoubleAnimation(-16, 16, TimeSpan.FromSeconds(14)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        SceneTranslate.BeginAnimation(TranslateTransform.XProperty, drift);
        ArtworkFloat.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-5, 5, TimeSpan.FromSeconds(3.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        AccentGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.24, 0.57, TimeSpan.FromSeconds(6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        RingRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(90)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    private void ApplyMusic()
    {
        if (!_presentationReady || _presentationClosed) return;
        if (_smokeTest) { MusicStatusText.Text = "Vorschau"; return; }
        _music?.Apply(_settings.MusicEnabled, _settings.MusicVolume, _gameRunning || WindowState == WindowState.Minimized, _activeTheme);
    }

    private void ShowArtwork(bool animate)
    {
        var theme = ThemeLooks[_activeTheme];
        int index = _activeTheme == "standard" ? (_settings.Artwork == "absol" ? 1 : 0) : (_alternateSeasonArtwork && theme.Artwork.Length > 1 ? 1 : 0);
        string artwork = theme.Artwork[index];
        PokemonArtwork.Source = new BitmapImage(new Uri($"pack://application:,,,/assets/artwork/{artwork}.png"));
        PokemonNameText.Text = theme.Names[index];
        ChangeArtworkButton.Visibility = theme.Artwork.Length > 1 ? Visibility.Visible : Visibility.Hidden;
        if (animate && Tag is false)
            PokemonArtwork.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)));
        else PokemonArtwork.BeginAnimation(OpacityProperty, null);
    }

    private async void ChangeArtwork_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTheme == "standard") _settings.Artwork = _settings.Artwork == "absol" ? "umbreon" : "absol";
        else _alternateSeasonArtwork = !_alternateSeasonArtwork;
        ShowArtwork(true);
        await SavePresentationAsync();
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
        var dialog = new LauncherDialog(this, message, title, buttons) { Tag = Tag };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void TrackGameMusic(Process? game)
    {
        _gameRunning = true;
        ApplyMusic();
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
            if (!_presentationClosed) ApplyMusic();
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
        ChangeArtwork_Click(this, new RoutedEventArgs());
        if (PokemonNameText.Text != "ABSOL / 0359") throw new InvalidOperationException("Motivwechsel fehlgeschlagen.");
        ReduceMotionCheckBox.IsChecked = false;
    }
}
