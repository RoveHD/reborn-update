using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>A rare Halloween disappearance, approach and return. This scene never handles input.</summary>
public sealed class GengarScene : FrameworkElement
{
    private const double FrameSeconds = 1.0 / 30.0;
    private const double WaitTickSeconds = 1;
    private const double VanishSeconds = .65;
    private const double AbsenceSeconds = .75;
    private const double FlightSeconds = 1.85;
    private const double ReturnSeconds = .60;
    private const double FlightStart = VanishSeconds + AbsenceSeconds;
    private const double FlightEnd = FlightStart + FlightSeconds;
    private const double EncounterSeconds = FlightEnd + ReturnSeconds;
    private const double MaxTickGapSeconds = 0.4;
    private const double MaxWaitGapSeconds = 5;
    private static readonly ImageSource Artwork = LoadArtwork();

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Random _random = new();
    private TimeSpan _previousTick;
    private double _secondsUntilNext;
    private double _flightElapsed;
    private bool _enabled;
    private bool _visibleReady;
    private bool _flying;
    private bool _startsFromLeft;
    private bool _laughPlayed;
    private Point? _sceneOrigin;
    private Point _encounterOrigin;
    private double _backdropAbsence;

    public event Action? LaughRequested;
    /// <summary>0 shows the original scene; 1 covers its Gengar with the empty clean plate.</summary>
    public event Action<double>? BackdropAbsenceChanged;
    public double BackdropAbsence => _backdropAbsence;
    public bool IsEncounterActive => _flying;
    public void SetSceneOrigin(Point? point) => _sceneOrigin = point;
    public bool IsAnimating => _timer.IsEnabled || _flying;
    public bool IsFlying => _flying && _flightElapsed >= FlightStart && _flightElapsed < FlightEnd;

