using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>Independent landscape, atmosphere and transparent foreground planes.
/// Transforms are composited by WPF; the camera timer runs only while settling.</summary>
public sealed class AtmosphericBackdrop : Grid
{
    private static readonly Dictionary<string, BitmapSource> Images = new(StringComparer.Ordinal);
    private readonly Grid _farHost = new() { ClipToBounds = true, IsHitTestVisible = false };
    private readonly Canvas _farCanvas = new() { IsHitTestVisible = false };
    private readonly Canvas _foregroundCanvas = new() { IsHitTestVisible = false };
    private readonly Image _landscape = new() { Stretch = Stretch.UniformToFill };
    private readonly Image _gengarEmpty = new() { Stretch = Stretch.Fill, Opacity = 0, IsHitTestVisible = false };
    private readonly Image _foreground = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _light = new() { IsHitTestVisible = false };
    private readonly Border _shade = new() { IsHitTestVisible = false };
    private readonly AtmospherePlane _air = new();
    private readonly TranslateTransform _farTransform = new();
    private readonly TranslateTransform _airTransform = new();
    private readonly TranslateTransform _nearTransform = new();
    private readonly DispatcherTimer _camera = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000d / 60) };
    private readonly Stopwatch _clock = new();
    private Point _target, _position;
    private string? _theme;
    private bool _active, _reduce, _daylight, _configured;
    public event Action<Point>? CameraChanged;
    // This is the visible scene width, not the oversized bitmap used to cover
    // the viewport during parallax. Props must stay inside the actual stage.
    public double IllustratedWidth => ActualWidth;
    public event Action<string, bool, bool, bool>? ConfigurationChanged;
    private Grid? _foregroundHost;
    private Brush? _defaultForegroundMask;

    public Point? LandscapePointIn(Visual destination, Point normalizedPoint)
    {
        if (!IsLoaded || !_landscape.IsLoaded || _landscape.ActualWidth <= 0 || _landscape.ActualHeight <= 0) return null;
        return _landscape.TransformToVisual(destination).Transform(new Point(
            normalizedPoint.X * _landscape.ActualWidth, normalizedPoint.Y * _landscape.ActualHeight));
    }

    /// <summary>Project an authored foreground pixel through cover/crop and near-camera motion.
    /// TransformToVisual also removes the destination stage's independent camera transform.</summary>
    public Point? ForegroundPointIn(Visual destination, Point normalizedPoint)
    {
        if (!IsLoaded || !_foreground.IsLoaded || _foreground.ActualWidth <= 0 || _foreground.ActualHeight <= 0) return null;
        return _foreground.TransformToVisual(destination).Transform(new Point(
            normalizedPoint.X * _foreground.ActualWidth, normalizedPoint.Y * _foreground.ActualHeight));
    }

    public void AttachForeground(Grid host)
    {
        Children.Remove(_foregroundCanvas);
        _foregroundHost = host;
        host.Children.Add(_foregroundCanvas);
        host.SizeChanged += (_, _) => FitLandscape();
        FitLandscape();
    }

    public AtmosphericBackdrop()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(9, 10, 23));
        // Oversized pictures live in Canvas so WPF cannot apply the parent Grid's
        // automatic layout clip before the RenderTransform is applied.
        _farCanvas.Children.Add(_landscape);
        _farCanvas.Children.Add(_gengarEmpty);
        _foregroundCanvas.Children.Add(_foreground);
        _farHost.Children.Add(_farCanvas);
        _landscape.RenderTransform = _farTransform;
        _gengarEmpty.RenderTransform = _farTransform;
        _gengarEmpty.OpacityMask = CreateGengarMask();
        _air.RenderTransform = _airTransform;
        _foreground.RenderTransform = _nearTransform;
        // The illustration already occludes the Pokémon with local terrain.
        // Keep the moving near frame strongest overhead and at the very bottom.
        _foreground.OpacityMask = new LinearGradientBrush(new GradientStopCollection
        {
            new(Colors.White, 0), new(Colors.White, .32), new(Colors.Transparent, .50),
            new(Colors.Transparent, .88), new(Colors.White, 1)
        }, new Point(0, 0), new Point(0, 1));
        _defaultForegroundMask = _foreground.OpacityMask;
        _light.Background = new LinearGradientBrush(Color.FromArgb(44, 255, 221, 163), Color.FromArgb(0, 240, 227, 200), 90);
        _shade.Background = new LinearGradientBrush(new GradientStopCollection
        {
            new(Color.FromArgb(80, 8, 9, 20), 0), new(Color.FromArgb(12, 8, 9, 20), .34),
            new(Color.FromArgb(12, 8, 9, 20), .72), new(Color.FromArgb(130, 8, 9, 20), 1)
        }, new Point(0, 0), new Point(0, 1));
        Children.Add(_farHost);
        Children.Add(_light);
        Children.Add(_air);
        Children.Add(_foregroundCanvas);
        Children.Add(_shade);
        // A feathered scrim protects the fixed interface on bright terrain too.
        var readability = new Border { IsHitTestVisible = false, Width = 800, HorizontalAlignment = HorizontalAlignment.Center };
        readability.Background = new RadialGradientBrush(new GradientStopCollection
        {
            new(Color.FromArgb(165, 8, 10, 24), 0), new(Color.FromArgb(140, 8, 10, 24), .52),
            new(Colors.Transparent, 1)
        }) { RadiusX = .5, RadiusY = .65, Center = new Point(.5, .53), GradientOrigin = new Point(.5, .53) };
        Children.Add(readability);
        _camera.Tick += (_, _) => StepCamera();
        Unloaded += (_, _) => StopCamera();
        IsVisibleChanged += (_, _) => { if (!IsVisible) StopCamera(); };
        SizeChanged += (_, _) => FitLandscape();
    }

    public void Configure(string theme, bool daylight, bool active, bool reduceMotion)
    {
        if (!ThemeCalendar.ThemeIds.Contains(theme)) throw new ArgumentException("Unbekannte Szene", nameof(theme));
        bool changedTheme = _theme != theme;
        if (changedTheme)
        {
            SetGengarAbsence(0);
            _landscape.Source = Load(theme + "-landscape");
            _gengarEmpty.Source = theme == "halloween" ? Load("halloween-gengar-empty") : null;
            _foreground.Source = Load(theme + "-foreground");
            // Halloween's courtyard already supplies solid ground. Keep its upper
            // tree (and attached spider), without a second translucent rocky floor.
            _foreground.OpacityMask = theme == "halloween"
                ? new LinearGradientBrush(new GradientStopCollection
                {
                    new(Colors.White, 0), new(Colors.White, .32), new(Colors.Transparent, .50),
                    new(Colors.Transparent, 1)
                }, new Point(0, 0), new Point(0, 1)) : _defaultForegroundMask;
            _theme = theme;
            FitLandscape();
            _air.SetTheme(theme);
            StopCamera();
        }
        _active = active;
        _reduce = reduceMotion;
        if (!active || reduceMotion) SetGengarAbsence(0);
        double targetLight = daylight ? 1 : 0;
        if (!_configured || changedTheme || daylight != _daylight || reduceMotion || !active)
        {
            double from = _light.Opacity;
            _light.BeginAnimation(OpacityProperty, null);
            _light.Opacity = targetLight;
            if (_configured && !changedTheme && active && !reduceMotion && daylight != _daylight)
                _light.BeginAnimation(OpacityProperty, new DoubleAnimation(from, targetLight, TimeSpan.FromSeconds(2.8))
                { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
        }
        _configured = true;
        _daylight = daylight;
        if (!active || reduceMotion) StopCamera();
        ConfigurationChanged?.Invoke(theme, daylight, active, reduceMotion);
    }

    public void SetPointer(Point point)
    {
        if (!_active || _reduce || !IsVisible || ActualWidth <= 0 || ActualHeight <= 0) return;
        _target = new Point(Math.Clamp(point.X / ActualWidth * 2 - 1, -1, 1), Math.Clamp(point.Y / ActualHeight * 2 - 1, -1, 1));
        StartCamera();
    }

    public void ResetPointer()
    {
        _target = new Point();
        if (_active && !_reduce && IsVisible) StartCamera();
        else StopCamera();
    }

    private void StartCamera()
    {
        if (_camera.IsEnabled || (_target - _position).Length < .0005) return;
        _clock.Restart();
        _camera.Start();
    }

    private void FitLandscape()
    {
        if (_foregroundHost is not null)
            _foregroundHost.Clip = new RectangleGeometry(new Rect(0, 64, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight - 98)));
        FitCover(_landscape, _farCanvas, ActualWidth, ActualHeight, 4, 2.5);
        // Exactly the same dimensions, cover offset and camera transform as the original.
        _gengarEmpty.Width = _landscape.Width;
        _gengarEmpty.Height = _landscape.Height;
        Canvas.SetLeft(_gengarEmpty, Canvas.GetLeft(_landscape));
        Canvas.SetTop(_gengarEmpty, Canvas.GetTop(_landscape));
        double foregroundWidth = _foregroundHost?.ActualWidth ?? ActualWidth;
        double foregroundHeight = _foregroundHost?.ActualHeight ?? ActualHeight;
        FitCover(_foreground, _foregroundCanvas, foregroundWidth, foregroundHeight, 17, 10);
    }

    private static void FitCover(Image image, Canvas canvas, double viewportWidth, double viewportHeight, double travelX, double travelY)
    {
        if (image.Source is not BitmapSource source || viewportWidth <= 0 || viewportHeight <= 0) return;

        // Center the artwork and scale until it covers the viewport plus the
        // full camera travel on both sides. This is a true cover calculation;
        // no blur strip or edge fade is needed to hide exposed bitmap bounds.
        double requiredWidth = viewportWidth + 2 * travelX;
        double requiredHeight = viewportHeight + 2 * travelY;
        double scale = Math.Max(requiredWidth / source.Width, requiredHeight / source.Height);
        image.Width = source.Width * scale;
        image.Height = source.Height * scale;
        Canvas.SetLeft(image, (viewportWidth - image.Width) / 2);
        // Keep the illustrated ground at the lower edge and reserve the full
        // vertical camera travel below the viewport.
        Canvas.SetTop(image, viewportHeight - image.Height + travelY);
    }

    private void StepCamera()
    {
        double dt = Math.Min(_clock.Elapsed.TotalSeconds, .08);
        _clock.Restart();
        double amount = 1 - Math.Exp(-dt * 8);
        _position += (_target - _position) * amount;
        if ((_target - _position).Length < .001)
        {
            _position = _target;
            _camera.Stop();
            _clock.Stop();
        }
        ApplyCamera();
    }

    private void StopCamera()
    {
        _camera.Stop();
        _clock.Reset();
        _position = _target = new Point();
        ApplyCamera();
    }

    private void ApplyCamera()
    {
        _farTransform.X = -_position.X * 4;
        _farTransform.Y = -_position.Y * 2.5;
        _airTransform.X = -_position.X * 9;
        _airTransform.Y = -_position.Y * 5;
        _nearTransform.X = -_position.X * 17;
        _nearTransform.Y = -_position.Y * 10;
        CameraChanged?.Invoke(new Point(_airTransform.X, _airTransform.Y));
    }

    /// <summary>Blend only the authored Gengar region; the rest of the landscape stays untouched.</summary>
    public void SetGengarAbsence(double amount)
    {
        _gengarEmpty.Opacity = _theme == "halloween" && _active && !_reduce
            && double.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
    }

    private static Brush CreateGengarMask()
    {
        // Source pixels in the 1672 x 941 illustration. The opaque center covers
        // Gengar's entire silhouette; 16-pixel feathering hides the inpaint boundary.
        var center = new Rect(274, 393, 178, 196);
        const double feather = 16;
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 1672, 941));
            dc.DrawRectangle(Brushes.White, null, center);
            dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.White, 0), null,
                new Rect(center.Left - feather, center.Top, feather, center.Height));
            dc.DrawRectangle(new LinearGradientBrush(Colors.White, Colors.Transparent, 0), null,
                new Rect(center.Right, center.Top, feather, center.Height));
            dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.White, 90), null,
                new Rect(center.Left, center.Top - feather, center.Width, feather));
            dc.DrawRectangle(new LinearGradientBrush(Colors.White, Colors.Transparent, 90), null,
                new Rect(center.Left, center.Bottom, center.Width, feather));
            // Corner opacity is the same smooth radial falloff as the feathered edges.
            foreach (var corner in new[] { center.TopLeft, center.TopRight, center.BottomLeft, center.BottomRight })
            {
                var gradient = new RadialGradientBrush(Colors.White, Colors.Transparent)
                {
                    MappingMode = BrushMappingMode.Absolute, Center = corner, GradientOrigin = corner,
                    RadiusX = feather, RadiusY = feather
                };
                double x = corner.X == center.Left ? corner.X - feather : corner.X;
                double y = corner.Y == center.Top ? corner.Y - feather : corner.Y;
                dc.DrawRectangle(gradient, null, new Rect(x, y, feather, feather));
            }
        }
        drawing.Freeze();
        var mask = new DrawingBrush(drawing) { Stretch = Stretch.Fill };
        mask.Freeze();
        return mask;
    }

    private static BitmapSource Load(string name)
    {
        if (Images.TryGetValue(name, out var cached)) return cached;
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        string folder = name.EndsWith("-landscape", StringComparison.Ordinal) || name == "halloween-gengar-empty"
            ? "inhabited" : "anime";
        source.UriSource = new Uri($"pack://application:,,,/launcher;component/assets/scenery/{folder}/{name}.png", UriKind.Absolute);
        source.EndInit();
        source.Freeze();
        Images.Add(name, source);
        return source;
    }

    private sealed class AtmospherePlane : FrameworkElement
    {
        private Color _color = Colors.Lavender;
        private string _theme = "standard";
        public void SetTheme(string theme)
        {
            _theme = theme;
            _color = theme switch
            {
                "winter" => Color.FromRgb(130, 218, 255),
                "spring" => Color.FromRgb(230, 185, 227),
                "summer" => Color.FromRgb(251, 179, 135),
                "harvest" or "newyear" => Color.FromRgb(255, 201, 133),
                "arceus" => Color.FromRgb(239, 220, 167),
                _ => Color.FromRgb(181, 164, 236)
            };
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var glow = new RadialGradientBrush(Color.FromArgb(20, _color.R, _color.G, _color.B), Colors.Transparent);
            dc.DrawEllipse(glow, null, new Point(ActualWidth * .14, ActualHeight * .64), ActualWidth * .22, ActualHeight * .25);
            dc.DrawEllipse(glow, null, new Point(ActualWidth * .85, ActualHeight * .7), ActualWidth * .19, ActualHeight * .28);
            // Stable, independent suspended motes make the middle depth readable without an idle timer.
            var mote = new SolidColorBrush(Color.FromArgb(_theme == "winter" ? (byte)100 : (byte)60, _color.R, _color.G, _color.B));
            for (int i = 0; i < 24; i++)
            {
                double x = ((i * 37 + 13) % 101) / 100d;
                if (x > .32 && x < .68) continue;
                double y = ((i * 23 + 7) % 83) / 100d;
                double radius = i % 3 == 0 ? 1.7 : .9;
                dc.DrawEllipse(mote, null, new Point(ActualWidth * x, ActualHeight * y), radius, radius);
            }
        }
    }
}
