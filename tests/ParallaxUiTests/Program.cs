using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokemonRebornLauncher;
using PokemonRebornLauncher.UI;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/parallax");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        var rows = new List<object>();
        try
        {
            window.Show(); Pump(); window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            // Freeze independent button-hover feedback while testing camera-only
            // displacement; a physical pointer can otherwise change hover mid-run.
            ((CheckBox)window.FindName("ReduceMotionCheckBox")).IsChecked = true; Pump();
            var root = (FrameworkElement)window.Content;
            var backdrop = (AtmosphericBackdrop)window.FindName("AtmosphereView");
            var nearHost = (Grid)window.FindName("NearSceneLayer");
            var shell = (Grid)window.FindName("MainShell");
            var layers = new[] { "_forest", "_castle", "_gengar", "_landscape", "_foreground" }
                .Select(name => Field<Image>(backdrop, name)).ToArray();
            var controls = new[] { "HomeNavButton", "LogoFrame", "StartGameButton", "FullscreenButton" }
                .Select(name => (FrameworkElement)window.FindName(name)).ToArray();
            var rootGrid = (Grid)root;
            Check(rootGrid.Children.IndexOf(nearHost) < rootGrid.Children.IndexOf(shell), "Near frame covers the fixed interface");
            Check(((Grid)shell.Children[0]).Background is SolidColorBrush { Color.A: 255 }, "Title bar still lets moving artwork shine through");
            var planes = Field<Canvas>(backdrop, "_farCanvas");
            Check(planes.Children.Cast<UIElement>().SequenceEqual(layers.Take(4)), "Forest/castle/character/stones occlusion order is wrong");

            foreach (var (width, height) in new[] { (960, 640), (1280, 800), (1920, 1080), (1920, 640), (960, 900) })
            {
                window.Width = width; window.Height = height; Pump();
                Invoke(window, "PreviewTheme", "halloween", false); Pump();
                backdrop.Configure("halloween", false, true, true); Pump();
                Check(nearHost.Clip is null && layers[4].OpacityMask is null, "A hard title-bar seam or lower-bush fade remains");
                foreach (int index in new[] { 1, 3, 4 })
                    Check(layers[index].Width == layers[0].Width && layers[index].Height == layers[0].Height
                        && Canvas.GetLeft(layers[index]) == Canvas.GetLeft(layers[0]) && Canvas.GetTop(layers[index]) == Canvas.GetTop(layers[0]),
                        "Extracted artwork planes lost their shared image registration");
                var fixedBounds = controls.Select(c => Bounds(c, root)).ToArray();
                foreach (var camera in new[] { new Point(), new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1) })
                {
                    typeof(AtmosphericBackdrop).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(backdrop, camera);
                    Invoke(backdrop, "ApplyCamera"); Pump();
                    var positions = layers.Select(layer => layer.TransformToVisual(root).Transform(new Point())).ToArray();
                    double[] travelX = [4, 7, 11, 15, 24], travelY = [2.5, 4, 6, 8.5, 14];
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var transform = (TranslateTransform)layers[i].RenderTransform;
                        Check(Math.Abs(transform.X + camera.X * travelX[i]) < .0001 && Math.Abs(transform.Y + camera.Y * travelY[i]) < .0001,
                            $"Depth layer {i} does not have its independent camera travel");
                    }
                    for (int i = 0; i < controls.Length; i++)
                        Check(Bounds(controls[i], root) == fixedBounds[i], $"Camera moves fixed control: {controls[i].Name}: {fixedBounds[i]} -> {Bounds(controls[i], root)}");
                    Check(Bounds(layers[0], root).Contains(new Rect(root.RenderSize)), "Camera exposes an empty forest edge");
                    var start = controls[2]; var center = start.TransformToVisual(root).Transform(new Point(start.ActualWidth / 2, start.ActualHeight / 2));
                    var hit = root.InputHitTest(center) as DependencyObject;
                    while (hit is not null && hit != start) hit = VisualTreeHelper.GetParent(hit);
                    Check(hit == start, "Near trees or bushes intercept start button input");
                    rows.Add(new { width, height, x = camera.X, y = camera.Y, layers = positions.Select(p => new { p.X, p.Y }) });
                    if (camera == new Point() || camera == new Point(1, 1))
                        Render(root, Path.Combine(output, $"halloween-{width}x{height}-{camera.X}-{camera.Y}.png"));
                }
            }
            var nearImage = (BitmapSource)layers[4].Source;
            Check(AlphaAt(nearImage, .03, .94) > 160 && AlphaAt(nearImage, .97, .94) > 160, "Blurred lower near bushes are missing");
            Check(AlphaAt(nearImage, .5, .5) < 8, "Near frame has an opaque center");
            Check(AlphaAt((BitmapSource)layers[1].Source, .5, .3) < 8 && AlphaAt((BitmapSource)layers[1].Source, .85, .5) > 160, "Castle is not an independent transparent layer");
            window.Width = 1280; window.Height = 800; Pump();
            backdrop.Configure("halloween", false, true, false);
            backdrop.SetPointer(new Point(backdrop.ActualWidth, backdrop.ActualHeight));
            PumpFor(TimeSpan.FromMilliseconds(550));
            Check(((TranslateTransform)layers[4].RenderTransform).X < -15, "Real camera timer did not move near frame");
            backdrop.Configure("halloween", false, true, true); Pump();
            Check(layers.All(layer => ((TranslateTransform)layer.RenderTransform).X == 0 && ((TranslateTransform)layer.RenderTransform).Y == 0), "Reduced motion did not reset all depth layers");
            Check(!Field<DispatcherTimer>(backdrop, "_camera").IsEnabled, "Reduced motion leaves camera timer running");
            Invoke(window, "PreviewTheme", "summer", false); Pump();
            Check(layers.Take(3).All(layer => layer.Source is null), "Halloween layers leak into other themes");
            Invoke(window, "PreviewTheme", "halloween", false); Pump();
            Check(layers.All(layer => layer.Source is not null), "Theme switch did not restore layered scene");
            File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new { success = true, independentDepthPlanes = 5, cameraLayouts = rows, fixedInterface = true, blurredNearBushes = true, reducedMotion = true, themeSwitch = true }, new JsonSerializerOptions { WriteIndented = true }));
            File.Delete(Path.Combine(output, "failure.txt"));
            Console.WriteLine("PARALLAX_UI_OK: 5 depth planes, 25 camera/viewport cases, fixed UI, lower bushes and reduced motion");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
    }
    private static byte AlphaAt(BitmapSource source, double x, double y)
    {
        var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4]; image.CopyPixels(new Int32Rect((int)(x * image.PixelWidth), (int)(y * image.PixelHeight), 1, 1), pixel, 4, 0);
        return pixel[3];
    }
    private static Rect Bounds(FrameworkElement element, Visual root) => element.TransformToVisual(root).TransformBounds(new Rect(element.RenderSize));
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) => frame.Continue = false; timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
    }
    private static void Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file);
    }
}
