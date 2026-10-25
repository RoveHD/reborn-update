using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private static readonly TimeSpan LightingDuration = TimeSpan.FromSeconds(2.8);
    private readonly Dictionary<string, string> _lightingTargets = new();
    private readonly Dictionary<Image, ImageSource> _artworkTargets = new();

    private static ColorAnimation LightingColor(Color from, Color to) => new(from, to, LightingDuration)
    {
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        FillBehavior = FillBehavior.Stop
    };

    // Keep resource brushes alive: controls with both static and dynamic references
    // follow the same animation. Reversing a transition starts at its visible color.
    private void MorphResource(string key, string hex, bool animate)
    {
        var target = (Color)ColorConverter.ConvertFromString(hex);
        _lightingTargets[key] = hex;
        var brush = (SolidColorBrush)Resources[key];
        bool replace = brush.IsFrozen;
        if (replace) brush = brush.CloneCurrentValue();
        Color current = brush.Color;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        brush.Color = target;
        // A clock keeps the shared resource mutable even after styles resolve it.
        var transition = LightingColor(animate ? current : target, target);
        transition.FillBehavior = FillBehavior.HoldEnd;
        if (!animate) transition.Duration = TimeSpan.Zero;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, transition);
        if (replace) Resources[key] = brush;
    }

    private void MorphGlow(Color accent, bool animate)
    {
        if (AccentGlow.Background is not RadialGradientBrush glow || glow.IsFrozen)
        {
            glow = new RadialGradientBrush(Colors.Transparent, Colors.Transparent);
            AccentGlow.Background = glow;
        }
        for (int i = 0; i < 2; i++)
        {
            var stop = glow.GradientStops[i];
            var target = Color.FromArgb(i == 0 ? (byte)65 : (byte)0, accent.R, accent.G, accent.B);
            var current = stop.Color;
            stop.BeginAnimation(GradientStop.ColorProperty, null);
            stop.Color = target;
            if (animate) stop.BeginAnimation(GradientStop.ColorProperty, LightingColor(current, target));
        }
    }

    private void MorphArtwork(Image image, ImageSource target, bool animate)
    {
        _artworkTargets[image] = target;
        if (image.Source?.ToString() == target.ToString()) return;
        if (!animate || image.Source is null || !image.IsVisible)
        {
            image.Source = target;
            return;
        }
        // A snapshot of the current drawing also preserves an interrupted blend.
        var previous = image.Source.CloneCurrentValue();
        previous.Freeze();
        static DrawingGroup Layer(ImageSource source)
        {
            double scale = 1000 / Math.Max(source.Width, source.Height);
            var bounds = new Rect((1000 - source.Width * scale) / 2, (1000 - source.Height * scale) / 2,
                source.Width * scale, source.Height * scale);
            var layer = new DrawingGroup();
            layer.Children.Add(new ImageDrawing(source, bounds));
            return layer;
        }
        var outgoing = Layer(previous);
        var incoming = Layer(target);
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 1000, 1000))));
        drawing.Children.Add(outgoing);
        drawing.Children.Add(incoming);
        var blended = new DrawingImage(drawing);
        image.Source = blended;
        var fadeIn = new DoubleAnimation(0, 1, LightingDuration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        fadeIn.Completed += (_, _) => { if (ReferenceEquals(image.Source, blended)) image.Source = target; };
        outgoing.BeginAnimation(DrawingGroup.OpacityProperty,
            new DoubleAnimation(1, 0, LightingDuration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        incoming.BeginAnimation(DrawingGroup.OpacityProperty, fadeIn);
    }

    private void CompleteLightingMorph()
    {
        foreach (var (key, color) in _lightingTargets.ToArray()) MorphResource(key, color, false);
        if (AccentGlow.Background is RadialGradientBrush glow)
            foreach (var stop in glow.GradientStops) stop.BeginAnimation(GradientStop.ColorProperty, null);
        foreach (var (image, target) in _artworkTargets) image.Source = target;
    }
}
