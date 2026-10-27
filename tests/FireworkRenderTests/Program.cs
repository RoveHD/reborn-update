using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/firework-render");
        Directory.CreateDirectory(output);
        var reports = new List<object>();
        foreach (int budget in new[] { 480, 960 })
        foreach (int width in new[] { 1280, 2560 })
        {
            int height = width * 5 / 8;
            var scene = new FireworkScene(new FireworkSimulation(2027));
            var canvas = new Grid { Width = width, Height = height, Background = new SolidColorBrush(Color.FromRgb(5, 9, 24)) };
            canvas.Children.Add(scene);
            canvas.Measure(new Size(width, height)); canvas.Arrange(new Rect(0, 0, width, height));
            scene.Configure(true, budget, 1.6, false, true);
            var times = new List<double>();
            int peak = 0;
            // Direct, bounded software-raster sample. No native window, live timer or PNG loop.
            for (int i = 0; i < 90; i++)
            {
                scene.AdvanceForTest(.4); canvas.UpdateLayout();
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                var watch = Stopwatch.StartNew(); bitmap.Render(canvas); watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds); peak = Math.Max(peak, scene.VisibleSparkCount);
                if (i == 18)
                {
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, $"fireworks-{budget}-{width}.png")); png.Save(file);
                }
            }
            times.Sort();
            if (peak < 100 || scene.SecondaryBurstCount == 0) throw new InvalidOperationException("Render sample missed the dense multi-stage show.");
            var report = new { budget, width, height, frames = times.Count, peakVisibleSparks = peak, medianMs = times[times.Count / 2], p95Ms = times[(int)(times.Count * .95)], nativeWindowsOpened = 0 };
            reports.Add(report); Console.WriteLine(JsonSerializer.Serialize(report));
            scene.Configure(false, 0, 1, false, false);
            if (scene.ActiveParticleCount != 0 || scene.ActiveAtmosphereCount != 0) return 1;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(reports));
        return 0;
    }
}
