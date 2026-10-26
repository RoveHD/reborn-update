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
            var backdrop = (AtmosphericBackdrop)window.FindName("AtmosphereView");
            var patch = (Image)typeof(AtmosphericBackdrop).GetField("_gengar", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backdrop)!;
            Check(patch.Source is BitmapSource { PixelWidth: 475, PixelHeight: 475 }, "Gengar artwork dimensions differ from the existing character artwork.");
            Check(patch.Opacity == 1, "Halloween did not show the independent Gengar layer initially.");
            // Settle camera and any preview lighting transition before comparing exact pixels.
            backdrop.Configure("halloween", daylight: false, active: true, reduceMotion: true);
            backdrop.Configure("halloween", daylight: false, active: true, reduceMotion: false);
            Render(content, Path.Combine(output, "gengar-before.png"));
            var before = Capture(backdrop);
            scene.TriggerFlyByForTest();
            scene.AdvanceForTest(.32);
            Check(scene.IsEncounterActive && !scene.IsFlying && scene.BackdropAbsence > .3 && scene.BackdropAbsence < .7 && patch.Opacity > .3 && patch.Opacity < .7,
                "Gengar did not fade smoothly out of the independent Gengar layer.");
            scene.AdvanceForTest(.55);
            Check(!scene.IsFlying && scene.BackdropAbsence == 1 && patch.Opacity == 0 && laughs == 0,
                "The short absence phase did not hide only the original Gengar before flight.");
            Render(content, Path.Combine(output, "gengar-vanished.png"));
            var vanished = Capture(backdrop);
            CheckLocalGengarChange(before, vanished, backdrop, patch);
            scene.AdvanceForTest(.80);
            Check(scene.IsFlying && laughs == 1, "The approach did not produce exactly one laugh cue.");
            scene.AdvanceForTest(.50);
            Render(content, Path.Combine(output, "gengar-approach.png"));
            scene.AdvanceForTest(.50);
            var flightPixels = Capture(scene);
            int paintedPixels = 0;
            for (int i = 3; i < flightPixels.Length; i += 4)
                if (flightPixels[i] > 8) paintedPixels++;
            Check(paintedPixels > 5000, "The foreground Gengar flight produced no visible rendered artwork.");
            int sceneWidth = (int)Math.Ceiling(scene.ActualWidth);
            int sceneHeight = (int)Math.Ceiling(scene.ActualHeight);
            Check(Enumerable.Range(0, sceneWidth).All(x => flightPixels[x * 4 + 3] == 0
                && flightPixels[((sceneHeight - 1) * sceneWidth + x) * 4 + 3] == 0),
                "The approaching Gengar silhouette was clipped by the top or bottom window edge.");
            scene.AdvanceForTest(2);
            Check(!scene.IsEncounterActive && !scene.IsFlying && scene.BackdropAbsence == 0 && patch.Opacity == 1 && laughs == 1,
                "Gengar encounter did not restore its original scene or repeated the laugh.");
            Render(content, Path.Combine(output, "gengar-restored.png"));
            Check(Capture(backdrop).SequenceEqual(before), "The completed encounter did not restore the exact original backdrop.");

            // Drive the real production timer into a near-due state, then let WPF tick naturally.
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: true);
            typeof(GengarScene).GetField("_secondsUntilNext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(scene, .15);
            PumpFor(TimeSpan.FromSeconds(1.2));
            Check(scene.IsEncounterActive, "The real idle timer did not start an encounter.");
            PumpFor(TimeSpan.FromSeconds(4.4));
            Check(!scene.IsEncounterActive && scene.IsAnimating && patch.Opacity == 1,
                "The real timer did not restore the scene and return to its idle schedule.");
            var timer = (DispatcherTimer)typeof(GengarScene).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
            Check(timer.IsEnabled && Math.Abs(timer.Interval.TotalSeconds - 1) < .01,
                "Gengar did not return to the low-frequency idle timer.");
            double delay = (double)typeof(GengarScene).GetField("_secondsUntilNext", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
            Check(delay >= 178 && delay <= 360, "The next encounter was not scheduled several minutes later.");
            Check(laughs == 2, "The real timer did not request exactly one laugh for its encounter.");

            void BeginAbsent()
            {
                scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: true);
                scene.TriggerFlyByForTest(); scene.AdvanceForTest(.85);
                Check(patch.Opacity == 0, "Test encounter did not hide the independent Gengar layer.");
            }
            void CheckRestored(string reason) => Check(!scene.IsAnimating && !scene.IsEncounterActive && !scene.IsFlying
                && scene.BackdropAbsence == 0 && patch.Opacity == 1, reason);
            BeginAbsent();
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: true, active: true);
            CheckRestored("Reduced motion did not stop Gengar and restore the original backdrop.");
            BeginAbsent();
            scene.Configure("winter", decorationsEnabled: true, reduceMotion: false, active: true);
            CheckRestored("Theme change did not stop Gengar and restore the original backdrop.");
            BeginAbsent();
            scene.Configure("halloween", decorationsEnabled: false, reduceMotion: false, active: true);
            CheckRestored("Disabling decorations did not restore Gengar.");
            BeginAbsent();
            scene.Configure("halloween", decorationsEnabled: true, reduceMotion: false, active: false);
            CheckRestored("Inactive-window gate did not stop Gengar and restore the original backdrop.");
            BeginAbsent();
            window.WindowState = WindowState.Minimized; Pump();
            CheckRestored("Minimized launcher kept the encounter running or left Gengar invisible.");
            window.WindowState = WindowState.Normal; window.Activate(); Pump();
            BeginAbsent();
            window.Hide(); Pump();
            CheckRestored("Hidden launcher kept the encounter running or left Gengar invisible.");
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
                localCleanPlateOnly = true,
                exactBackdropRestoration = true,
                smoothDisappearance = true,
                severalMinuteCooldown = true,
                minimizedStops = true,
                screenshots = new[] { "gengar-before.png", "gengar-vanished.png", "gengar-approach.png", "gengar-restored.png" }
            }));
            Console.WriteLine("GENGAR_UI_OK full-window, click-through, smooth disappearance, local clean plate, approach, exact restoration, cooldown, one laugh, settings, theme, inactive, minimized, hidden, stale audio callback");
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
    private static byte[] Capture(FrameworkElement view)
    {
        view.UpdateLayout(); Pump();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }
    private static void CheckLocalGengarChange(byte[] before, byte[] after, AtmosphericBackdrop backdrop, Image gengar)
    {
        int width = (int)Math.Ceiling(backdrop.ActualWidth);
        int changed = 0;
        var allowed = gengar.TransformToAncestor(backdrop).TransformBounds(new Rect(0, 0, gengar.ActualWidth, gengar.ActualHeight));
        allowed.Inflate(2, 2);
        for (int i = 0; i < before.Length; i += 4)
        {
            if (before.AsSpan(i, 4).SequenceEqual(after.AsSpan(i, 4))) continue;
            int pixel = i / 4; int x = pixel % width; int y = pixel / width;
            Check(allowed.Contains(new Point(x, y)), $"Hiding Gengar altered pixels outside its visual bounds at {x},{y}.");
            changed++;
        }
        Check(changed > 500, "The independent Gengar layer did not visibly disappear.");
    }
    private static void Render(FrameworkElement view, string path)
    {
        view.UpdateLayout(); Pump();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
}
