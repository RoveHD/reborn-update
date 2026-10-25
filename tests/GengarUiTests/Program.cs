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
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/gengar");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        MainWindow? window = null;
        try
        {
            VerifyStaleAudioCallback();

            window = new MainWindow(smokeTest: true) { Width = 1280, Height = 800 };
            app.MainWindow = window;
            window.Show(); window.Activate(); Pump();
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            Invoke(window, "PreviewTheme", "halloween", false);
            window.UpdateLayout(); Pump();

            var scene = (GengarScene)window.FindName("GengarView");
            var content = (FrameworkElement)window.Content;
            var button = (Button)window.FindName("StartGameButton");
            Check(scene.ActualWidth == content.ActualWidth && scene.ActualHeight == content.ActualHeight,
                "Gengar overlay does not cover the full launcher.");
            Check(!scene.IsHitTestVisible && !scene.Focusable, "Gengar overlay can intercept input.");
            var center = button.TransformToAncestor(content).Transform(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            // Use WPF input routing, which honors click-through scenery layers.
            var hit = content.InputHitTest(center) as DependencyObject;
            while (hit is not null && hit != button) hit = VisualTreeHelper.GetParent(hit);
            Check(hit == button, "Start button is not reachable through the Gengar overlay.");

            int laughs = 0;
            scene.LaughRequested += () => laughs++;
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: true);
            Check(scene.IsAnimating, "Eligible Halloween scene did not start its wait timer.");
            scene.TriggerFlyByForTest();
            scene.AdvanceForTest(.9);
            Check(scene.IsFlying && laughs == 1, "Controlled Gengar fly-by did not produce exactly one laugh cue.");
            window.UpdateLayout(); Pump();
            Render(content, Path.Combine(output, "gengar-flyby.png"));
            scene.AdvanceForTest(2);
            Check(!scene.IsFlying && laughs == 1, "Gengar flight did not end cleanly or repeated its laugh.");

            // Drive the real production timer into a near-due state, then let WPF tick naturally.
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: true);
            typeof(GengarScene).GetField("_secondsUntilNext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(scene, .15);
            PumpFor(TimeSpan.FromSeconds(1.2));
            Check(scene.IsFlying, "The real idle timer did not start a fly-by.");
            PumpFor(TimeSpan.FromSeconds(2.8));
            Check(!scene.IsFlying && scene.IsAnimating, "The real timer did not return to its idle schedule after flight.");
            var timer = (DispatcherTimer)typeof(GengarScene).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
            Check(timer.IsEnabled && Math.Abs(timer.Interval.TotalSeconds - 1) < .01,
                "Gengar did not return to the low-frequency idle timer.");
            Check(laughs == 2, "The real timer did not request exactly one laugh for its fly-by.");

            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: true, active: true);
            Check(!scene.IsAnimating && !scene.IsFlying, "Reduced motion did not stop and clear Gengar.");
            scene.Configure("winter", decorationsEnabled: true, reduceMotion: false, active: true);
            Check(!scene.IsAnimating && !scene.IsFlying, "Theme change did not stop Gengar.");
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: false);
            Check(!scene.IsAnimating && !scene.IsFlying, "Inactive-window gate did not stop Gengar.");
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: true);
            scene.TriggerFlyByForTest();
            window.Hide(); Pump();
            Check(!scene.IsAnimating && !scene.IsFlying, "Hidden launcher kept the Gengar scene running.");
            window.Close();

            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                fullWindow = true,
                clickThrough = true,
                controlledFlight = true,
                laughCues = laughs,
                reducedMotionStops = true,
                themeChangeStops = true,
                inactiveStops = true,
                hiddenStops = true,
                staleAudioCallbackSafe = true,
                screenshot = "gengar-flyby.png"
            }));
            Console.WriteLine("GENGAR_UI_OK full-window, click-through, flight, one laugh, settings, theme, inactive, hidden, stale audio callback");
            app.Shutdown(); return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
            window?.Close(); app.Shutdown(); return 1;
        }
    }

    private static void VerifyStaleAudioCallback()
    {
        using var audio = new HalloweenAmbientAudio();
        audio.ConfigureJumpscareLaugh(enabled: true, eventVolume: .2, paused: false);
        var current = new MediaPlayer { IsMuted = true };
        var stale = new MediaPlayer { IsMuted = true };
        var field = typeof(HalloweenAmbientAudio).GetField("jumpscareVoice", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(audio, current);
        typeof(HalloweenAmbientAudio).GetMethod("OnJumpscareOpened", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(audio, [stale, EventArgs.Empty]);
        Check(ReferenceEquals(field.GetValue(audio), current), "A stale audio callback stopped a newer Gengar laugh.");
    }

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
