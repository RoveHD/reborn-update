using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>A rare, short Halloween Gengar fly-by. This scene never handles input.</summary>
public sealed class GengarScene : FrameworkElement
{
    private const double FrameSeconds = 1.0 / 30.0;
    private const double WaitTickSeconds = 1;
    private const double FlightSeconds = 2.45;
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
    private double _randomYOffset;

    public event Action? LaughRequested;
    public bool IsAnimating => _timer.IsEnabled || _flying;
    public bool IsFlying => _flying;

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

    /// <summary>Enables a rare, non-blocking fly-by only while the Halloween stage is active.</summary>
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
        if (!_laughPlayed && _flightElapsed >= FlightSeconds * .30)
        {
            _laughPlayed = true;
            try { LaughRequested?.Invoke(); }
            catch { /* Optional sound callbacks must never interrupt the launcher UI. */ }
        }
        if (_flightElapsed >= FlightSeconds)
        {
            _flying = false;
            _flightElapsed = 0;
            _secondsUntilNext = NextDelay();
            _timer.Interval = TimeSpan.FromSeconds(WaitTickSeconds);
        }
    }

    /// <summary>Starts one controlled fly-by for UI verification, without waiting for the random idle delay.</summary>
    public void TriggerFlyByForTest()
    {
        if (!_enabled) throw new InvalidOperationException("Gengar muss für den Test aktiviert sein.");
        _timer.Stop();
        BeginFlyBy();
        InvalidateVisual();
    }

    /// <summary>Advances a controlled fly-by without relying on wall-clock timer ticks.</summary>
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
        _randomYOffset = _random.NextDouble() * .1;
    }

    private double NextDelay() => 42 + _random.NextDouble() * 58;

    private void StopAndClear()
    {
        _timer.Stop();
        _flying = false;
        _flightElapsed = 0;
        _laughPlayed = false;
        _secondsUntilNext = _enabled ? NextDelay() : 0;
        _previousTick = _clock.Elapsed;
        _timer.Interval = TimeSpan.FromSeconds(WaitTickSeconds);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (!_flying || ActualWidth <= 0 || ActualHeight <= 0) return;

        double t = Math.Clamp(_flightElapsed / FlightSeconds, 0, 1);
        double aspectRatio = Artwork.Height / Artwork.Width;
        double rotatedExtentPerBaseWidth = 1.1 * Math.Sqrt(1 + aspectRatio * aspectRatio);
        double inset = 8;
        double largestFittingWidth = Math.Max(1, (Math.Min(ActualWidth, ActualHeight) - inset * 2) / rotatedExtentPerBaseWidth);
        double baseWidth = Math.Min(270, Math.Min(ActualHeight * .4, largestFittingWidth));
        // Enter fast, hang just long enough for the laugh and full turn, then dart away.
        double travel = t < .34
            ? EaseOutCubic(t / .34) * .44
            : t < .69
                ? .44 + (t - .34) / .35 * .12
                : .56 + EaseInCubic((t - .69) / .31) * .44;
        double direction = _startsFromLeft ? 1 : -1;
        double x = direction > 0
            ? -baseWidth * .35 + travel * (ActualWidth + baseWidth * .7)
            : ActualWidth + baseWidth * .35 - travel * (ActualWidth + baseWidth * .7);
        double requestedY = ActualHeight * (.18 + _randomYOffset);
        double rotatedHalfDiagonal = baseWidth * rotatedExtentPerBaseWidth / 2;
        double y = Math.Clamp(requestedY, inset + rotatedHalfDiagonal, ActualHeight - inset - rotatedHalfDiagonal);

        // A fresh path is selected per appearance, then held stable for the whole flight.
        double scale = t < .22 ? .55 + EaseOutCubic(t / .22) * .55 : t > .84 ? 1 - (t - .84) / .16 * .35 : 1.1;
        double rotation = Math.Min(360, Math.Max(0, (t - .16) / .53) * 360) * direction;
        double opacity = t < .08 ? t / .08 : t > .93 ? (1 - t) / .07 : 1;
        double width = baseWidth * scale;
        double height = Artwork.Height * (width / Artwork.Width);
        var center = new Point(x, y);

        context.PushOpacity(Math.Clamp(opacity, 0, 1));
        context.PushTransform(new RotateTransform(rotation, center.X, center.Y));
        context.DrawImage(Artwork, new Rect(center.X - width / 2, center.Y - height / 2, width, height));
        context.Pop();
        context.Pop();
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
