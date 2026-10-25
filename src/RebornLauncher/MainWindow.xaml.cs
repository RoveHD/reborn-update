using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher;

public partial class MainWindow : Window
{
    private readonly UpdateService _updates = new();
    private LauncherSettings _settings = new();
    private CancellationTokenSource? _operationCts;
    private bool _isBusy;
    private bool _settingsLoaded;
    private bool _smokeTest;
    private bool _previewMissingGame;

    private bool HasSelectedGame() => !_previewMissingGame && (_smokeTest || File.Exists(Path.Combine(_settings.ResolveInstallDirectory(), _settings.GameExecutable)));
    private bool _allowClose;
    private bool _closeWhenIdle;
    private bool _suppressSettingsEvents;
    private bool _launcherUpdateFailed;
    private string _localVersion = "0.0.0";
    private string _remoteVersion = "";
    private VersionSelection? _versionSelection;

    public MainWindow(bool smokeTest = false)
    {
        InitializeComponent();
        PokemonRebornLauncher.UI.WindowWorkArea.Attach(this);
        _smokeTest = smokeTest;
        Opacity = 0;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_smokeTest)
        {
            _settings = new LauncherSettings { InstallDirectory = AppContext.BaseDirectory, Fullscreen = false, MusicEnabled = false };
            _localVersion = "7.0.0";
            _remoteVersion = "7.0.0";
            _versionSelection = new VersionSelection("7.0.0", "6.1.0", false);
            ApplySettingsToUi();
            LoadVisualAssets();
            InitializePresentation();
            BeginFadeIn();
            LauncherStatusText.Text = $"Launcher {SelfUpdateService.CurrentVersion} · Vorschau";
            PatchNotesText.Text = "Neues Abenteuer, neue Updates.\n\n• Verbesserte Stabilität und Fehlerkorrekturen.\n• Versionshinweise lassen sich hier bequem lesen.";
            PatchNotesSubtitle.Text = "Beispielansicht · offline";
            UpdateVersionLabels();
            SetStatus("Vorschau der Launcher-Oberfläche.");
            return;
        }

