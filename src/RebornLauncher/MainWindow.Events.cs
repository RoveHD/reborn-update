using System.Windows;
using System.Windows.Media;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private EventSoundBank? _eventSounds;
    private HalloweenAmbientAudio? _halloweenAmbient;
    private bool _eventsReady;

    private void InitializeEvents()
    {
        _eventsReady = true;
        _eventSounds = new EventSoundBank();
        _halloweenAmbient = new HalloweenAmbientAudio();
        EventPropsView.SoundRequested += PlayEventSound;
        FireworksView.SoundRequested += PlayEventSound;
        GengarView.LaughRequested += () => _halloweenAmbient?.PlayJumpscareLaugh();
        EventPropsView.EggsFoundChanged += count => SetStatus($"Osterentdeckungen · {count} gefunden");
        EventPropsView.SecretDiscovered += discovery =>
        {
            if (!_isBusy) SetStatus($"Entdeckung: {discovery.Name} · {discovery.Hint}");
        };
        Closed += (_, _) => { _eventsReady = false; _eventSounds.Dispose(); _halloweenAmbient.Dispose(); };
        ApplyEventPresentation();
    }

    private void ApplyEventPresentation()
    {
        if (!_eventsReady) return;
        UpdateBloodEasterEggActivity();
        _halloweenAmbient?.Configure(_activeTheme, _settings.HalloweenAmbientEnabled,
            _settings.EventSoundsEnabled && (!_smokeTest || _previewAudio), _settings.EventSoundVolume,
            _presentationClosed || _gameRunning || WindowState == WindowState.Minimized,
            _settings.HalloweenAmbientFrequency);
        bool active = IsActive && WindowState != WindowState.Minimized && !_presentationClosed && !_gameRunning;
        bool reduced = _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || _settings.EventAnimationIntensity == "reduced";
        AtmosphereView.Configure(_activeTheme, _daylight, active && IsVisible, reduced);
        int limit = _settings.EventParticleQuality switch { "off" => 0, "low" => 80, "high" => 320, _ => 160 };
        double intensity = _settings.EventAnimationIntensity switch { "reduced" => .4, "high" => 1.6, _ => 1 };
        var day = _smokeTest ? _themeDay : DateOnly.FromDateTime(DateTime.Now);
        bool newYear = _activeTheme == "newyear";
        int fireworksLimit = newYear ? limit * 3 / 4 : 0;
        EventPropsView.Configure(_activeTheme, day.Month == 12, _settings.EventDecorationsEnabled, _settings.InteractivePropsEnabled, limit - fireworksLimit, intensity, reduced, active);
        FireworksView.Configure(newYear && _settings.EventDecorationsEnabled, fireworksLimit, intensity, reduced, active);
        bool halloween = string.Equals(_activeTheme, "halloween", StringComparison.OrdinalIgnoreCase);
        GengarView.Configure(_activeTheme, _settings.EventDecorationsEnabled, reduced, active);
        _halloweenAmbient?.ConfigureJumpscareLaugh(_settings.EventSoundsEnabled && (!_smokeTest || _previewAudio),
            _settings.EventSoundVolume, !halloween || !_settings.EventDecorationsEnabled || reduced || !active);
        var decorationVisibility = _settings.EventDecorationsEnabled ? Visibility.Visible : Visibility.Collapsed;
        PokemonSceneryForeground.Visibility = Visibility.Visible;
        var particlesVisibility = _settings.EventDecorationsEnabled && limit > 0 ? Visibility.Visible : Visibility.Collapsed;
        SeasonalBackdrop.Visibility = LeftEdgeScene.Visibility = RightEdgeScene.Visibility = particlesVisibility;
        _eventSounds?.Configure(_settings.EventSoundsEnabled && (!_smokeTest || _previewAudio), _settings.EventSoundVolume, !active);
    }

    private void PlayEventSound(string cue)
    {
        string translated = cue switch
        {
            "launch" => "rocket-launch", "boom" => "firework-boom", "collect" => "egg-crack", "rare" => "magic",
            "pop" => _activeTheme == "summer" ? "water-pop" : _activeTheme == "halloween" ? "pumpkin-pop" : "confetti",
            "shatter" => _activeTheme == "winter" ? "ice-break" : "chocolate-snap", "bubble" => "cauldron-bubble",
            "chime" => "sparkle", "snow" => "snow-poof", "tap" => _activeTheme == "halloween" ? "wood-creak" : "leaf-rustle", _ => cue
        };
        _eventSounds?.Play(translated);
    }

    private async void EventSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EventSettingsDialog(this, _settings);
        dialog.ShowDialog();
        if (!dialog.Accepted) return;
        dialog.ApplyTo(_settings);
        ApplyEventPresentation();
        await SavePresentationAsync();
    }

    internal object VerifyEventInteractions()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        var checks = new Dictionary<string, int>();
        foreach (string theme in ThemeCalendar.ThemeIds)
        {
            EventPropsView.Configure(theme, theme == "winter", true, true, 80, 1, false, true);
            if (EventPropsView.Props.Count == 0) throw new InvalidOperationException("Leere Propszene: " + theme);
            foreach (var prop in EventPropsView.Props.ToArray()) EventPropsView.ActivateForTest(prop.Id);
            EventPropsView.AdvanceForTest(2);
            if (EventPropsView.ActiveParticleCount > 80) throw new InvalidOperationException("Partikellimit überschritten.");
            checks[theme] = EventPropsView.Props.Count;
        }
        EventPropsView.Configure("winter", true, true, true, 80, 1, false, true);
        var snowman = EventPropsView.Props.First(prop => prop.Kind == PropKind.Snowman);
        var snowmanBounds = EventPropsView.GetPlacementBounds().First(item => item.Id == snowman.Id).Bounds;
        var center = new Point(snowmanBounds.X + snowmanBounds.Width / 2, snowmanBounds.Y + snowmanBounds.Height / 2);
        if (InputHitTest(EventPropsView.TranslatePoint(center, this)) != EventPropsView)
            throw new InvalidOperationException("Eine Fensterebene blockiert die Prop-Klicks.");
        EventPropsView.Configure("winter", true, false, false, 0, 1, true, false);
        if (EventPropsView.IsHitTestVisible || EventPropsView.IsAnimating || EventPropsView.ActiveParticleCount != 0)
            throw new InvalidOperationException("Deaktivierte Props bleiben aktiv.");
        ApplyEventPresentation();
        return checks;
    }
}
