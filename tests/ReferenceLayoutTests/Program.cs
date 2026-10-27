using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/reference-layout");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        try
        {
            window.Show(); Pump();
            window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            var atmosphere = (AtmosphericBackdrop)window.FindName("AtmosphereView");
            var landscape = Field<Image>(atmosphere, "_landscape");
            var far = Field<Image>(atmosphere, "_forest");
            var foreground = Field<Image>(atmosphere, "_foreground");
            var near = (TranslateTransform)foreground.RenderTransform;
            if (args.Contains("--gallery"))
            {
                var compositions = new List<object>();
                var settings = Field<LauncherSettings>(window, "_settings");
                ((CheckBox)window.FindName("ReduceMotionCheckBox")).IsChecked = true;
                foreach (var (w, h) in new[] { (960, 640), (1280, 800), (1920, 1080) })
                {
                    window.Width = w; window.Height = h; Pump();
                    foreach (string theme in ThemeCalendar.ThemeIds)
                    {
                        settings.ThemeArtworkSelections.Clear();
                        Invoke(window, "PreviewTheme", theme, false); Pump(); window.UpdateLayout();
                        Render((FrameworkElement)window.Content, Path.Combine(output, $"{theme}-{w}.png"));
                        foreach (bool day in new[] { false, true })
                        {
                            Invoke(window, "PreviewTheme", theme, day); Pump(); window.UpdateLayout();
                            var rows = (object[])typeof(MainWindow).GetMethod("ExportSceneComposition", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)!;
                            compositions.Add(new { width = w, height = h, daylight = day, actors = rows });
                        }
                    }
                }
                File.WriteAllText(Path.Combine(output, "composition.json"), System.Text.Json.JsonSerializer.Serialize(compositions, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("GALLERY_CAPTURED (visual inspection and composition export; no test assertions)");
                return 0;
            }
            foreach (var (width, height) in new[] { (960, 640), (1280, 800), (1920, 640), (1920, 1080) })
            {
                window.Width = width; window.Height = height; Pump(); window.UpdateLayout();
                foreach (string theme in ThemeCalendar.ThemeIds)
                {
                    foreach (bool day in new[] { false, true })
                    {
                        Invoke(window, "PreviewTheme", theme, day); Pump(); window.UpdateLayout();
                        Invoke(window, "VerifyVisibleControls");
                        Check(((BitmapImage)landscape.Source).UriSource.ToString().Contains(theme == "winter" ? "anime/winter-foreground" : theme + "-terrain"), "Wrong terrain/middle plane");
                        Check(((BitmapImage)far.Source).UriSource.ToString().Contains(theme switch
                        {
                            "winter" => "inhabited/winter-landscape",
                            "halloween" => "halloween-forest",
                            _ => theme + "-far"
                        }), "Wrong far plane");
                        Check(((BitmapImage)foreground.Source).UriSource.ToString().Contains(theme + (theme == "winter" ? "-foreground" : "-near")), "Wrong foreground");
                        if (theme != "halloween")
                            Check(new[] { far.RenderTransform, landscape.RenderTransform, foreground.RenderTransform }
                                .Distinct(ReferenceEqualityComparer.Instance).Count() == 3, "Seasonal theme has fewer than three independent parallax planes: " + theme);
                        if (theme == "winter")
                        {
                            Check(landscape.Clip is RectangleGeometry middle && middle.Rect.Bottom <= landscape.ActualHeight * 390d / 941 + .001,
                                "Winter middle sheet must clip to upper branches");
                            Check(foreground.Clip is RectangleGeometry winterNearClip && winterNearClip.Rect.Top >= foreground.ActualHeight * 390d / 941 - .001,
                                "Winter near sheet must clip to lower bushes");
                        }
                        Check(!atmosphere.IsHitTestVisible, "Background intercepts clicks");
                        VerifyAlpha((BitmapSource)foreground.Source);
                        if (width == 1280 && !day) Render((FrameworkElement)window.Content, Path.Combine(output, theme + ".png"));
                    }
                }
                Invoke(window, "PreviewTheme", "halloween", false); Pump();
                Render((FrameworkElement)window.Content, Path.Combine(output, $"layout-{width}x{height}.png"));
                Invoke(window, "PreviewDownload"); Pump(); Invoke(window, "VerifyVisibleControls");
                Check(((Button)window.FindName("CancelButton")).IsVisible, "Download cancellation hidden");
                Invoke(window, "EndDownloadPreview");
            }
            // Check the four maximum camera extrema at every supported viewport.
            var logo = (FrameworkElement)window.FindName("LogoFrame");
            var start = (FrameworkElement)window.FindName("StartGameButton");
            ((CheckBox)window.FindName("ReduceMotionCheckBox")).IsChecked = true; Pump();
            // Isolate synthetic corner input from the real desktop mouse; a moving
            // scene under the physical pointer can legitimately produce MouseMove.
            window.IsHitTestVisible = false; Pump();
            foreach (var (width, height) in new[] { (960, 640), (1280, 800), (1920, 640), (1920, 1080) })
            {
                window.Width = width; window.Height = height; Pump(); window.UpdateLayout();
                Invoke(window, "PreviewTheme", "halloween", false); Pump(); window.UpdateLayout();
                atmosphere.Configure("halloween", false, true, false);
                var terrain = (TranslateTransform)landscape.RenderTransform;
                var logoOrigin = logo.TranslatePoint(new Point(), window);
                var buttonOrigin = start.TranslatePoint(new Point(), window);
                var corners = new[]
                {
                    (Point: new Point(0, 0), X: 1d, Y: 1d),
                    (Point: new Point(atmosphere.ActualWidth, 0), X: -1d, Y: 1d),
                    (Point: new Point(0, atmosphere.ActualHeight), X: 1d, Y: -1d),
                    (Point: new Point(atmosphere.ActualWidth, atmosphere.ActualHeight), X: -1d, Y: -1d)
                };
                for (int i = 0; i < corners.Length; i++)
                {
                    var corner = corners[i];
                    atmosphere.SetPointer(corner.Point);
                    WaitUntil(() => !Field<DispatcherTimer>(atmosphere, "_camera").IsEnabled, "Camera did not settle");
                    Check(Math.Abs(terrain.X - corner.X * 15) < .01 && Math.Abs(terrain.Y - corner.Y * 8.5) < .01 &&
                          Math.Abs(near.X - corner.X * 24) < .01 && Math.Abs(near.Y - corner.Y * 14) < .01,
                        "Parallax did not reach the expected corner offsets");
                    VerifyLayerCoverage(landscape, atmosphere, new Rect(0, 0, atmosphere.ActualWidth, atmosphere.ActualHeight), $"landscape {width}x{height}, corner {i}");
                    VerifyLayerCoverage(foreground, atmosphere, new Rect(0, 0, atmosphere.ActualWidth, atmosphere.ActualHeight), $"foreground {width}x{height}, corner {i}");
                    Check(logo.TranslatePoint(new Point(), window) == logoOrigin && start.TranslatePoint(new Point(), window) == buttonOrigin, "Camera moves interface");
                    if (width == 1920 && height == 1080 && i == corners.Length - 1)
                        Render((FrameworkElement)window.Content, Path.Combine(output, "parallax-corner.png"));
                }
                atmosphere.ResetPointer();
                WaitUntil(() => !Field<DispatcherTimer>(atmosphere, "_camera").IsEnabled, "Reset never settles");
            }
            window.Width = 1280; window.Height = 800; Pump(); window.UpdateLayout();
            Invoke(window, "PreviewTheme", "halloween", false); Pump();
            atmosphere.ResetPointer();
            WaitUntil(() => !Field<DispatcherTimer>(atmosphere, "_camera").IsEnabled, "Reset never settles");
            Check(near.X == 0 && near.Y == 0, "Mouse leave did not recenter");
            foreach (var (active, reduce) in new[] { (false, false), (true, true) })
            {
                atmosphere.Configure("halloween", false, active, reduce);
                atmosphere.SetPointer(new Point(0, 0)); Pump();
                Check(near.X == 0 && !Field<DispatcherTimer>(atmosphere, "_camera").IsEnabled, "Inactive/reduced camera runs");
            }
            atmosphere.Configure("halloween", false, true, false);
            atmosphere.SetPointer(new Point(0, 0));
            window.Hide(); Pump();
            Check(!Field<DispatcherTimer>(atmosphere, "_camera").IsEnabled && near.X == 0, "Hidden window camera runs");
            window.IsHitTestVisible = true;
            window.Show(); Pump();
            VerifyMenus(window, output);
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"success\":true,\"themes\":8,\"lightingStates\":2,\"sizes\":4,\"alphaLayers\":true,\"parallax\":true,\"edgeCoverage\":true,\"fixedUI\":true,\"motionGates\":true,\"inWindowMenus\":true,\"directMenuSwitching\":true,\"downloadCancel\":true}");
            Console.WriteLine("REFERENCE_LAYOUT_OK 8 themes x 2 lighting states x 4 sizes; parallax, edge coverage, stationary UI, in-window menus, direct switching, scroll, download cancel");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            var props = (EventPropsScene)window.FindName("EventPropsView");
            foreach (var item in props.GetPlacementBounds()) Console.Error.WriteLine(item);
            var hero = (FrameworkElement)window.FindName("HeroPanel");
            Console.Error.WriteLine("Hero " + hero.TransformToVisual(props).TransformBounds(new Rect(hero.RenderSize)));
            Render((FrameworkElement)window.Content, Path.Combine(output, "failure.png"));
            return 1;
        }
        finally { window.Close(); app.Shutdown(); }
    }

    private static void VerifyMenus(MainWindow window, string output)
    {
        var panel = (FrameworkElement)window.FindName("NavigationPanel");
        var navigation = new Dictionary<string, string>
        {
            ["Changelog"] = "PatchNotesHost",
            ["Extras"] = "ExtrasHost",
            ["Einstellungen"] = "SettingsHost"
        };
        var sizes = new[] { (960, 640), (1280, 800), (1920, 640), (1920, 1080) };
        foreach (var (width, height) in sizes)
        {
            window.Width = width; window.Height = height; Pump(); window.UpdateLayout();
            foreach (var (page, hostName) in navigation)
            {
                string buttonName = page switch { "Changelog" => "OpenPatchNotesButton", "Extras" => "ExtrasNavButton", _ => "SettingsNavButton" };
                ((Button)window.FindName(buttonName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(); window.UpdateLayout();
                var host = (Border)window.FindName(hostName);
                Check(panel.IsVisible && host.IsVisible && Window.GetWindow(host) == window, $"{page} not in main window");
                Check(!((FrameworkElement)window.FindName("StageArtArea")).IsHitTestVisible, "Scene remains clickable behind menu");
                var panelBounds = panel.TransformToAncestor(window).TransformBounds(new Rect(panel.RenderSize));
                Check(panelBounds.Left >= -1 && panelBounds.Right <= window.ActualWidth + 1 && panel.ActualWidth <= 1041,
                    $"Menu width/placement is wrong at {width}x{height}: {panelBounds}");
                var content = (FrameworkElement)window.FindName(page == "Changelog" ? "ScenePanel" : page == "Extras" ? "ExtrasPanel" : "SettingsPanel");
                foreach (FrameworkElement control in Descendants(content).OfType<FrameworkElement>().Where(c => c is Button or CheckBox or Slider))
                {
                    if (!control.IsVisible) continue;
                    var bounds = control.TransformToAncestor(panel).TransformBounds(new Rect(control.RenderSize));
                    Check(bounds.Left >= -1 && bounds.Right <= panel.ActualWidth + 1, $"{page} control clipped: {control.Name}");
                }
                var scroll = Descendants(host).OfType<ScrollViewer>().FirstOrDefault();
                if (scroll is not null && scroll.ScrollableHeight > 0)
                {
                    scroll.ScrollToEnd(); Pump();
                    Check(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1, $"Cannot scroll to end of {page}");
                    scroll.ScrollToTop(); Pump();
                }
                Render((FrameworkElement)window.Content, Path.Combine(output, $"menu-{page.ToLowerInvariant()}-{width}x{height}.png"));
            }
        }
        Invoke(window, "PreviewDownload"); Pump();
        ((Button)window.FindName("ExtrasNavButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); window.UpdateLayout();
        var menuCancel = (Button)window.FindName("MenuCancelButton");
        Check(menuCancel.IsVisible && menuCancel.IsEnabled, "Menu download cancellation is hidden or disabled");
        menuCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(Field<System.Threading.CancellationTokenSource?>(window, "_operationCts")?.IsCancellationRequested == true,
            "Menu cancel did not request cancellation");
        Invoke(window, "EndDownloadPreview");
        ((Button)window.FindName("HomeNavButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); window.UpdateLayout();
        Check(!panel.IsVisible && ((FrameworkElement)window.FindName("StageArtArea")).IsHitTestVisible, "Home did not restore scene input");
    }

    private static void VerifyAlpha(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(source.PixelWidth / 2, source.PixelHeight / 2, 1, 1), pixel, 4, 0);
        Check(pixel[3] == 0, "Foreground center is not truly transparent");
    }

    private static void VerifyLayerCoverage(Image image, FrameworkElement viewport, Rect required, string label)
    {
        var imageRect = new Rect(image.RenderSize);
        VerifyImageClip(LayoutInformation.GetLayoutClip(image), imageRect, label, "automatic layout clip");
        VerifyImageClip(VisualTreeHelper.GetClip(image), imageRect, label, "explicit visual clip");
        Rect bounds = image.TransformToVisual(viewport).TransformBounds(imageRect);
        const double tolerance = .5;
        Check(bounds.Left <= required.Left + tolerance && bounds.Top <= required.Top + tolerance &&
              bounds.Right >= required.Right - tolerance && bounds.Bottom >= required.Bottom - tolerance,
            $"{label}: rendered bitmap bounds {bounds} do not cover {required}");
    }

    private static void VerifyImageClip(Geometry? clip, Rect imageRect, string label, string clipKind)
    {
        if (clip is not null && !clip.FillContains(new RectangleGeometry(imageRect)))
            throw new InvalidOperationException($"{label}: {clipKind} {clip.Bounds} crops image bounds {imageRect}");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void WaitUntil(Func<bool> condition, string message)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed.TotalSeconds < 4)
        {
            var frame = new DispatcherFrame();
            var tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            tick.Tick += (_, _) => { tick.Stop(); frame.Continue = false; }; tick.Start(); Dispatcher.PushFrame(frame);
        }
        Check(condition(), message);
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Invoke(MainWindow window, string method, params object[] args) => typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }
}
