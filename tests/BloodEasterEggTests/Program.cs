using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/blood-easter-egg");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        MainWindow? window = null;
        try
        {
            window = new MainWindow(smokeTest: true) { Width = 1280, Height = 800 };
            app.MainWindow = window;
            window.Show(); window.Activate(); Pump();
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;

            var scene = (BloodSplashScene)window.FindName("BloodView");
            var logo = (FrameworkElement)window.FindName("LogoFrame");
            var content = (FrameworkElement)window.Content;
            var startButton = (Button)window.FindName("StartGameButton");
            Check(scene.ActualWidth == content.ActualWidth && scene.ActualHeight == content.ActualHeight,
                "Blood overlay does not cover the full launcher.");
            Check(!scene.IsHitTestVisible && !scene.Focusable, "Blood overlay can intercept input.");

            for (int i = 0; i < 4; i++) ClickLogo(logo);
            Check(!scene.IsAnimating && scene.SplashCount == 0, "Four clicks incorrectly triggered the Easter egg.");
            PumpFor(TimeSpan.FromSeconds(4.15));
            ClickLogo(logo); // The expired sequence must restart at click one.
            for (int i = 0; i < 3; i++) ClickLogo(logo);
            Check(!scene.IsAnimating, "Expired clicks were counted toward the next sequence.");
            ClickLogo(logo);
            Check(scene.IsAnimating && scene.DropletCount == 42, "Five fresh clicks did not start the spray.");

            var buttonCenter = startButton.TransformToAncestor(content)
                .Transform(new Point(startButton.ActualWidth / 2, startButton.ActualHeight / 2));
            // InputHitTest follows WPF input routing, including click-through
            // foreground/weather layers; raw visual enumeration does not.
            var hit = content.InputHitTest(buttonCenter) as DependencyObject;
            while (hit is not null && hit != startButton) hit = VisualTreeHelper.GetParent(hit);
            Check(hit == startButton, "Start button is not reachable through the blood overlay.");

            // A second full trigger replaces the first effect instead of stacking marks.
            for (int i = 0; i < 5; i++) ClickLogo(logo);
            Check(scene.DropletCount == 42 && scene.SplashCount == 0 && scene.DripCount == 0,
                "Repeated trigger stacked effects instead of replacing them.");
            scene.AdvanceForTest(1.8);
            Check(scene.SplashCount > 10 && scene.DripCount > 10 && scene.DropletCount < 42,
                "Droplets did not impact across the window and create growing drips.");
            window.UpdateLayout(); Pump();
            Render(content, Path.Combine(output, "blood-splashes-and-drips.png"));
            scene.AdvanceForTest(7.5);
            Check(!scene.IsAnimating && scene.SplashCount == 0 && scene.DripCount == 0 && scene.DropletCount == 0,
                "The blood effect did not fade and clean itself up.");

            var settings = (LauncherSettings)typeof(MainWindow)
                .GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            settings.ReduceMotion = true;
            Invoke(window, "ApplyEventPresentation");
            for (int i = 0; i < 5; i++) ClickLogo(logo);
            Check(scene.DropletCount == 0 && scene.SplashCount > 10 && scene.DripCount > 10,
                "Reduced motion did not use a still splatter image.");
            scene.AdvanceForTest(2.8);
            Check(!scene.IsAnimating && scene.SplashCount == 0, "Still reduced-motion effect did not fade out.");
            settings.ReduceMotion = false;
            Invoke(window, "ApplyEventPresentation");

            for (int i = 0; i < 5; i++) ClickLogo(logo);
            typeof(MainWindow).GetField("_gameRunning", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
            Invoke(window, "ApplyEventPresentation");
            Check(!scene.IsAnimating && scene.SplashCount == 0, "Running game did not stop the overlay.");
            typeof(MainWindow).GetField("_gameRunning", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, false);
            Invoke(window, "ApplyEventPresentation");

            for (int i = 0; i < 5; i++) ClickLogo(logo);
            window.WindowState = WindowState.Minimized; Pump();
            Check(!scene.IsAnimating && scene.SplashCount == 0, "Minimizing did not stop the overlay.");
            window.WindowState = WindowState.Normal; window.Activate(); Pump();
            for (int i = 0; i < 5; i++) ClickLogo(logo);
            window.Hide(); Pump();
            Check(!scene.IsAnimating && scene.SplashCount == 0, "Hiding did not stop the overlay.");
            window.Close();

            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                fiveClicks = true,
                fourClicksDoNotTrigger = true,
                timeoutResetsSequence = true,
                repeatedTriggerReplaces = true,
                fullWindow = true,
                clickThrough = true,
                splashes = true,
                growingDrips = true,
                naturalFadeCleanup = true,
                reducedMotionStaticFade = true,
                gameStops = true,
                minimizeStops = true,
                hiddenStops = true,
                screenshot = "blood-splashes-and-drips.png"
            }));
            Console.WriteLine("BLOOD_EASTER_EGG_OK click threshold/reset, replacement, whole-window drips, fade, reduced motion, game/minimize/hidden gates, click-through");
            app.Shutdown(); return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
            window?.Close(); app.Shutdown(); return 1;
        }
    }

    private static void ClickLogo(FrameworkElement logo) => logo.RaiseEvent(new MouseButtonEventArgs(
        Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
    private static void Invoke(MainWindow window, string method, params object[] args) =>
        typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }
    private static void Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
}
