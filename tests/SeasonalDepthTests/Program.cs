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
    [STAThread] private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/seasonal-depth");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        int cases = 0;
        try
        {
            window.Show(); Pump(); window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            ((CheckBox)window.FindName("ReduceMotionCheckBox")).IsChecked = true; Pump();
            // Synthetic camera extrema must not receive the desktop pointer's
            // MouseMove events while the test resizes or switches the scene.
            window.IsHitTestVisible = false; Pump();
            var root = (FrameworkElement)window.Content;
            var backdrop = (AtmosphericBackdrop)window.FindName("AtmosphereView");
            var props = (FrameworkElement)window.FindName("EventPropsView");
            var nearHost = (Grid)window.FindName("NearSceneLayer");
            var allLayers = new[] { "_forest", "_landscape", "_foreground" }.Select(n => Field<Image>(backdrop, n)).ToArray();
            var controls = new[] { "HomeNavButton", "LogoFrame", "StartGameButton", "FullscreenButton" }
                .Select(n => (FrameworkElement)window.FindName(n)).ToArray();
            var themes = ThemeCalendar.ThemeIds.Where(t => t != "halloween").ToArray();
            var checkedAlpha = new HashSet<string>();
            foreach (var (width, height) in new[] { (1280, 800), (960, 640), (1920, 1080), (1920, 640), (960, 900) })
            {
                window.Width = width; window.Height = height; Pump();
                foreach (string theme in themes)
                {
                    Invoke(window, "PreviewTheme", theme, false); Pump();
                    backdrop.Configure(theme, false, true, true); Pump();
                    var layers = allLayers;
                    Check(Field<Image>(backdrop, "_castle").Source is null && Field<Image>(backdrop, "_gengar").Source is null,
                        "Halloween artwork leaked into " + theme);
                    Check(backdrop.GengarPointIn(root) is null && backdrop.CastlePointIn(root, new Point(.5, .5)) is null,
                        "Halloween character/castle API is active in " + theme);
                    Check(nearHost.Clip is null && allLayers[2].OpacityMask is null, "Seasonal near frame is clipped or faded: " + theme);
                    string[] suffixes = ["far", "terrain", "near"];
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var source = (BitmapImage)layers[i].Source;
                        string expectedResource = theme == "winter"
                            ? i == 0 ? "scenery/inhabited/winter-landscape.png" : "scenery/anime/winter-foreground.png"
                            : $"scenery/parallax/{theme}-{suffixes[i]}.png";
                        Check(source.UriSource.ToString().Contains(expectedResource), "Wrong seasonal plane: " + theme + "/" + suffixes[i]);
                        // Preserve the original tool exports: only these two
                        // terrain PNGs are one pixel narrower. The renderer maps
                        // their normalized coordinates onto the shared 1672 frame.
                        int expectedWidth = suffixes[i] == "terrain" && theme is "harvest" or "newyear" ? 1671 : 1672;
                        Check(source.PixelWidth == expectedWidth && source.PixelHeight == 941,
                            "Unexpected authored source frame: " + theme + "-" + suffixes[i]);
                        Check(layers[i].Width == layers[0].Width && layers[i].Height == layers[0].Height
                            && Canvas.GetLeft(layers[i]) == Canvas.GetLeft(layers[0]) && Canvas.GetTop(layers[i]) == Canvas.GetTop(layers[0]),
                            "Rendered registration mismatch: " + theme);
                        if (checkedAlpha.Add(theme + suffixes[i])) VerifyAlpha(source, i == 0, theme + "-" + suffixes[i]);
                    }
                    if (theme == "winter")
                    {
                        BitmapSource winterFrame = (BitmapSource)allLayers[1].Source;
                        Check(!HasVisibleAlphaRows(winterFrame, 363, 424),
                            "Winter cut between branch and foreground sheets crosses visible artwork");
                        double split = allLayers[1].ActualHeight * 390d / 941;
                        Check(allLayers[1].Clip is RectangleGeometry middle && Math.Abs(middle.Rect.X) < .001
                            && Math.Abs(middle.Rect.Y) < .001 && Math.Abs(middle.Rect.Width - allLayers[1].ActualWidth) < .001
                            && Math.Abs(middle.Rect.Height - split) < .001,
                            "Winter middle plane must show only the upper branches");
                        Check(allLayers[2].Clip is RectangleGeometry winterNearClip && Math.Abs(winterNearClip.Rect.X) < .001
                            && Math.Abs(winterNearClip.Rect.Y - split) < .001 && Math.Abs(winterNearClip.Rect.Width - allLayers[2].ActualWidth) < .001
                            && Math.Abs(winterNearClip.Rect.Height - (allLayers[2].ActualHeight - split)) < .001,
                            "Winter near plane must show only the lower bushes");
                    }
                    var fixedBounds = controls.Select(c => Bounds(c, root)).ToArray();
                    foreach (var camera in new[] { new Point(), new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1) })
                    {
                        SetCamera(backdrop, camera); Pump();
                        double[] travelX = [4, 15, 24];
                        double[] travelY = [2.5, 8.5, 14];
                        if (theme == "summer")
                        {
                            foreach (var waterline in new[] { new Point(.72, .69), new Point(.86, .64) })
                            {
                                Check(AlphaAt((BitmapSource)layers[1].Source, waterline.X, waterline.Y) > 160,
                                    "Summer waterline art is missing from the terrain plane");
                                Point local = new(waterline.X * layers[1].ActualWidth, waterline.Y * layers[1].ActualHeight);
                                Point waterlinePoint = layers[1].TransformToVisual(root).Transform(local);
                                Point terrainPoint = backdrop.TerrainPointIn(root, waterline)!.Value;
                                Check((waterlinePoint - terrainPoint).Length < .0001, "Summer waterline contact is not projected from the terrain plane");
                            }
                        }
                        Check(layers.Select(layer => layer.RenderTransform).Distinct(ReferenceEqualityComparer.Instance).Count() == 3,
                            "Seasonal far, terrain, and near planes must have independent transforms: " + theme);
                        for (int i = 0; i < layers.Length; i++)
                        {
                            var transform = (TranslateTransform)layers[i].RenderTransform;
                            Check(Math.Abs(transform.X + camera.X * travelX[i]) < .0001
                                && Math.Abs(transform.Y + camera.Y * travelY[i]) < .0001,
                                $"Independent camera travel missing: {theme}, layer {i}, camera {camera}, actual ({transform.X},{transform.Y}), expected ({-camera.X * travelX[i]},{-camera.Y * travelY[i]})");
                            Rect layerBounds = Bounds(layers[i], backdrop);
                            // Cover math can leave a 1e-13 DIP rounding residual
                            // at an exact camera limit; allow only numeric noise.
                            Rect coverageBounds = layerBounds;
                            coverageBounds.Inflate(.000001, .000001);
                            Check(coverageBounds.Contains(new Rect(backdrop.RenderSize)),
                                $"Camera exposes a source edge: {theme}, {width}x{height}, layer {i}, camera {camera}, bounds {layerBounds}, viewport {backdrop.RenderSize}");
                        }
                        Point sourcePoint = new(.2, .9);
                        Image terrainPlane = theme == "winter" ? allLayers[0] : allLayers[1];
                        Point expected = terrainPlane.TransformToVisual(props).Transform(new Point(terrainPlane.ActualWidth * sourcePoint.X, terrainPlane.ActualHeight * sourcePoint.Y));
                        Check(backdrop.TerrainPointIn(props, sourcePoint) is Point actual && (actual - expected).Length < .001,
                            "Terrain attachment does not follow the registered source plane: " + theme);
                        for (int i = 0; i < controls.Length; i++)
                            Check(Bounds(controls[i], root) == fixedBounds[i], "Camera moves fixed control: " + controls[i].Name);
                        if (width == 1280 && (theme == "summer" || theme == "winter")
                            && (camera == new Point(-1, -1) || camera == new Point(1, 1)))
                            Render(root, Path.Combine(output, $"{theme}-camera-{camera.X}-{camera.Y}.png"));
                        cases++;
                    }
                    if (width == 1280)
                    {
                        SetCamera(backdrop, new Point()); Pump();
                        Render(root, Path.Combine(output, theme + ".png"));
                    }
                    backdrop.Configure(theme, false, true, true); Pump();
                    Check(layers.All(l => ((TranslateTransform)l.RenderTransform).X == 0 && ((TranslateTransform)l.RenderTransform).Y == 0),
                        "Reduced motion did not reset seasonal planes: " + theme);
                    Check(!Field<DispatcherTimer>(backdrop, "_camera").IsEnabled, "Reduced motion leaves camera running: " + theme);
                }
            }
            // January uses a hanging icicle instead of December's festival set.
            // Its anchor belongs to the faster near branch, not the terrain.
            window.Width = 1280; window.Height = 800; Pump();
            Invoke(window, "PreviewTheme", "winter", false); Pump();
            typeof(MainWindow).GetField("_themeDay", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(window, new DateOnly(2027, 1, 15));
            Invoke(window, "ApplyEventPresentation"); Pump();
            var propScene = (EventPropsScene)props;
            var icicle = propScene.Props.Single(p => p.Kind == PropKind.Icicle);
            var branchAnchor = SceneDecorationLayout.SeasonalAnchorsForAspect("winter", backdrop.ActualWidth / backdrop.ActualHeight)
                .Single(a => SceneDecorationLayout.ForTheme("winter")[a.Index].Kind == PropKind.Icicle);
            foreach (var camera in new[] { new Point(), new Point(-1, -1), new Point(1, 1) })
            {
                SetCamera(backdrop, camera); Pump();
                Point branch = backdrop.ForegroundPointIn(props, new Point(branchAnchor.X, branchAnchor.Y))!.Value;
                var attached = Field<Dictionary<int, Point>>(propScene, "_terrainAnchors");
                Check(attached.TryGetValue(icicle.Id, out Point pin) && (pin - branch).Length < .001,
                    "Winter icicle drifts away from its near-plane branch");
                propScene.VerifyLayout();
            }
            // A seasonal round trip must restore all five original Halloween
            // planes with exactly the same backdrop/near pixels and transforms.
            window.Width = 1280; window.Height = 800; Pump();
            Invoke(window, "PreviewTheme", "halloween", false); Pump();
            backdrop.Configure("halloween", false, true, true); Pump();
            byte[] beforeFar = Pixels(Capture(backdrop)), beforeNear = Pixels(Capture(nearHost));
            foreach (string theme in themes) { Invoke(window, "PreviewTheme", theme, false); Pump(); }
            Invoke(window, "PreviewTheme", "halloween", false); Pump();
            backdrop.Configure("halloween", false, true, true); Pump();
            Check(beforeFar.SequenceEqual(Pixels(Capture(backdrop))) && beforeNear.SequenceEqual(Pixels(Capture(nearHost))),
                "Seasonal round trip changes Halloween scene pixels");
            Check(backdrop.TerrainPointIn(props, new Point(.2, .9)) is null, "Seasonal terrain API remains active in Halloween");
            Invoke(window, "PreviewTheme", "summer", false); Pump();
            backdrop.Configure("summer", false, true, false);
            backdrop.SetPointer(new Point(backdrop.ActualWidth, backdrop.ActualHeight));
            PumpFor(TimeSpan.FromMilliseconds(550));
            Check(((TranslateTransform)allLayers[2].RenderTransform).X < -15, "Real seasonal camera timer did not advance");
            backdrop.Configure("summer", false, false, false); Pump();
            Check(!Field<DispatcherTimer>(backdrop, "_camera").IsEnabled && allLayers.All(l => ((TranslateTransform)l.RenderTransform).X == 0),
                "Inactive scene keeps camera motion");
            File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new
            { success = true, themes = themes.Length, threeImagePlaneThemes = themes.Length, cameraViewportCases = cases,
                registeredAlphaPlanes = true, terrainProjection = true, winterIcicleNearAttachment = true, summerWaterlineTerrainAttachment = true,
                fixedInterface = true, reducedMotion = true, halloweenRoundTripPixels = true },
                new JsonSerializerOptions { WriteIndented = true }));
            File.Delete(Path.Combine(output, "failure.txt"));
            Console.WriteLine($"SEASONAL_DEPTH_OK: {themes.Length} themes, {cases} camera/viewport cases, {themes.Length} triple-plane scenes, terrain projection, Halloween round-trip pixels");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
    }
    private static void VerifyAlpha(BitmapSource source, bool opaque, string label)
    {
        byte[] pixels = Pixels(source);
        int transparent = 0, solid = 0;
        for (int i = 3; i < pixels.Length; i += 4) { if (pixels[i] < 8) transparent++; if (pixels[i] > 240) solid++; }
        int count = pixels.Length / 4;
        Check(opaque ? solid == count : transparent > count / 10 && solid > count / 100,
            "Plane is not " + (opaque ? "opaque: " : "authored transparent artwork: ") + label);
    }
    private static byte[] Pixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0); return pixels;
    }
    private static byte AlphaAt(BitmapSource source, double x, double y)
    {
        var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4];
        image.CopyPixels(new Int32Rect((int)(x * image.PixelWidth), (int)(y * image.PixelHeight), 1, 1), pixel, 4, 0);
        return pixel[3];
    }
    private static bool HasVisibleAlphaRows(BitmapSource source, int firstRow, int endRow)
    {
        var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = image.PixelWidth * 4;
        byte[] pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        for (int y = firstRow; y < endRow; y++)
        for (int x = 0; x < image.PixelWidth; x++)
            if (pixels[y * stride + x * 4 + 3] >= 8) return true;
        return false;
    }
    private static RenderTargetBitmap Capture(FrameworkElement view)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); return bitmap;
    }
    private static void Render(FrameworkElement view, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Capture(view)));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static void SetCamera(AtmosphericBackdrop backdrop, Point point)
    { typeof(AtmosphericBackdrop).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(backdrop, point); Invoke(backdrop, "ApplyCamera"); }
    private static Rect Bounds(FrameworkElement element, Visual root) => element.TransformToVisual(root).TransformBounds(new Rect(element.RenderSize));
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) => frame.Continue = false; timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
    }
}
