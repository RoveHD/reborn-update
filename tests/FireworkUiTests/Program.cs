using System.IO;
using System.Reflection;
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
            window = new MainWindow(smokeTest: true) { Width = 1280, Height = 800 };
            app.MainWindow = window;
            window.Show(); window.Activate(); Pump();
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            Invoke(window, "PreviewTheme", "newyear", false);
            window.UpdateLayout(); Pump();
            var fireworks = (FireworkScene)window.FindName("FireworksView");
            var button = (Button)window.FindName("StartGameButton");
            var content = (FrameworkElement)window.Content;
            Check(fireworks.ActualWidth == content.ActualWidth && fireworks.ActualHeight == content.ActualHeight, "Overlay does not cover whole launcher");
            Check(!fireworks.IsHitTestVisible && !fireworks.Focusable, "Overlay intercepts input");
            var center = button.TransformToAncestor(content).Transform(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            var hit = VisualTreeHelper.HitTest(content, center)?.VisualHit;
            while (hit is not null && hit != button) hit = VisualTreeHelper.GetParent(hit);
            Check(hit == button, "Start button is not reachable through overlay");
            fireworks.Configure(true, 240, 1.6, false, true);
            int maxParticles = 0, maxBursts = 0;
            for (int i = 0; i < 90; i++)
            {
                // The user's foreground window can change during an offscreen QA run.
                // Explicitly supply the tested active state for each simulated frame.
                fireworks.Configure(true, 240, 1.6, false, true);
                fireworks.AdvanceForTest(.12);
                maxBursts = Math.Max(maxBursts, fireworks.BurstCount);
                Pump(); window.UpdateLayout();
                Check(fireworks.ActiveParticleCount <= 240, "Overlay budget exceeded");
                if (fireworks.ActiveParticleCount > maxParticles)
                {
                    maxParticles = fireworks.ActiveParticleCount;
                    Render(content, Path.Combine(output, "launcher-fireworks.png"));
                }
                Render(content, Path.Combine(output, $"frame-{i:000}.png"));
            }
            Check(maxParticles > 10 && maxBursts >= 2, "No automatic explosions");
            var settings = (LauncherSettings)typeof(MainWindow).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            foreach (string gate in new[] { "reduced", "off", "disabled" })
            {
                settings.ReduceMotion = gate == "reduced";
                settings.EventParticleQuality = gate == "off" ? "off" : "medium";
                settings.EventDecorationsEnabled = gate != "disabled";
                Invoke(window, "ApplyEventPresentation");
                Check(!fireworks.IsAnimating && fireworks.ActiveParticleCount == 0, "Settings do not stop fireworks: " + gate);
            }
            settings.ReduceMotion = false; settings.EventParticleQuality = "medium"; settings.EventDecorationsEnabled = true;
            Invoke(window, "PreviewTheme", "winter", false);
            Check(!fireworks.IsAnimating && fireworks.ActiveParticleCount == 0, "Fireworks leaked into winter");
            Invoke(window, "PreviewTheme", "newyear", true);
            window.Width = window.MinWidth; window.Height = window.MinHeight; window.UpdateLayout(); Pump();
            fireworks.Configure(true, 60, 1, false, true); fireworks.AdvanceForTest(5);
            Check(fireworks.ActiveParticleCount <= 60, "Low budget exceeded");
            Render(content, Path.Combine(output, "launcher-fireworks-compact-day.png"));
            window.Hide(); Pump();
            Check(!fireworks.IsAnimating && fireworks.ActiveParticleCount == 0, "Hidden overlay keeps running");
            window.Close();
            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { success = true, maxParticles, maxBursts, fullWindow = true, clickThrough = true, settings = true, themeReset = true, compact = true, hiddenStopped = true }));
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
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
}
