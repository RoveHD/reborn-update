using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokemonRebornLauncher;
using PokemonRebornLauncher.UI;
using PokemonRebornLauncher.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/centered-dialog");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        try
        {
            window.Show(); Pump();
            Invoke(window, "PreviewTheme", "halloween", false);
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            if (!args.Contains("--menus-only"))
            foreach (var (width, height) in new[] { (960, 640), (1280, 800), (1920, 640), (1920, 1080) })
            {
                window.Width = width; window.Height = height; Pump(); window.UpdateLayout();
                foreach (string theme in PokemonRebornLauncher.Services.ThemeCalendar.ThemeIds)
                {
                    Invoke(window, "PreviewTheme", theme, false); Pump(); window.UpdateLayout();
                    Invoke(window, "VerifyVisibleControls");
                }
                Invoke(window, "PreviewTheme", "halloween", false); Pump(); window.UpdateLayout();
                Render((FrameworkElement)window.Content, Path.Combine(output, $"layout-{width}x{height}.png"));
            }
            VerifyPersistentProps(window, output);
            var host = (Border)window.FindName("PatchNotesHost");
            var notes = (TextBlock)window.FindName("PatchNotesText");
            var scroll = (ScrollViewer)window.FindName("PatchNotesScroller");
            var open = (Button)window.FindName("OpenPatchNotesButton");
            Check(!host.IsVisible, "Notes still take space on the home screen");
            notes.Text = string.Join("\n\n", Enumerable.Repeat("Neue Pokémon, überarbeitete Menüs und Fehlerkorrekturen. Deine Spielstände bleiben erhalten.", 30));
            foreach (var (width, height) in new[] { (960, 640), (1280, 800), (1920, 640), (960, 1000) })
            {
                window.Width = width; window.Height = height; Pump(); window.UpdateLayout();
                Click(window, "OpenPatchNotesButton");
                Check(host.IsVisible && Window.GetWindow(host) == window, "Notes must stay in the main window");
                Check(window.IsEnabled && window.OwnedWindows.Count == 0, "Navigation blocks the launcher");
                Check(scroll.ScrollableHeight > 100, "Long notes cannot scroll");
                scroll.ScrollToVerticalOffset(150); Pump();
                double offset = scroll.VerticalOffset;
                Click(window, "ExtrasNavButton");
                Check(!host.IsVisible && ((Border)window.FindName("ExtrasHost")).IsVisible, "Direct switch to Extras failed");
                Click(window, "SettingsNavButton");
                Check(((Border)window.FindName("SettingsHost")).IsVisible, "Direct switch to Settings failed");
                Click(window, "OpenPatchNotesButton");
                Check(Math.Abs(scroll.VerticalOffset - offset) < 1, "Tab switch loses notes scroll position");
                scroll.ScrollToEnd(); Pump();
                Check(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1, "Cannot reach end of notes");
                notes.Text = "Offline-Modus: Versionshinweise sind gerade nicht verfügbar.";
                window.UpdateLayout(); Pump();
                Check(scroll.ScrollableHeight < 1, "Notes update is stale");
                notes.Text = string.Join("\n\n", Enumerable.Repeat("Neue Pokémon und überarbeitete Menüs. Die bisherige Spielversion und deine Spielstände bleiben erhalten.", 30));
                window.UpdateLayout(); scroll.ScrollToTop(); Pump();
                Click(window, "RefreshPatchNotesButton");
                Render((FrameworkElement)window.Content, Path.Combine(output, $"changelog-{width}x{height}.png"));
                // Exercise the routed keyboard path rather than the private handler.
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape)
                    { RoutedEvent = Keyboard.KeyDownEvent };
                window.RaiseEvent(escape); Pump();
                Check(escape.Handled && !host.IsVisible && window.IsEnabled, "Escape does not return home");
                Check(((Button)window.FindName("HomeNavButton")).IsKeyboardFocused, "Escape loses keyboard focus");
                Click(window, "SettingsNavButton");
                Click(window, "HomeNavButton");
                Check(!((Border)window.FindName("SettingsHost")).IsVisible, "Home does not dismiss settings");
                if (!args.Contains("--menus-only")) Invoke(window, "VerifyVisibleControls");
            }
            // A nested settings dialog remains modal, owned by the main window,
            // and returns to the same Settings page without losing navigation.
            Click(window, "SettingsNavButton");
            Exception? nestedFailure = null;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                Window? dialog = null;
                try
                {
                    dialog = window.OwnedWindows.Cast<Window>().Single();
                    Check(dialog.Owner == window && dialog.Title == "Lebendige Jahreszeiten", "Wrong nested dialog owner");
                }
                catch (Exception ex) { nestedFailure = ex; }
                finally { dialog?.Close(); }
            }));
            ((Button)window.FindName("EventSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (nestedFailure is not null) throw nestedFailure;
            Check(window.IsEnabled && ((Border)window.FindName("SettingsHost")).IsVisible, "Nested dialog loses Settings page");
            Click(window, "HomeNavButton");
            Invoke(window, "PreviewDownload");
            Click(window, "ExtrasNavButton");
            var operation = (CancellationTokenSource)typeof(MainWindow).GetField("_operationCts", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Click(window, "MenuCancelButton");
            Check(operation.IsCancellationRequested && !((Button)window.FindName("MenuCancelButton")).IsEnabled,
                "Menu cancellation is disconnected from the active update");
            Invoke(window, "EndDownloadPreview");
            Click(window, "HomeNavButton");
            window.Width = 1280; window.Height = 800; Pump();
            ((StackPanel)window.FindName("ProgressPanel")).Visibility = Visibility.Collapsed;
            ((TextBlock)window.FindName("StatusText")).Text = "Bereit für dein nächstes Abenteuer.";
            ((TextBlock)window.FindName("TopStatusText")).Text = "Bereit";
            var releaseNotes = Path.GetFullPath("release-notes-7.3.6.md");
            if (File.Exists(releaseNotes))
                notes.Text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(releaseNotes), @"(?m)^#{1,6}\s+", "");
            foreach (var (button, screenshot) in new[] { ("OpenPatchNotesButton", "changelog-final"), ("ExtrasNavButton", "extras-final"), ("SettingsNavButton", "settings-final") })
            {
                Click(window, button);
                Render((FrameworkElement)window.Content, Path.Combine(output, screenshot + ".png"));
            }
            Click(window, "HomeNavButton");
            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { success = true, sizes = 4, homeSceneChecks = !args.Contains("--menus-only"), directNavigation = true, persistentProps = true, liveNotes = true, scrollPreserved = true, escape = true, nestedDialog = true, downloadCancel = true }));
            Console.WriteLine(args.Contains("--menus-only")
                ? "NAVIGATION_OK direct menu switching at 4 sizes, live notes, preserved scroll, Escape, nested dialog owner, download cancellation (home scene checks not requested)"
                : "CENTERED_LAYOUT_OK home across 8 themes; direct menu switching, live notes, preserved scroll, Escape, nested dialog owner, download cancellation");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
    }
    private static void VerifyPersistentProps(MainWindow window, string output)
    {
        var props = (EventPropsScene)window.FindName("EventPropsView");
        var stage = (FrameworkElement)window.FindName("StageArtArea");
        var root = (FrameworkElement)window.Content;
        ((CheckBox)window.FindName("ReduceMotionCheckBox")).IsChecked = true;
        int unloads = 0;
        props.Unloaded += (_, _) => unloads++;
        var cases = ThemeCalendar.ThemeIds.Select(theme => (theme, width: 1280, height: 800))
            .Concat(new[] { ("halloween", 960, 640), ("halloween", 1920, 640), ("halloween", 960, 1000) });
        foreach (var (theme, width, height) in cases)
        {
            Click(window, "HomeNavButton");
            window.Width = width; window.Height = height; Pump(); window.UpdateLayout();
            Invoke(window, "PreviewTheme", theme, false); Pump();
            props.Configure(theme, theme == "winter", true, true, 0, 1, true, false); Pump();
            var initial = props.Props.ToArray();
            var stageBounds = stage.TransformToVisual(root).TransformBounds(new Rect(stage.RenderSize));
            var groundBounds = props.GetPlacementBounds().ToDictionary(p => p.Id, p => p.Bounds);
            Check(groundBounds.Count >= (theme == "halloween" ? 6 : 1), "Props missing on home screen: " + theme);
            foreach (var button in new[] { "OpenPatchNotesButton", "ExtrasNavButton", "SettingsNavButton", "HomeNavButton" })
            {
                Click(window, button); window.UpdateLayout(); Pump();
                Check(stage.IsVisible && props.IsVisible && stage.RenderSize.Height > 0, "Navigation hides scenery props: " + button);
                Check(stage.TransformToVisual(root).TransformBounds(new Rect(stage.RenderSize)) == stageBounds,
                    "Navigation resizes or moves scenery: " + button);
                Check(props.Props.Count == initial.Length && initial.All(p => props.Props.Any(other => ReferenceEquals(p, other))),
                    "Navigation recreates or resets scenery props: " + button);
                foreach (var item in props.GetPlacementBounds())
                    Check(groundBounds.TryGetValue(item.Id, out var before) && before == item.Bounds, "Navigation relocates a prop: " + item.Kind);
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(props.ActualWidth), (int)Math.Ceiling(props.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(props);
                var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                int painted = 0; for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 32) painted++;
                Check(painted > 1000, "Prop scene is visible but paints no decorations behind " + button);
                Check(unloads == 0, "Navigation unloaded the permanent prop scene");
            }
        }
    }
    private static void Click(MainWindow window, string name)
    {
        var button = (Button)window.FindName(name);
        Check(button.IsVisible && button.IsEnabled, "Button unavailable: " + name);
        var p = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window);
        var hit = window.InputHitTest(p) as DependencyObject;
        while (hit is not null && hit != button)
            hit = VisualTreeHelper.GetParent(hit);
        Check(hit == button, "Button is obscured: " + name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump(); window.UpdateLayout();
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Invoke(MainWindow window, string method, params object[] args) => typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
