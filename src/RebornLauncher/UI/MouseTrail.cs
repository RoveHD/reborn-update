using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>A small, non-interactive accent-color spark trail drawn inside its host element.</summary>
public sealed class MouseTrail : FrameworkElement
{
    private const int MaximumParticles = 40;
    private static readonly TimeSpan MaximumLife = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(34);

    private readonly List<Particle> particles = new(MaximumParticles);
    private readonly DispatcherTimer timer;
    private readonly Random random = new();
    private Brush accentBrush = Brushes.MediumPurple;
    private Brush highlightBrush = Brushes.White;
    private Pen trailPen = new(Brushes.MediumPurple, 1.4);
    private Point? lastPosition;
    private bool enabled;
    private bool reduceMotion;

    public MouseTrail()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = FrameInterval
        };
        timer.Tick += OnFrame;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
    }

    /// <summary>Sets the theme color and whether pointer effects are currently allowed.</summary>
    public void Configure(Color accent, bool enabled, bool reduceMotion)
    {
        EnsureDispatcher();
        this.enabled = enabled;
        this.reduceMotion = reduceMotion;
        accentBrush = new SolidColorBrush(Color.FromRgb(accent.R, accent.G, accent.B));
        accentBrush.Freeze();
        trailPen = new Pen(accentBrush, 1.4);
        trailPen.Freeze();
        highlightBrush = Brushes.White;

        if (!enabled || reduceMotion) Clear();
        else InvalidateVisual();
    }

    /// <summary>Adds a few particles along the latest element-local pointer segment.</summary>
    public void Move(Point point)
    {
        EnsureDispatcher();
        if (!CanAnimate || !IsFinite(point)) return;

        if (lastPosition is not { } previous)
        {
            AddParticle(point);
            lastPosition = point;
            UpdateTimer();
            InvalidateVisual();
            return;
        }

        var delta = point - previous;
        var distance = delta.Length;
        if (distance < 1.5)
        {
            lastPosition = point;
            return;
        }

        // Keep a short, even trail on both slow and fast pointer movement.
        var sampleCount = Math.Clamp((int)Math.Ceiling(distance / 7.0), 1, 8);
        var segmentStart = previous;
        if (distance > 56) segmentStart = point - delta / distance * 56;
        var segment = point - segmentStart;
        for (var i = 1; i <= sampleCount; i++)
        {
            var fraction = (double)i / sampleCount;
            AddParticle(segmentStart + segment * fraction);
        }

        lastPosition = point;
        UpdateTimer();
        InvalidateVisual();
    }

    /// <summary>Clears all particles and stops the animation timer.</summary>
    public void Clear()
    {
        EnsureDispatcher();
        timer.Stop();
        particles.Clear();
        lastPosition = null;
        InvalidateVisual();
    }

    /// <summary>Emits a small celebratory burst at an element-local point.</summary>
    public void Burst(Point point)
    {
        EnsureDispatcher();
        if (!CanAnimate || !IsFinite(point)) return;
        const int count = 14;
        for (var i = 0; i < count; i++)
        {
            var angle = (Math.PI * 2 * i / count) + (random.NextDouble() - 0.5) * 0.18;
            var speed = 18 + random.NextDouble() * 28;
            AddParticle(point,
                new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed - 8),
                1.5 + random.NextDouble() * 1.5,
                TimeSpan.FromMilliseconds(290 + random.NextDouble() * 110));
        }
        UpdateTimer();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!CanAnimate || particles.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var particle in particles)
        {
            var elapsed = now - particle.Born;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / particle.Lifetime.TotalMilliseconds, 0d, 1d);
            var opacity = Math.Pow(1d - progress, 1.55);
            var center = particle.Origin + particle.Velocity * elapsed.TotalSeconds;

            drawingContext.PushOpacity(opacity * 0.18);
            drawingContext.DrawEllipse(accentBrush, null, center, particle.Radius * 3.2, particle.Radius * 3.2);
            drawingContext.Pop();

            drawingContext.PushOpacity(opacity * 0.82);
            drawingContext.DrawLine(trailPen, particle.Origin, center);
            drawingContext.DrawEllipse(accentBrush, null, center, particle.Radius, particle.Radius);
            drawingContext.Pop();

            drawingContext.PushOpacity(opacity * 0.62);
            var glint = new Point(center.X - particle.Radius * 0.28, center.Y - particle.Radius * 0.28);
            drawingContext.DrawEllipse(highlightBrush, null, glint, particle.Radius * 0.28, particle.Radius * 0.28);
            drawingContext.Pop();
        }
    }

    internal int ParticleCount => particles.Count;
    internal bool IsAnimating => timer.IsEnabled;
    private bool CanAnimate => enabled && !reduceMotion;

    private void AddParticle(Point point, Vector? velocity = null, double? radius = null, TimeSpan? lifetime = null)
    {
        if (particles.Count == MaximumParticles) particles.RemoveAt(0);
        particles.Add(new Particle(
            point,
            velocity ?? new Vector((random.NextDouble() - 0.5) * 18, -5 - random.NextDouble() * 14),
            radius ?? 1.3 + random.NextDouble() * 1.7,
            lifetime ?? TimeSpan.FromMilliseconds(255 + random.NextDouble() * (MaximumLife.TotalMilliseconds - 255)),
            DateTime.UtcNow));
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!CanAnimate || !IsLoaded || !IsVisible)
        {
            Clear();
            return;
        }

        var now = DateTime.UtcNow;
        particles.RemoveAll(particle => now - particle.Born >= particle.Lifetime);
        if (particles.Count == 0)
        {
            timer.Stop();
            InvalidateVisual();
            return;
        }
        InvalidateVisual();
    }

    private void UpdateTimer()
    {
        if (CanAnimate && IsLoaded && IsVisible && particles.Count > 0)
        {
            if (!timer.IsEnabled) timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateTimer();
    private void OnUnloaded(object sender, RoutedEventArgs e) => Clear();

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible) Clear();
        else UpdateTimer();
    }

    private static bool IsFinite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private void EnsureDispatcher()
    {
        if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("MouseTrail muss auf seinem WPF-Dispatcher verwendet werden.");
    }

    private readonly record struct Particle(Point Origin, Vector Velocity, double Radius, TimeSpan Lifetime, DateTime Born);
}