    public GengarScene()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(FrameSeconds)
        };
        _timer.Tick += OnTick;
        Loaded += (_, _) => { _visibleReady = true; UpdateTimer(); };
        Unloaded += (_, _) => { _visibleReady = false; StopAndClear(); };
        IsVisibleChanged += (_, _) => UpdateTimer();
        SizeChanged += (_, _) => UpdateTimer();
    }

    /// <summary>Enables a rare, non-blocking encounter only while the Halloween stage is active.</summary>
    public void Configure(string? theme, bool decorationsEnabled, bool reduceMotion, bool active)
    {
        _enabled = string.Equals(theme, "halloween", StringComparison.OrdinalIgnoreCase)
            && decorationsEnabled && !reduceMotion && active;
        if (!_enabled) StopAndClear();
        else if (!_flying && _secondsUntilNext <= 0) _secondsUntilNext = NextDelay();
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        bool shouldRun = _enabled && _visibleReady && IsVisible && ActualWidth > 0 && ActualHeight > 0;
        if (!shouldRun)
        {
            StopAndClear();
            return;
        }
        _previousTick = _clock.Elapsed;
        _timer.Interval = TimeSpan.FromSeconds(_flying ? FrameSeconds : WaitTickSeconds);
        if (!_timer.IsEnabled) _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_enabled || !_visibleReady || !IsVisible)
        {
            StopAndClear();
            return;
        }

        var now = _clock.Elapsed;
        double elapsed = (now - _previousTick).TotalSeconds;
        _previousTick = now;
        if (!double.IsFinite(elapsed) || elapsed <= 0) return;
        if (!_flying)
        {
            // Idle ticks are sparse. A long suspend resets the random delay instead of catching up.
            if (elapsed > MaxWaitGapSeconds)
            {
                _secondsUntilNext = NextDelay();
                return;
            }
            _secondsUntilNext -= elapsed;
            if (_secondsUntilNext > 0) return;
            BeginFlyBy();
            _timer.Interval = TimeSpan.FromSeconds(FrameSeconds);
            _previousTick = _clock.Elapsed;
            return;
        }

        // A suspended/blocked dispatcher discards the flight instead of showing a catch-up animation.
        if (elapsed > MaxTickGapSeconds)
        {
            StopAndClear();
            UpdateTimer();
            return;
        }

        AdvanceFlight(elapsed);
        InvalidateVisual();
    }

    private void AdvanceFlight(double elapsed)
    {
        _flightElapsed += elapsed;
        double absence = _flightElapsed < VanishSeconds
            ? SmoothStep(_flightElapsed / VanishSeconds)
            : _flightElapsed <= FlightEnd ? 1
            : 1 - SmoothStep((_flightElapsed - FlightEnd) / ReturnSeconds);
        SetBackdropAbsence(absence);
        if (!_laughPlayed && _flightElapsed >= FlightStart + .18)
        {
            _laughPlayed = true;
            try { LaughRequested?.Invoke(); }
            catch { /* Optional sound callbacks must never interrupt the launcher UI. */ }
        }
        if (_flightElapsed >= EncounterSeconds)
        {
            _flying = false;
            _flightElapsed = 0;
            SetBackdropAbsence(0);
            _secondsUntilNext = NextDelay();
            _timer.Interval = TimeSpan.FromSeconds(WaitTickSeconds);
        }
    }

    private void SetBackdropAbsence(double value)
    {
        value = Math.Clamp(value, 0, 1);
        if (Math.Abs(value - _backdropAbsence) < .00001) return;
        _backdropAbsence = value;
        BackdropAbsenceChanged?.Invoke(value);
    }

    /// <summary>Starts one controlled encounter for UI verification, without waiting for the random idle delay.</summary>
    public void TriggerFlyByForTest()
    {
        if (!_enabled) throw new InvalidOperationException("Gengar muss für den Test aktiviert sein.");
        _timer.Stop();
        BeginFlyBy();
        InvalidateVisual();
    }

    /// <summary>Advances a controlled encounter without relying on wall-clock timer ticks.</summary>
    public void AdvanceForTest(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || !_flying) return;
        double left = Math.Min(seconds, 10);
        while (left > 0 && _flying)
        {
            double delta = Math.Min(1d / 60d, left);
            AdvanceFlight(delta);
            left -= delta;
        }
        InvalidateVisual();
    }

    private void BeginFlyBy()
    {
        _flying = true;
        _startsFromLeft = _random.Next(2) == 0;
        _flightElapsed = 0;
        _laughPlayed = false;
        // Freeze the authored origin for this appearance, even if the pointer moves later.
        _encounterOrigin = _sceneOrigin ?? new Point(ActualWidth * .18, ActualHeight * .55);
        SetBackdropAbsence(0);
    }

    private double NextDelay() => 180 + _random.NextDouble() * 180;

    private void StopAndClear()
    {
        _timer.Stop();
        _flying = false;
        _flightElapsed = 0;
        _laughPlayed = false;
        SetBackdropAbsence(0);
        _secondsUntilNext = _enabled ? NextDelay() : 0;
        _previousTick = _clock.Elapsed;
        _timer.Interval = TimeSpan.FromSeconds(WaitTickSeconds);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (!IsFlying || ActualWidth <= 0 || ActualHeight <= 0) return;

        double t = Math.Clamp((_flightElapsed - FlightStart) / FlightSeconds, 0, 1);
        double approach = EaseOutCubic(Math.Min(1, t / .70));
        double exit = EaseInCubic((t - .73) / .27);
        double direction = _startsFromLeft ? 1 : -1;
        double targetX = ActualWidth * .52;
        double targetY = ActualHeight * .46;
        double x = _encounterOrigin.X + (targetX - _encounterOrigin.X) * approach
            + direction * exit * ActualWidth * .85;
        double y = _encounterOrigin.Y + (targetY - _encounterOrigin.Y) * approach
            - Math.Sin(t * Math.PI) * ActualHeight * .035 - exit * ActualHeight * .12;
        double maximumWidth = Math.Min(620, Math.Min(ActualWidth * .64, ActualHeight * .73));
        double width = maximumWidth * (.14 + approach * .86);
        double height = Artwork.Height * (width / Artwork.Width);
        double rotation = direction * (-10 + 17 * approach + exit * 19);
        double opacity = t < .07 ? t / .07 : t > .89 ? (1 - t) / .11 : 1;
        var center = new Point(x, y);

        // Gengar approaches the viewer from its actual wall location, then slips away.
        // Keep the artwork intact: no full spins, flashes or input-blocking layer.
        context.PushOpacity(Math.Clamp(opacity, 0, 1));
        context.PushTransform(new RotateTransform(rotation, center.X, center.Y));
        context.DrawImage(Artwork, new Rect(center.X - width / 2, center.Y - height / 2, width, height));
        context.Pop();
        context.Pop();
    }

    private static double SmoothStep(double value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }
    private static double EaseOutCubic(double value) => 1 - Math.Pow(1 - Math.Clamp(value, 0, 1), 3);
    private static double EaseInCubic(double value) => Math.Pow(Math.Clamp(value, 0, 1), 3);

    private static ImageSource LoadArtwork()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/assets/artwork/gengar.png", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
