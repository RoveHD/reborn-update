using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokemonRebornLauncher;

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
            var host = (Border)window.FindName("PatchNotesHost");
            var notes = (TextBlock)window.FindName("PatchNotesText");
            var scroll = (ScrollViewer)window.FindName("PatchNotesScroller");
            var open = (Button)window.FindName("OpenPatchNotesButton");
            Check(!host.IsVisible, "Notes still take space on the home screen");
            notes.Text = string.Join("\n\n", Enumerable.Repeat("Neue Pokémon, überarbeitete Menüs und Fehlerkorrekturen. Deine Spielstände bleiben erhalten.", 30));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Exception? error = null;
                int index = attempt;
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    Window? dialog = null;
                    try
                    {
                        dialog = window.OwnedWindows.Cast<Window>().Single();
                        if (index == 1) { dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight; }
                        dialog.UpdateLayout();
                        Check(host.IsVisible && Window.GetWindow(host) == dialog, "Notes not in owned dialog");
                        Check(scroll.ScrollableHeight > 100, "Long notes cannot scroll");
                        scroll.ScrollToEnd(); Pump();
                        Check(scroll.VerticalOffset > 100, "Cannot reach end of notes");
                        notes.Text = "Offline-Modus: Versionshinweise sind gerade nicht verfügbar.";
                        dialog.UpdateLayout();
                        Check(scroll.ScrollableHeight < 1, "Notes update is stale");
                        if (index == 0) Render((FrameworkElement)dialog.Content, Path.Combine(output, "changelog-offline.png"));
                        notes.Text = string.Join("\n\n", Enumerable.Repeat("Neue Pokémon und überarbeitete Menüs. Die bisherige Spielversion und deine Spielstände bleiben erhalten.", 30));
                        dialog.UpdateLayout(); scroll.ScrollToTop(); Pump();
                        Render((FrameworkElement)dialog.Content, Path.Combine(output, index == 0 ? "changelog.png" : "changelog-compact.png"));
                        ((Button)window.FindName("RefreshPatchNotesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var close = ((Grid)((Border)dialog.Content).Child).Children.OfType<Button>().Single();
                        Check(close.IsCancel, "Escape is not wired to close the dialog");
                        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception ex) { error = ex; }
                    finally { dialog?.Close(); }
                }));
                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (error is not null) throw error;
                Check(!host.IsVisible && Window.GetWindow(host) == window, "Dialog did not restore hidden host");
                Check(window.IsEnabled, "Main window remains disabled after closing notes");
            }
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"success\":true,\"sizes\":4,\"themes\":8,\"dialogReopen\":true,\"liveNotes\":true,\"scroll\":true,\"refresh\":true}");
            Console.WriteLine("CENTERED_LAYOUT_OK 8 themes x 4 sizes; dialog open/close/reopen, live notes, long scroll, refresh");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
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