        try
        {
            _settings = await LauncherSettings.LoadAsync();
            _settingsLoaded = true;
            ApplySettingsToUi();
            LoadVisualAssets();
            InitializePresentation();
            BeginFadeIn();
            var launcherReady = await CheckLauncherUpdateAsync(continueOnFailure: true);
            if (_closeWhenIdle || _allowClose) return;
            await RefreshLauncherInfoAsync();
            if (_closeWhenIdle) return;

            if (HasSelectedGame() && launcherReady && !_launcherUpdateFailed && (_settings.AutoUpdate || SelfUpdateService.ContinueAfterSelfUpdate))
            {
                await EnsureUpdatedAsync(automatic: true, force: false);
            }
        }
        catch (Exception ex)
        {
            ShowFailure("Launcher konnte nicht gestartet werden.", ex);
        }
    }

    private static string ReadablePatchNotes(string text)
    {
        // The channel accepts plain text and simple Markdown; don't display its markup as prose.
        text = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"(?m)^#{1,6}\s+", "");
        return text.Replace("**", "").Replace("`", "");
    }

    private void BeginFadeIn()
    {
        if (_settings.ReduceMotion || !SystemParameters.ClientAreaAnimation) { Opacity = 1; return; }
        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(520),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    internal void PreviewPreviousVersion()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        _localVersion = "6.1.0";
        _versionSelection = new VersionSelection("6.1.0", "7.0.0", true);
        UpdateVersionLabels();
        SetStatus("Version 6.1.0 ausgewählt. Automatische Spielupdates pausieren.");
        if (PreviousVersionButton.IsEnabled || !LatestVersionButton.IsEnabled || CheckUpdatesButton.IsEnabled || RepairButton.IsEnabled || !StartGameButton.IsEnabled)
            throw new InvalidOperationException("Falsche Bedienelemente bei ausgewählter vorheriger Version.");
    }

    private async Task RefreshLauncherInfoAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;
        SetBusy(true, "Spielstatus wird geprüft …");
        try
        {
            if (HasSelectedGame())
            {
                _localVersion = await Task.Run(() => _updates.ReadLocalVersionAsync(_settings, token), token);
                _versionSelection = _updates.GetVersionSelection(_settings);
            }
            else { _localVersion = "0.0.0"; _versionSelection = null; }
            UpdateVersionLabels();
            var manifest = await _updates.GetManifestAsync(_settings, token);
            _remoteVersion = manifest.Game.Version;
            UpdateVersionLabels();
            PatchNotesText.Text = string.IsNullOrWhiteSpace(manifest.PatchNotes)
                ? "Für diese Version gibt es keine Versionshinweise."
                : ReadablePatchNotes(manifest.PatchNotes);
            PatchNotesSubtitle.Text = $"Änderungen für Version {_remoteVersion}";
            SetStatus("Spielstatus geladen.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Prüfung abgebrochen.");
            PatchNotesText.Text = "Laden der Versionshinweise abgebrochen.";
        }
        catch (Exception ex)
        {
            _remoteVersion = "";
            UpdateVersionLabels();
            PatchNotesText.Text = $"Versionshinweise sind gerade nicht verfügbar.\n\n{ex.Message}";
            PatchNotesSubtitle.Text = "Offline-Modus";
            SetStatus($"Spielstatus konnte nicht geladen werden: {ex.Message}");
        }
        finally
        {
            _operationCts?.Dispose();
            _operationCts = null;
            SetBusy(false);
            UpdateIdleStatus();
            CloseWhenIdle();
        }
    }

    private async Task<bool> EnsureUpdatedAsync(bool automatic, bool force)
    {
        if (_isBusy)
        {
            return false;
        }

        if (!HasSelectedGame())
        {
            SetStatus("Wähle zuerst deinen vorhandenen Reborn-Spielordner aus.");
            return false;
        }
        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;

        SetBusy(true, force ? "Spiel wird repariert …" : "Suche nach Spielupdates …");
        ResetProgress();

        try
        {
            var progress = new Progress<UpdateProgress>(ApplyProgress);

            _localVersion = await _updates.ReadLocalVersionAsync(_settings, token);
            _versionSelection = _updates.GetVersionSelection(_settings);
            UpdateVersionLabels();

            if (_versionSelection.IsPreviousActive)
            {
                SetStatus("Vorherige Version ausgewählt. Zum Aktualisieren oder Reparieren zuerst auf „Aktuell“ wechseln.");
                return true;
            }

            _remoteVersion = await _updates.GetRemoteVersionAsync(_settings, token);
            UpdateVersionLabels();

            var needsUpdate = force || UpdateService.IsDifferentVersion(_localVersion, _remoteVersion);
            if (!needsUpdate)
            {
                SetStatus("Das Spiel ist auf dem neuesten Stand.");
                UpdateIdleStatus();
                return true;
            }

            if (!automatic && !force)
            {
                var answer = ShowPrompt(
                    $"Version {_remoteVersion} ist verfügbar. Jetzt herunterladen und installieren?",
                    "Spielupdate verfügbar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                {
                    SetStatus("Update übersprungen.");
                    return true;
                }
            }

            await _updates.DownloadAndInstallAsync(_settings, _remoteVersion, progress, token);
            _localVersion = _remoteVersion;
            _versionSelection = _updates.GetVersionSelection(_settings);
            UpdateVersionLabels();
            SetStatus($"Version {_localVersion} wurde installiert.");
            return true;
        }
        catch (OperationCanceledException)
        {
            SetStatus("Update abgebrochen.");
            ProgressText.Text = "Abgebrochen";
            SpeedText.Text = "";
            return false;
        }
        catch (Exception ex)
        {
            ShowFailure("Spielupdate fehlgeschlagen.", ex);
            return false;
        }
        finally
        {
            _operationCts?.Dispose();
            _operationCts = null;
            SetBusy(false);
            UpdateIdleStatus();
            CloseWhenIdle();
        }
    }

    private void ApplyProgress(UpdateProgress progress)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        TopStatusText.Text = StageName(progress.Stage);
        ActivityText.Text = progress.Message;
        StatusText.Text = progress.Message;

        if (progress.Percent is { } percent)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = Math.Clamp(percent, 0, 100);
            ProgressText.Text = $"{percent:0.0}%";
        }
        else
        {
            DownloadProgressBar.IsIndeterminate = true;
            ProgressText.Text = StageName(progress.Stage);
        }

        if (progress.Stage == UpdateStage.Downloading)
        {
            var downloaded = FormatBytes(progress.BytesReceived);
            var total = progress.TotalBytes is > 0 ? FormatBytes(progress.TotalBytes.Value) : "?";
            var speed = progress.BytesPerSecond > 0 ? $"{FormatBytes(progress.BytesPerSecond)}/s" : "";
            ProgressText.Text = $"{downloaded} / {total}";
            SpeedText.Text = speed;
        }
        else
        {
            SpeedText.Text = "";
        }
    }

    private void ResetProgress()
    {
        ProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = true;
        DownloadProgressBar.Value = 0;
        ProgressText.Text = "Wird vorbereitet …";
        Dispatcher.BeginInvoke(new Action(() => ProgressPanel.BringIntoView()), System.Windows.Threading.DispatcherPriority.Loaded);
        SpeedText.Text = "";
    }

    private void UpdateVersionLabels()
    {
        UpdateVersionSelectionUi();
        LocalVersionText.Text = string.IsNullOrWhiteSpace(_localVersion) ? "Nicht installiert" : _localVersion;
        RemoteVersionText.Text = string.IsNullOrWhiteSpace(_remoteVersion) ? "Offline" : _remoteVersion;
        if (!HasSelectedGame())
        {
            LocalVersionText.Text = "Nicht ausgewählt";
            SetBadge("Spielordner auswählen", "#17131D", "#463B4E", "#E8DCC7");
            return;
        }

        var hasRemote = !string.IsNullOrWhiteSpace(_remoteVersion);
        var needsUpdate = hasRemote && UpdateService.IsDifferentVersion(_localVersion, _remoteVersion);

        if (_versionSelection?.IsPreviousActive == true)
        {
            SetBadge($"Vorherige Version {_localVersion} aktiv · Auto-Update pausiert", "#2A2230", "#5D4D6D", "#E7D8FF");
        }
        else if (!hasRemote)
        {
            SetBadge("Offline · Spiel kann lokal starten", "#2A2230", "#5D4D6D", "#E7D8FF");
        }
        else if (needsUpdate)
        {
            SetBadge($"Spielupdate verfügbar: {_remoteVersion}", "#302016", "#8A5B28", "#FFE0B0");
        }
        else
        {
            SetBadge("Spiel ist aktuell", "#12301F", "#2E7D4F", "#B8FFD3");
        }
    }

    private void SetBadge(string text, string background, string border, string foreground)
    {
        UpdateBadgeText.Text = text;
        UpdateBadgeBorder.Background = Brushes.Transparent;
        UpdateBadgeBorder.BorderBrush = BrushFromHex(border);
        UpdateBadgeText.Foreground = (Brush)FindResource("Mint");
    }

    private void UpdateVersionSelectionUi()
    {
        bool previous = _versionSelection?.IsPreviousActive == true;
        bool hasPair = !string.IsNullOrWhiteSpace(_versionSelection?.OtherVersion);
        string latest = previous ? _versionSelection!.OtherVersion! : _localVersion;
        string? older = previous ? _localVersion : _versionSelection?.OtherVersion;
        LatestVersionButton.Content = $"{(!previous ? "✓ " : "")}Aktuell · {latest}";
        PreviousVersionButton.Content = older is null ? "Vorherige · –" : $"{(previous ? "✓ " : "")}Vorherige · {older}";
        LatestVersionButton.IsEnabled = !_isBusy && hasPair && previous;
        PreviousVersionButton.IsEnabled = !_isBusy && hasPair && !previous;
        LatestVersionButton.BorderBrush = (Brush)FindResource(!previous ? "Violet" : "Line");
        PreviousVersionButton.BorderBrush = (Brush)FindResource(previous ? "Violet" : "Line");
        LatestVersionButton.Opacity = _isBusy ? 0.45 : 1;
        PreviousVersionButton.Opacity = _isBusy || !hasPair ? 0.45 : 1;
        VersionSelectionHint.Text = !hasPair
            ? "Eine zweite Version ist nach dem nächsten Spielupdate verfügbar."
            : previous
                ? "Auto-Update pausiert. Neue Spielstände sind eventuell nicht mit der älteren Version kompatibel."
                : "Beide Versionen bleiben verfügbar. Zum Wechseln muss Reborn geschlossen sein.";
        CheckUpdatesButton.IsEnabled = !_isBusy && !previous;
        RepairButton.IsEnabled = !_isBusy && !previous;
        StartGameButton.Content = "REBORN STARTEN";
        if (!HasSelectedGame())
        {
            StartGameButton.Content = "SPIELORDNER WÄHLEN";
            LatestVersionButton.Content = "Aktuell · –";
            PreviousVersionButton.Content = "Vorherige · –";
            LocalVersionText.Text = "Nicht ausgewählt";
            LatestVersionButton.IsEnabled = PreviousVersionButton.IsEnabled = RepairButton.IsEnabled = false;
            VersionSelectionHint.Text = "Wähle deinen bestehenden Reborn-Ordner. Der Launcher bleibt separat installiert.";
            SetBadge("Spielordner auswählen", "#17131D", "#463B4E", "#E8DCC7");
        }
    }

    private async void VersionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        if (_isBusy || _versionSelection?.OtherVersion is null) return;
        bool wantsPrevious = ReferenceEquals(sender, PreviousVersionButton);
        if (wantsPrevious == _versionSelection.IsPreviousActive) return;
        _operationCts = new CancellationTokenSource();
        SetBusy(true, "Spielversion wird gewechselt …");
        ResetProgress();
        try
        {
            await _updates.SwitchVersionAsync(_settings, new Progress<UpdateProgress>(ApplyProgress), _operationCts.Token);
            _localVersion = await _updates.ReadLocalVersionAsync(_settings, CancellationToken.None);
            _versionSelection = _updates.GetVersionSelection(_settings);
            SetStatus($"Version {_localVersion} ist ausgewählt und kann gestartet werden.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Versionswechsel abgebrochen. Die bisherige Version bleibt ausgewählt.");
        }
        catch (Exception ex)
        {
            ShowFailure("Spielversion konnte nicht gewechselt werden.", ex);
        }
        finally
        {
            try
            {
                _localVersion = await _updates.ReadLocalVersionAsync(_settings, CancellationToken.None);
                _versionSelection = _updates.GetVersionSelection(_settings);
            }
            catch (Exception ex)
            {
                _versionSelection = null;
                SetStatus($"Versionsstatus konnte nicht wiederhergestellt werden: {ex.Message}");
            }
            _operationCts?.Dispose();
            _operationCts = null;
            SetBusy(false);
            UpdateVersionLabels();
            CloseWhenIdle();
        }
    }

    private void UpdateIdleStatus()
    {
        if (_isBusy)
        {
            return;
        }

        if (StatusText.Text.Contains("fehlgeschlagen", StringComparison.OrdinalIgnoreCase) ||
            StatusText.Text.Contains("nicht möglich", StringComparison.OrdinalIgnoreCase) ||
            StatusText.Text.Contains("konnte nicht", StringComparison.OrdinalIgnoreCase) ||
            StatusText.Text.Contains("nicht verfügbar", StringComparison.OrdinalIgnoreCase) ||
            StatusText.Text.Contains("abgebrochen", StringComparison.OrdinalIgnoreCase)) return;
        TopStatusText.Text = "Bereit";
        ActivityText.Text = "Bereit";
    }

    private void SetBusy(bool busy, string? activity = null)
    {
        _isBusy = busy;
        StartGameButton.IsEnabled = !busy;
        CheckUpdatesButton.IsEnabled = !busy;
        ChooseFolderButton.IsEnabled = !busy;
        RepairButton.IsEnabled = !busy;
        RefreshPatchNotesButton.IsEnabled = !busy;
        AutoUpdateCheckBox.IsEnabled = !busy;
        CloseAfterStartCheckBox.IsEnabled = !busy;
        UpdateVersionSelectionUi();
        var canCancel = busy && _operationCts is not null;
        CancelButton.Visibility = canCancel ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = canCancel;

        if (!string.IsNullOrWhiteSpace(activity))
        {
            TopStatusText.Text = activity;
            ActivityText.Text = activity;
            StatusText.Text = activity;
        }
    }

    public void SetStartupNotice(string notice)
    {
        SetStatus(notice);
        TopStatusText.Text = "Launcher-Update nicht möglich";
    }

    private async Task<bool> CheckLauncherUpdateAsync(bool continueOnFailure = false)
    {
        var ownsOperation = _operationCts is null;
        _operationCts ??= new CancellationTokenSource();
        LauncherStatusText.Text = $"Launcher {SelfUpdateService.CurrentVersion} · prüfe …";
        SetBusy(true, "Launcher wird auf Updates geprüft …");
        try
        {
            var started = await new SelfUpdateService().CheckAndApplyAsync(
                _settings, new Progress<UpdateProgress>(ApplyProgress), _operationCts.Token);
            if (!started)
            {
                _launcherUpdateFailed = false;
                LauncherStatusText.Text = $"Launcher {SelfUpdateService.CurrentVersion} · aktuell";
                return true;
            }
            _allowClose = true;
            Application.Current.Shutdown();
            return false;
        }
        catch (OperationCanceledException)
        {
            _launcherUpdateFailed = true;
            LauncherStatusText.Text = "Launcher-Update abgebrochen";
            SetStatus("Launcher-Update abgebrochen.");
            return continueOnFailure;
        }
        catch (Exception ex)
        {
            _launcherUpdateFailed = true;
            LauncherStatusText.Text = "Launcher-Update fehlgeschlagen";
            SetStatus($"Launcher-Update fehlgeschlagen: {ex.Message}");
            return continueOnFailure;
        }
        finally
        {
            if (ownsOperation)
            {
                _operationCts?.Dispose();
                _operationCts = null;
            }
            SetBusy(false);
            CloseWhenIdle();
        }
    }
    private void SetStatus(string status)
    {
        StatusText.Text = status;
        ActivityText.Text = status;
    }

    private void ApplySettingsToUi()
    {
        _suppressSettingsEvents = true;
        try
        {
            AutoUpdateCheckBox.IsChecked = _settings.AutoUpdate;
            CloseAfterStartCheckBox.IsChecked = _settings.CloseLauncherAfterGameStart;
            MusicCheckBox.IsChecked = _settings.MusicEnabled;
            MusicVolumeSlider.Value = double.IsFinite(_settings.MusicVolume) ? Math.Clamp(_settings.MusicVolume, 0, 1) : 0.22;
            ReduceMotionCheckBox.IsChecked = _settings.ReduceMotion;
            MouseTrailCheckBox.IsChecked = _settings.MouseTrailEnabled;
            ApplyWindowMode();
        }
        finally { _suppressSettingsEvents = false; }
    }

    private void ApplyWindowMode()
    {
        WindowState = _settings.Fullscreen ? WindowState.Maximized : WindowState.Normal;
        FullscreenButton.Content = _settings.Fullscreen ? "❐" : "□";
        FullscreenButton.ToolTip = _settings.Fullscreen ? "Fenster wiederherstellen (F11)" : "Maximieren (F11)";
    }

    private void LoadVisualAssets()
    {
        var logoPath = _settings.ResolvePath(_settings.LogoImage);
        if (File.Exists(logoPath) && TrySetImageSource(LogoArtwork, logoPath))
        {
            LogoArtwork.Visibility = Visibility.Visible;
            LogoFallback.Visibility = Visibility.Collapsed;
        }
        else if (TrySetEmbeddedImageSource(LogoArtwork, "logo.png"))
        {
            LogoArtwork.Visibility = Visibility.Visible;
            LogoFallback.Visibility = Visibility.Collapsed;
        }
    }

    private static bool TrySetImageSource(System.Windows.Controls.Image image, string path)
    {
        try
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(path, UriKind.Absolute);
            source.EndInit();
            image.Source = source;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetEmbeddedImageSource(System.Windows.Controls.Image image, string resourcePath)
    {
        try
        {
            var resourceName = $"PokemonRebornLauncher.{resourcePath}";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return false;
            }

            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = stream;
            source.EndInit();
            image.Source = source;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void StartGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        if (_isBusy) return;
        if (!HasSelectedGame()) { ChooseFolderButton_Click(sender, e); return; }
        var ready = true;
        if (_versionSelection?.IsPreviousActive != true && _settings.AutoUpdate && !string.IsNullOrWhiteSpace(_remoteVersion))
        {
            ready = await CheckLauncherUpdateAsync();
        }
        if (ready && _versionSelection?.IsPreviousActive != true && _settings.AutoUpdate && !string.IsNullOrWhiteSpace(_remoteVersion))
        {
            ready = await EnsureUpdatedAsync(automatic: true, force: false);
        }

        if (ready)
        {
            await LaunchGameAsync();
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        if (!await CheckLauncherUpdateAsync()) return;
        await EnsureUpdatedAsync(automatic: false, force: false);
    }

    private async void RepairButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        var answer = ShowPrompt(
            "Dabei werden Spieldateien erneut geladen und vorhandene Dateien ersetzt. Fortfahren?",
            "Spiel reparieren",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            if (!await CheckLauncherUpdateAsync()) return;
            await EnsureUpdatedAsync(automatic: true, force: true);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _operationCts?.Cancel();
        CancelButton.IsEnabled = false;
        SetStatus("Abbruch läuft …");
    }

    private async void RefreshPatchNotesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        await RefreshLauncherInfoAsync();
    }

    private async void AutoUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_settingsLoaded || _suppressSettingsEvents) return;
        var oldValue = _settings.AutoUpdate;
        _settings.AutoUpdate = AutoUpdateCheckBox.IsChecked == true;
        try { await _settings.SaveAsync(); }
        catch (Exception ex)
        {
            _settings.AutoUpdate = oldValue;
            _suppressSettingsEvents = true;
            AutoUpdateCheckBox.IsChecked = oldValue;
            _suppressSettingsEvents = false;
            ShowFailure("Einstellung konnte nicht gespeichert werden.", ex);
        }
    }

    private async void CloseAfterStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_settingsLoaded || _suppressSettingsEvents) return;
        var oldValue = _settings.CloseLauncherAfterGameStart;
        _settings.CloseLauncherAfterGameStart = CloseAfterStartCheckBox.IsChecked == true;
        try { await _settings.SaveAsync(); }
        catch (Exception ex)
        {
            _settings.CloseLauncherAfterGameStart = oldValue;
            _suppressSettingsEvents = true;
            CloseAfterStartCheckBox.IsChecked = oldValue;
            _suppressSettingsEvents = false;
            ShowFailure("Einstellung konnte nicht gespeichert werden.", ex);
        }
    }

    private async Task LaunchGameAsync()
    {
        _operationCts = new CancellationTokenSource();
        SetBusy(true, "Spielinstallation wird geprüft …");
        try
        {
            await Task.Run(() => UpdateService.EnsureSafeToLaunch(_settings));
            if (_operationCts.IsCancellationRequested || _closeWhenIdle) return;
        }
        catch (Exception ex)
        {
            ShowFailure("Das Spiel kann gerade nicht sicher gestartet werden.", ex);
            return;
        }
        finally
        {
            _operationCts?.Dispose();
            _operationCts = null;
            SetBusy(false);
            CloseWhenIdle();
        }

        var installDirectory = _settings.ResolveInstallDirectory();
        var gamePath = Path.Combine(installDirectory, _settings.GameExecutable);

        if (!File.Exists(gamePath))
        {
            SetStatus($"{_settings.GameExecutable} wurde nicht gefunden.");
            ShowPrompt(
                $"Nicht gefunden:\n{gamePath}\n\nInstalliere oder repariere zuerst die Spieldateien.",
                "Spiel nicht gefunden",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var game = Process.Start(new ProcessStartInfo
            {
                FileName = gamePath,
                WorkingDirectory = installDirectory,
                UseShellExecute = true
            });

            TrackGameMusic(game);
            SetStatus("Spiel gestartet.");

            if (_settings.CloseLauncherAfterGameStart)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            ShowFailure("Spiel konnte nicht gestartet werden.", ex);
        }
    }

    private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        TryOpen(_settings.ReleasePageUrl);
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        TryOpen(_settings.ResolveInstallDirectory());
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private async void ToggleFullscreen()
    {
        var oldValue = _settings.Fullscreen;
        _settings.Fullscreen = WindowState != WindowState.Maximized;
        ApplyWindowMode();

        if (_settingsLoaded)
        {
            try { await _settings.SaveAsync(); }
            catch (Exception ex)
            {
                _settings.Fullscreen = oldValue;
                ApplyWindowMode();
                ShowFailure("Einstellung konnte nicht gespeichert werden.", ex);
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFullscreen();
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
        }
    }

    private static void TryOpen(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening a folder or browser is a convenience action; the launcher stays usable if it fails.
        }
    }

    private void ShowFailure(string title, Exception ex)
    {
        SetStatus($"{title} {ex.Message}");
        ShowPrompt(
            $"{title}\n\n{ex.Message}",
            "Pokemon Reborn Launcher",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    private static string StageName(UpdateStage stage) => stage switch
    {
        UpdateStage.Checking => "Suche", UpdateStage.Downloading => "Download", UpdateStage.Extracting => "Entpacken", UpdateStage.Installing => "Installation", UpdateStage.Complete => "Fertig", _ => "Bereit"
    };

    private async void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_smokeTest) { SetStatus("Designvorschau · Spiel und Updates bleiben unberührt."); return; }
        var dialog = new OpenFolderDialog { Title = "Pokémon-Reborn-Spielordner auswählen", InitialDirectory = _settings.ResolveInstallDirectory(), Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        var requiredFiles = new[] { _settings.GameExecutable, "Game.ini", "mkxp.json" };
        var missingFile = requiredFiles.FirstOrDefault(file => !File.Exists(Path.Combine(dialog.FolderName, file)));
        if (!Directory.Exists(dialog.FolderName) || missingFile is not null)
        {
            ShowFailure("Das ist kein vollständiger Reborn-Spielordner.", new FileNotFoundException($"Die Datei {missingFile ?? _settings.GameExecutable} fehlt in: {dialog.FolderName}"));
            return;
        }
        var oldDirectory = _settings.InstallDirectory;
        _settings.InstallDirectory = dialog.FolderName;
        try { await _settings.SaveAsync(); }
        catch (Exception ex)
        {
            _settings.InstallDirectory = oldDirectory;
            ShowFailure("Spielordner konnte nicht gespeichert werden.", ex);
            return;
        }
        SetStatus($"Spielordner gespeichert: {dialog.FolderName}");
        _versionSelection = null;
        _localVersion = "0.0.0";
        _remoteVersion = "";
        await RefreshLauncherInfoAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            _closeWhenIdle = false;
            return;
        }
        if (_isBusy && _operationCts is not null && !_allowClose)
        {
            var answer = ShowPrompt("Ein Vorgang läuft. Abbrechen und Launcher schließen?", "Vorgang läuft", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) { e.Cancel = true; return; }
            _closeWhenIdle = true;
            _operationCts.Cancel();
            e.Cancel = true;
            return;
        }
    }

    private void CloseWhenIdle()
    {
        if (!_closeWhenIdle || _isBusy) return;
        _allowClose = true;
        Dispatcher.BeginInvoke(new Action(Close));
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{value:0.##} {units[unitIndex]}");
    }
}
