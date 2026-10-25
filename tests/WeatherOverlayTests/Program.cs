using System.IO;
using System.Reflection;
using System.Text.Json;
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
    private static int _exitCode;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/weather-overlay");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try { await Run(output); }
            catch (Exception ex) { Console.Error.WriteLine(ex); _exitCode = 1; }
            finally { app.Shutdown(); }
        }));
        app.Run();
        return _exitCode;
    }

    private static async Task Run(string output)
    {
        var weather = new SeasonalWeatherOverlay();
        var host = new Window { Width = 960, Height = 640, Content = weather, WindowStyle = WindowStyle.None, ShowInTaskbar = false };
        host.Show();
        await Dispatcher.Yield(DispatcherPriority.Loaded);
        host.UpdateLayout();
        int frames = 0;
        foreach (string theme in ThemeCalendar.ThemeIds)
        foreach (bool day in new[] { false, true })
        {
            weather.Configure(theme, day, true, 90, 1, false, false);
            weather.SetReadingZones([new Rect(300, 180, 360, 280)]);
            var first = Render(weather);
            weather.AdvanceForTest(2.4);
            var moved = Render(weather);
            Check(Difference(first, moved) > 30, "Keine sichtbare Wetterbewegung: " + theme);
            Check(weather.ParticleCount is > 0 and <= 90, "Partikellimit: " + theme);
            Check(!weather.IsHitTestVisible && !weather.Focusable && !weather.IsAnimating, "Wetter blockiert UI oder läuft inaktiv weiter.");
            Check(EmptyRows(moved, 0, 64) && EmptyRows(moved, moved.PixelHeight - 34, 34), "Navigation/Statusleiste werden übermalt.");
            Save(moved, Path.Combine(output, $"weather-{theme}-{(day ? "day" : "night")}.png"));
            frames++;
        }
        weather.Configure("winter", false, true, 160, 1.6, true, true);
        var quiet = Render(weather);
        weather.AdvanceForTest(3);
        Check(Difference(quiet, Render(weather)) == 0 && !weather.IsAnimating, "Ruhige Effekte bewegen sich weiter.");
        weather.Configure("winter", false, true, 90, 1, false, true);
        Check(weather.IsAnimating, "Aktives Wetter startet nicht.");
        host.Hide();
        Check(!weather.IsAnimating, "Verstecktes Wetter läuft weiter.");
        host.Show();
        weather.Configure("winter", false, true, 0, 1, false, true);
        Check(weather.ParticleCount == 0 && !weather.IsAnimating && weather.Visibility == Visibility.Collapsed, "Qualität aus beendet Wetter nicht.");
        weather.Configure("winter", false, false, 90, 1, false, true);
        Check(weather.ParticleCount == 0 && !weather.IsAnimating, "Dekorationen aus beendet Wetter nicht.");
        host.Close();

        var window = new MainWindow(smokeTest: true);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.Loaded);
        window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
        var integrated = (SeasonalWeatherOverlay)typeof(MainWindow).GetField("_seasonalWeather", Flags)!.GetValue(window)!;
        foreach (var (width, height) in new[] { (960, 640), (1280, 800) })
        {
            window.Width = width; window.Height = height;
            foreach (string theme in ThemeCalendar.ThemeIds)
            {
                typeof(MainWindow).GetMethod("PreviewTheme", Flags)!.Invoke(window, [theme, false]);
                window.UpdateLayout();
                integrated.Configure(theme, false, true, 90, 1, false, false);
                integrated.AdvanceForTest(2.4);
                var root = (FrameworkElement)window.Content;
                Check(integrated.RenderSize == root.RenderSize, "Wetter bedeckt nicht die ganze Launcherfläche.");
                var start = (Button)window.FindName("StartGameButton");
                Point point = start.TranslatePoint(new Point(start.ActualWidth / 2, start.ActualHeight / 2), root);
                // InputHitTest observes IsHitTestVisible on transparent scenery parents,
                // unlike a raw visual-layer enumeration.
                DependencyObject? firstHit = root.InputHitTest(point) as DependencyObject;
                DependencyObject? hit = firstHit;
                while (hit is not null && hit != start) hit = VisualTreeHelper.GetParent(hit);
                if (hit != start)
                {
                    Save(Render(root), Path.Combine(output, "failed-hit.png"));
                    throw new InvalidOperationException($"Starttreffer {theme}: Ziel={point}, sichtbar={start.IsVisible}, enabled={start.IsEnabled}, Treffer={firstHit?.GetType().Name}/{(firstHit as FrameworkElement)?.Name}, Fenster={root.RenderSize}");
                }
                Save(Render(root), Path.Combine(output, $"launcher-{theme}-{width}x{height}.png"));
            }
        }
        window.Close();
        Check(!integrated.IsAnimating, "Geschlossenes Launcherfenster hält Wettertimer am Leben.");

        var durations = new Dictionary<string, double>();
        foreach (string theme in ThemeCalendar.ThemeIds)
            durations[theme] = (await LauncherMusic.RunMutedCodecSmokeCheckAsync(TimeSpan.FromSeconds(10), themeId: theme)).TotalSeconds;
        Check(durations.Count == 8 && durations.Values.All(value => value > 0), "Nicht alle8 Themes dekodieren Musik.");
        int effectWavs = await (Task<int>)typeof(EventSoundBank).GetMethod("VerifyMutedCodecsAsync", Flags)!.Invoke(null, null)!;
        Check(effectWavs == 50, "Fehlende Eventgeräusche.");
        var result = new { success = true, weatherFrames = frames, integratedThemesAndSizes = 16, musicThemes = durations, effectWavs,
            clickThrough = true, protectedChrome = true, reducedMotion = true, disabledAndHiddenTimersStopped = true };
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("WEATHER_OVERLAY_OK themes=8 frames=16 layouts=16 music=8 effects=50");
    }

    private static RenderTargetBitmap Render(FrameworkElement view)
    {
        view.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(view); return image;
    }
    private static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var data = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(data, image.PixelWidth * 4, 0); return data;
    }
    private static int Difference(BitmapSource a, BitmapSource b)
    {
        var first = Pixels(a); var second = Pixels(b); int changed = 0;
        for (int i = 0; i < first.Length; i += 4)
            if (first[i] != second[i] || first[i + 1] != second[i + 1] || first[i + 2] != second[i + 2] || first[i + 3] != second[i + 3]) changed++;
        return changed;
    }
    private static bool EmptyRows(BitmapSource image, int start, int count)
    {
        var pixels = Pixels(image);
        for (int y = start; y < start + count; y++)
        for (int x = 0; x < image.PixelWidth; x++) if (pixels[(y * image.PixelWidth + x) * 4 + 3] != 0) return false;
        return true;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
