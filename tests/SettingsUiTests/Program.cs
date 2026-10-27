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
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/settings-ui");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        int cases = 0;
        try
        {
            window.Show(); Pump();
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            ((CheckBox)window.FindName("ReduceMotionCheckBox")).IsChecked = true;
            var panel = (Border)window.FindName("NavigationPanel");
            var host = (Border)window.FindName("SettingsHost");
            var lighting = (Button)window.FindName("LightingModeButton");
            foreach (string theme in ThemeCalendar.ThemeIds)
            foreach (bool daylight in new[] { false, true })
            foreach (var (width, height) in new[] { (960, 640), (1280, 900) })
            {
                window.Width = width; window.Height = height;
                Invoke(window, "PreviewTheme", theme, daylight); Click(window, "SettingsNavButton"); Pump(); window.UpdateLayout();
                Color expected = ((SolidColorBrush)window.FindResource("Surface")).Color;
                Color actual = ((SolidColorBrush)panel.Background).Color;
                Check(actual.R == expected.R && actual.G == expected.G && actual.B == expected.B, "Settings ignore theme " + theme);
                Check(!panel.Resources.Contains("Surface") && !panel.Resources.Contains("Line"), "Local palette overrides theme");
                foreach (var control in Descendants(host).OfType<Control>().Where(c => c is Button or CheckBox))
                    Check(control.FontSize == 16 && control.MinHeight >= 44, "Inconsistent settings typography or click size: " + control.Name);
                foreach (var button in Descendants(host).OfType<Button>())
                    Check(button.ActualWidth < 440, "Stretched settings button: " + button.Name);
                var scroll = Descendants(host).OfType<ScrollViewer>().First();
                scroll.ScrollToEnd(); Pump(); window.UpdateLayout();
                Check(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1, "Cannot reach settings end");
                var bounds = lighting.TransformToAncestor(window).TransformBounds(new Rect(lighting.RenderSize));
                Check(new Rect(window.RenderSize).Contains(bounds), "Lighting selection is clipped");
                Click(window, "LightingModeButton"); Pump();
                var menu = lighting.ContextMenu;
                Check(menu.IsOpen && menu.ActualWidth > 230, "Lighting popup is not open/readable");
                foreach (var item in menu.Items.OfType<MenuItem>())
                {
                    Check(item.FontSize == 16 && item.ActualHeight >= 44, "Small popup item");
                    Check(item.IsChecked == ((string)item.Tag == (daylight ? "day" : "night")), "Wrong selected lighting option");
                }
                Check(!Descendants(menu).OfType<FrameworkElement>().Any(e => e.Name.Contains("Gutter", StringComparison.OrdinalIgnoreCase)), "System gutter remains");
                if (width == 1280)
                {
                    string mode = daylight ? "day" : "night";
                    Render((FrameworkElement)window.Content, Path.Combine(output, $"settings-{theme}-{mode}.png"));
                    var bitmap = Render(menu, Path.Combine(output, $"lighting-{theme}-{mode}.png"));
                    var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                    int white = 0;
                    for (int y = 10; y < bitmap.PixelHeight - 10; y++)
                    // Only the old system gutter area; labels start at x=20.
                    for (int x = 3; x < Math.Min(14, bitmap.PixelWidth); x++)
                    {
                        int at = (y * bitmap.PixelWidth + x) * 4;
                        if (pixels[at] > 220 && pixels[at + 1] > 220 && pixels[at + 2] > 220) white++;
                    }
                    Check(white == 0, "White strip in popup rendering");
                }
                menu.IsOpen = false; Pump();
                cases++;
            }
            Invoke(window, "VerifyLightingControls");
            VerifyDialog(window, output);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { success = true, cases, themes = 8, lightingStates = 2, sizes = 2, themeColors = true, uniformTypography = true, noSystemGutter = true, lightingSelection = true, fixedDialogActions = true, acceptAndCancel = true }));
            Console.WriteLine($"SETTINGS_UI_OK {cases} theme/light/size cases, actual popup pixels, selection, dialog scrolling and accept/cancel");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
    }
    private static void VerifyDialog(MainWindow owner, string output)
    {
        Click(owner, "HomeNavButton"); Invoke(owner, "PreviewTheme", "halloween", false);
        var settings = new LauncherSettings { EventDecorationsEnabled = true, InteractivePropsEnabled = true };
        var type = typeof(MainWindow).Assembly.GetType("PokemonRebornLauncher.UI.EventSettingsDialog")!;
        Window Create() => (Window)Activator.CreateInstance(type, owner, settings)!;
        foreach (bool accept in new[] { false, true })
        {
            var dialog = Create();
            try
            {
                dialog.Show(); Pump();
                Check(dialog.Width >= 680, "Event settings too narrow");
                foreach (var control in Descendants(dialog).OfType<Control>().Where(c => c is Button or CheckBox))
                    Check(control.FontSize == 16 && control.MinHeight >= 44, "Dialog uses inconsistent settings style");
                var toggle = Descendants(dialog).OfType<CheckBox>().First(); toggle.IsChecked = false;
                foreach (double height in new[] { 540d, 850d })
                {
                    dialog.Height = height; Pump(); dialog.UpdateLayout();
                    var scroll = Descendants(dialog).OfType<ScrollViewer>().First(); scroll.ScrollToEnd(); Pump();
                    Check(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1, "Dialog body cannot scroll");
                    foreach (var button in Descendants(dialog).OfType<Button>().Where(b => b.Name.EndsWith("SettingsButton")))
                    {
                        var rect = button.TransformToAncestor(dialog).TransformBounds(new Rect(button.RenderSize));
                        Check(new Rect(dialog.RenderSize).Contains(rect), "Dialog actions clipped or scrolled away");
                    }
                    if (!accept) Render((FrameworkElement)dialog.Content, Path.Combine(output, $"events-halloween-{height}.png"));
                }
                Descendants(dialog).OfType<Button>().Single(b => b.Name == (accept ? "AcceptSettingsButton" : "CancelSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                type.GetMethod("ApplyTo")!.Invoke(dialog, [settings]);
                Check(settings.EventDecorationsEnabled == !accept, "Cancel/accept changed settings incorrectly");
            }
            finally { dialog.Close(); }
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
    private static void Click(MainWindow window, string name) { ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static void Invoke(MainWindow window, string method, params object[] args) => typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static RenderTargetBitmap Render(FrameworkElement view, string path)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file); return bitmap;
    }
}
