using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokemonRebornLauncher;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/fireworks");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        MainWindow? window = null;
        try
        {
            if (args.Contains("--render-only"))
            {
                RenderShowcase(output);
                app.Shutdown(); return 0;
            }
            window = new MainWindow(smokeTest: true) { Width = 1280, Height = 800 };
            app.MainWindow = window;
            window.Show(); window.Activate(); Pump();
            window.WindowState = WindowState.Normal;
            window.Width = 1280; window.Height = 800;
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            Invoke(window, "PreviewTheme", "newyear", false);
            window.UpdateLayout(); Pump();
            var fireworks = (FireworkScene)window.FindName("FireworksView");
            var button = (Button)window.FindName("StartGameButton");
            var content = (FrameworkElement)window.Content;
            Check(fireworks.ActualWidth == content.ActualWidth && fireworks.ActualHeight == content.ActualHeight, "Overlay does not cover whole launcher");
            Check(!fireworks.IsHitTestVisible && !fireworks.Focusable, "Overlay intercepts input");
            var root = (Grid)window.FindName("Root");
            Check(root.Children.IndexOf(fireworks) < root.Children.IndexOf((UIElement)window.FindName("MainShell")), "Fireworks cover text and controls");
            Check(root.Children.IndexOf(fireworks) < root.Children.IndexOf((UIElement)window.FindName("NearSceneLayer")), "Fireworks cover the nearest branches");
            var center = button.TransformToAncestor(content).Transform(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            // Real WPF input routing skips the click-through scenery layers.
            var hit = content.InputHitTest(center) as DependencyObject;
            while (hit is not null && hit != button) hit = VisualTreeHelper.GetParent(hit);
            Check(hit == button, "Start button is not reachable through overlay");
            fireworks.Configure(true, 960, 1.6, false, true);
            int maxParticles = 0, maxBursts = 0, maxSecondary = 0, maxAtmosphere = 0, maxVisibleSparks = 0;
            for (int i = 0; i < 108; i++)
            {
                // The user's foreground window can change during an offscreen QA run.
                // Explicitly supply the tested active state for each simulated frame.
                fireworks.Configure(true, 960, 1.6, false, true);
                fireworks.AdvanceForTest(.25);
                maxBursts = Math.Max(maxBursts, fireworks.BurstCount);
                maxSecondary = Math.Max(maxSecondary, fireworks.SecondaryBurstCount);
                maxAtmosphere = Math.Max(maxAtmosphere, fireworks.ActiveAtmosphereCount);
                Pump(); window.UpdateLayout();
                Check(fireworks.ActiveParticleCount <= 960 && fireworks.ActiveAtmosphereCount <= 18, "Overlay budget exceeded");
                maxParticles = Math.Max(maxParticles, fireworks.ActiveParticleCount);
                if (fireworks.VisibleSparkCount > maxVisibleSparks)
                {
                    maxVisibleSparks = fireworks.VisibleSparkCount;
                }
            }
            // PNG encoding belongs to selected evidence only, never every UI frame.
            Render(content, Path.Combine(output, "launcher-fireworks.png"));
            Check(maxParticles > 100 && maxBursts >= 10 && maxSecondary > 0 && maxAtmosphere > 0, "No rich multi-stage show");
            Check(!fireworks.IsHitTestVisible && content.InputHitTest(center) is not FireworkScene, "Dense fireworks intercept input");
            var inputTimes = new List<double>();
            for (int i = 0; i < 12; i++)
            {
                var elapsed = Stopwatch.StartNew();
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { inputTimes.Add(elapsed.Elapsed.TotalMilliseconds); frame.Continue = false; }));
                Dispatcher.PushFrame(frame);
                PumpFor(TimeSpan.FromMilliseconds(50));
            }
            Check(inputTimes.Max() < 250, "Fireworks starve dispatcher input");
            var settings = (LauncherSettings)typeof(MainWindow).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            foreach (string gate in new[] { "reduced", "off", "disabled" })
            {
                settings.ReduceMotion = gate == "reduced";
                settings.EventParticleQuality = gate == "off" ? "off" : "medium";
                settings.EventDecorationsEnabled = gate != "disabled";
                Invoke(window, "ApplyEventPresentation");
                Check(!fireworks.IsAnimating && fireworks.ActiveParticleCount == 0 && fireworks.ActiveAtmosphereCount == 0, "Settings do not stop fireworks: " + gate);
            }
            settings.ReduceMotion = false; settings.EventParticleQuality = "medium"; settings.EventDecorationsEnabled = true;
            Invoke(window, "PreviewTheme", "winter", false);
            Check(!fireworks.IsAnimating && fireworks.ActiveParticleCount == 0 && fireworks.ActiveAtmosphereCount == 0, "Fireworks leaked into winter");
            Invoke(window, "PreviewTheme", "newyear", true);
            window.Width = window.MinWidth; window.Height = window.MinHeight; window.UpdateLayout(); Pump();
            fireworks.Configure(true, 60, 1, false, true); fireworks.AdvanceForTest(5);
            Check(fireworks.ActiveParticleCount <= 60, "Low budget exceeded");
            Render(content, Path.Combine(output, "launcher-fireworks-compact-day.png"));
            window.Hide(); Pump();
            Check(!fireworks.IsAnimating && fireworks.ActiveParticleCount == 0 && fireworks.ActiveAtmosphereCount == 0, "Hidden overlay keeps running");
            window.Close();
            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { success = true, maxParticles, maxVisibleSparks, maxBursts, maxSecondary, maxAtmosphere, fullWindow = true, clickThrough = true, textInFront = true, secondaryExplosions = true, atmosphere = true, settings = true, themeReset = true, compact = true, hiddenStopped = true,
                maxInputDispatchMs = inputTimes.Max(), inputCallbacks = inputTimes.Count, screenshots = 2 }));
            Console.WriteLine("FIREWORK_UI_OK full-window overlay, click-through, auto bursts, settings, theme reset, compact day, hidden lifecycle");
            app.Shutdown(); return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
            window?.Close(); app.Shutdown(); return 1;
        }
    }
    private static void Invoke(MainWindow window, string method, params object[] args) => typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
    private static void RenderShowcase(string output)
    {
        // No Window or focus changes: inspect the renderer while other UI tests run.
        var simulation = new FireworkSimulation(2027);
        var scene = new FireworkScene(simulation);
        var canvas = new Grid { Width = 1280, Height = 800, Background = new SolidColorBrush(Color.FromRgb(5, 9, 24)) };
        canvas.Children.Add(scene); canvas.Measure(new Size(1280, 800)); canvas.Arrange(new Rect(0, 0, 1280, 800)); canvas.UpdateLayout();
        scene.Configure(true, 960, 1.6, false, true);
        var captured = new HashSet<FireworkBurstKind>();
        var pending = new List<(FireworkBurstKind Kind, double At)>();
        double now = 0;
        simulation.BurstCreated += burst =>
        {
            if (!burst.IsSecondary && !captured.Contains(burst.Kind) && !pending.Any(p => p.Kind == burst.Kind))
                pending.Add((burst.Kind, now + (burst.Kind is FireworkBurstKind.MultiBreak or FireworkBurstKind.Crossette ? 1.4 : .8)));
        };
        for (int i = 0; i < 900 && captured.Count < Enum.GetValues<FireworkBurstKind>().Length; i++)
        {
            now += .1; scene.AdvanceForTest(.1); canvas.UpdateLayout();
            foreach (var capture in pending.Where(p => p.At <= now).ToArray())
            {
                Render(canvas, Path.Combine(output, $"effect-{capture.Kind}.png"));
                captured.Add(capture.Kind); pending.Remove(capture);
            }
        }
        Check(captured.Count == 14 && simulation.SecondaryBurstCount > 0, "Offscreen showcase missed authored effects");
        var report = new { success = true, families = captured.Select(c => c.ToString()).ToArray(), secondaryBursts = simulation.SecondaryBurstCount, atmosphereLimit = scene.ActiveAtmosphereCount <= 18, nativeWindowsOpened = 0 };
        File.WriteAllText(Path.Combine(output, "showcase.json"), System.Text.Json.JsonSerializer.Serialize(report));
        Console.WriteLine("FIREWORK_RENDER_OK all 14 effect families rendered without native windows");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static double Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var watch = Stopwatch.StartNew(); bitmap.Render(view); watch.Stop();
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
        return watch.Elapsed.TotalMilliseconds;
    }
}
