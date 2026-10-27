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

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/status-ui");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        try
        {
            window.Show(); Pump(); Invoke(window, "PreviewTheme", "halloween", false);
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            var panel = (StackPanel)window.FindName("ProgressPanel");
            var detail = (TextBlock)window.FindName("ProgressText");
            var speed = (TextBlock)window.FindName("SpeedText");
            var status = (TextBlock)window.FindName("StatusText");
            var bar = (ProgressBar)window.FindName("DownloadProgressBar");
            Invoke(window, "PreviewDownload");
            Invoke(window, "ResetProgress", "Installierte Version und Update-Kanal werden geprüft …");
            Check(panel.IsVisible && detail.Text.Contains("Update-Kanal") && !detail.Text.Contains("vorbereitet"), "Vague preparation message remains");
            Invoke(window, "ApplyProgress", new UpdateProgress(UpdateStage.Checking, 35, 0, null, 0, "Spieldateien werden geprüft (35 von 100) …"));
            Check(detail.Text.Contains("Prüfung") && detail.Text.Contains("35%") && status.Text.Contains("35 von 100"), "Checking phase is not informative");
            Invoke(window, "ApplyProgress", new UpdateProgress(UpdateStage.Downloading, null, 42000, null, 0, "Update wird heruntergeladen …"));
            Check(!detail.Text.Contains('?') && detail.Text.Contains("heruntergeladen") && bar.IsIndeterminate, "Unknown download length uses a fake total");
            Invoke(window, "ApplyProgress", new UpdateProgress(UpdateStage.Downloading, 42, 42000000, 100000000, 8000000, "Update wird heruntergeladen …"));
            Check(detail.Text.Contains(" von ") && speed.Text.EndsWith("/s") && !bar.IsIndeterminate, "Download size or speed missing");
            Invoke(window, "ApplyProgress", new UpdateProgress(UpdateStage.Extracting, 20, 0, null, 0, "Spieldateien werden entpackt …"));
            Check(detail.Text.Contains("Entpacken") && speed.Text.Length == 0, "Old download speed remains during extraction");
            Invoke(window, "ApplyProgress", new UpdateProgress(UpdateStage.Installing, 50, 0, null, 0, "Vorherige Spieldateien werden gesichert …"));
            Check(detail.Text.Contains("Installation") && status.Text.Contains("gesichert"), "Installation phase missing");
            foreach (string result in new[] { "Reborn 7.3.9 ist aktuell. Bereit zum Spielen.", "Update abgebrochen.", "Spielupdate fehlgeschlagen. Download nicht verfügbar." })
            {
                Invoke(window, "PreviewDownload");
                IProgress<UpdateProgress> late = null!;
                window.Dispatcher.Invoke(() => late = new Progress<UpdateProgress>(value => Invoke(window, "ApplyProgress", value)));
                late.Report(new UpdateProgress(UpdateStage.Complete, 100, 0, null, 0, "Verspätete Fertigmeldung"));
                Invoke(window, "SetStatus", result);
                Invoke(window, "EndDownloadPreview"); Pump(); window.UpdateLayout();
                Check(!panel.IsVisible && detail.Text.Length == 0 && speed.Text.Length == 0 && !bar.IsIndeterminate && bar.Value == 0,
                    "Idle view retains progress or spinner");
                Check(status.Text == result, "Queued progress overwrites the final result");
            }
            Invoke(window, "SetStatus", "Reborn 7.3.9 ist aktuell. Bereit zum Spielen.");
            window.Width = 1280; window.Height = 800; Pump(); window.UpdateLayout();
            Invoke(window, "VerifyVisibleControls");
            var root = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(output, "status-ready.png"))) encoder.Save(file);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { success = true, concretePhases = true, downloadDetails = true, idleProgressHidden = true, lateProgressIgnored = true, errorsPreserved = true }));
            Console.WriteLine("STATUS_UI_OK concrete phases, download size/speed, idle cleanup, queued progress ignored, results preserved");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
    }
    private static void Invoke(MainWindow window, string method, params object[] args) => typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
