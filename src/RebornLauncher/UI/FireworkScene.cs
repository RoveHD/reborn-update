using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>Transparent, non-interactive ambient fireworks overlay in viewport DIPs.</summary>
public sealed class FireworkScene : FrameworkElement
{
    private readonly FireworkSimulation _simulation;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<uint, Brush> _brushes = [];
    private readonly Dictionary<uint, Brush> _coreBrushes = [];

    public event Action<string>? SoundRequested;
    public int ActiveParticleCount => _simulation.ActiveParticleCount;
    public bool IsAnimating => _timer.IsEnabled;
    public int BurstCount => _simulation.BurstCount;
    public FireworkBurstKind? LastBurstKind => _simulation.LastBurstKind;

    public FireworkScene() : this(new FireworkSimulation()) { }

    public FireworkScene(FireworkSimulation simulation)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = false;
        _simulation.SoundRequested += cue => SoundRequested?.Invoke(cue);
        _timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(30) };
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
        SizeChanged += (_, _) => ResizeSimulation();
    }

    public void Configure(bool enabled, int particleLimit, double intensity, bool reduceMotion, bool active)
    {
        _simulation.Configure(enabled, Math.Clamp(particleLimit, 0, 320), intensity, reduceMotion, active);
        SyncTimer();
        InvalidateVisual();
    }

    /// <summary>Advances a deterministic simulation for renderer smoke tests without waiting on the UI clock.</summary>
    public void AdvanceForTest(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || !_simulation.IsRunning) return;
        ResizeSimulation();
        double left = Math.Min(seconds, 120);
        while (left > 0)
        {
            double dt = Math.Min(1d / 60d, left);
            _simulation.Step(dt);
            left -= dt;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        foreach (var particle in _simulation.Particles)
        {
            double fade = particle.Opacity;
            if (fade <= 0) continue;
            Brush brush = BrushFor(particle.Color);
            dc.PushOpacity(fade * (particle.IsTrail ? .38 : .34));
            double dx = particle.X - particle.PreviousX;
            double dy = particle.Y - particle.PreviousY;
            if (dx * dx + dy * dy > .35)
            {
                var pen = new Pen(brush, particle.IsTrail ? particle.Size * .9 : particle.Size * .64);
                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
                pen.Freeze();
                dc.DrawLine(pen, new Point(particle.PreviousX, particle.PreviousY), new Point(particle.X, particle.Y));
            }
            dc.Pop();
            // A faint halo gives bloom without turning each spark into a large solid dot.
            if (!particle.IsTrail)
            {
                dc.PushOpacity(fade * .18);
                dc.DrawEllipse(brush, null, new Point(particle.X, particle.Y), particle.Size * 2.25, particle.Size * 2.25);
                dc.Pop();
            }
            if (particle.LeavesTrail && particle.TrailPoints.Count > 1)
            {
                int segments = particle.TrailPoints.Count - 1;
                for (int i = 0; i < segments; i++)
                {
                    var from = particle.TrailPoints[i];
                    var to = particle.TrailPoints[i + 1];
                    double trailFade = fade * (.18 + .45 * (i + 1d) / segments);
                    dc.PushOpacity(trailFade);
                    var trailPen = new Pen(brush, Math.Max(.65, particle.Size * .55)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                    trailPen.Freeze();
                    dc.DrawLine(trailPen, new Point(from.X, from.Y), new Point(to.X, to.Y));
                    dc.Pop();
                }
            }
            // Small warm-white cores stay distinct from the softer color halo.
            dc.PushOpacity(fade * (particle.IsTrail ? .48 : .88));
            dc.DrawEllipse(CoreBrushFor(particle.Color), null, new Point(particle.X, particle.Y), particle.Size * .8, particle.Size * .8);
            dc.Pop();
        }

        foreach (var rocket in _simulation.Rockets)
        {
            Brush brush = BrushFor(rocket.Color);
            dc.PushOpacity(.75);
            var pen = new Pen(brush, 2.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            dc.DrawLine(pen, new Point(rocket.X - rocket.Vx * .065, rocket.Y - rocket.Vy * .065), new Point(rocket.X, rocket.Y));
            dc.DrawEllipse(Brushes.Wheat, null, new Point(rocket.X, rocket.Y), 2.1, 2.1);
            dc.Pop();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResizeSimulation();
        SyncTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _clock.Reset();
        _simulation.Clear();
        InvalidateVisual();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            _timer.Stop();
            _clock.Reset();
            _simulation.Clear();
            InvalidateVisual();
            return;
        }
        ResizeSimulation();
        SyncTimer();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        _simulation.Step(elapsed);
        InvalidateVisual();
    }

    private void SyncTimer()
    {
        if (!IsLoaded || !IsVisible || !_simulation.IsRunning)
        {
            _timer.Stop();
            _clock.Reset();
            if (!_simulation.IsRunning) _simulation.Clear();
            return;
        }
        if (!_timer.IsEnabled)
        {
            _clock.Restart();
            _timer.Start();
        }
    }

    private void ResizeSimulation() => _simulation.Resize(ActualWidth, ActualHeight);

    private Brush BrushFor(uint argb)
    {
        if (_brushes.TryGetValue(argb, out var brush)) return brush;
        var color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        var created = new SolidColorBrush(color);
        created.Freeze();
        _brushes[argb] = created;
        return created;
    }

    private Brush CoreBrushFor(uint argb)
    {
        if (_coreBrushes.TryGetValue(argb, out var brush)) return brush;
        var red = (byte)(argb >> 16);
        var green = (byte)(argb >> 8);
        var blue = (byte)argb;
        static byte Lift(byte channel) => (byte)Math.Min(255, channel + (255 - channel) * .32);
        var created = new SolidColorBrush(Color.FromRgb(Lift(red), Lift(green), Lift(blue)));
        created.Freeze();
        _coreBrushes[argb] = created;
        return created;
    }
}
