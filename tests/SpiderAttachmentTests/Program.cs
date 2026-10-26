using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using PokemonRebornLauncher;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "test-results/spider-attachment"); Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(smokeTest: true);
        var rows = new List<object>();
        try
        {
            window.Show(); Pump(); window.BeginAnimation(UIElement.OpacityProperty, null); window.Opacity = 1;
            var props = (EventPropsScene)window.FindName("EventPropsView");
            var discoveries = new List<HalloweenSecretDiscovery>(); props.SecretDiscovered += discoveries.Add;
            var backdrop = (AtmosphericBackdrop)window.FindName("AtmosphereView");
            var root = (FrameworkElement)window.Content;
            var foreground = Field<Image>(backdrop, "_foreground");
            var landscape = Field<Image>(backdrop, "_landscape");
            Type artworkType = typeof(EventPropsScene).Assembly.GetType("PokemonRebornLauncher.UI.HalloweenPropArtwork")!;
            var frozenArt = new Dictionary<PropKind, object>();
            foreach (var anchor in SceneDecorationLayout.HalloweenLandscapeAnchors)
            {
                object art = artworkType.GetMethod("For")!.Invoke(null, [anchor.Kind])!;
                var bitmap = (BitmapSource)artworkType.GetProperty("Image")!.GetValue(art)!;
                var bounds = (Rect)artworkType.GetProperty("Bounds")!.GetValue(art)!;
                Check(bitmap.IsFrozen && bitmap.PixelWidth > 16 && bitmap.PixelHeight > 16, "Missing/flexible painted bitmap");
                Check(Math.Abs(bounds.Width / bounds.Height - (double)bitmap.PixelWidth / bitmap.PixelHeight) < .00001, "Painted prop aspect distorted");
                frozenArt[anchor.Kind] = art;
            }
            foreach (var (width, height) in new[] { (960, 640), (1280, 800), (1920, 1080), (1920, 640), (960, 900) })
            {
                window.Width = width; window.Height = height; Pump();
                typeof(MainWindow).GetMethod("PreviewTheme", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, ["halloween", false]);
                props.Configure("halloween", false, true, true, 0, 1, false, false); Pump();
                foreach (var camera in new[] { new Point(), new Point(-1,-1), new Point(1,-1), new Point(-1,1), new Point(1,1) })
                {
                    typeof(AtmosphericBackdrop).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(backdrop, camera);
                    typeof(AtmosphericBackdrop).GetMethod("ApplyCamera", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(backdrop, null); Pump();
                    bool ordinaryAspect = width != 1920 || height != 640;
                    ordinaryAspect &= width != 960 || height != 900;
                    var maybeTether = props.GetSpiderTether();
                    Check(ordinaryAspect == maybeTether.HasValue, "Spider crop visibility mismatch");
                    Point expected = foreground.TransformToVisual(root).Transform(new Point(foreground.ActualWidth * 240 / 1671, foreground.ActualHeight * 224 / 941));
                    Point actual = expected;
                    if (maybeTether is { } tether)
                    {
                        actual = props.TransformToVisual(root).Transform(tether.Branch);
                        Check((actual - expected).Length < .01, "Thread detached from foreground branch");
                        var spider = props.Props.Single(p => p.Kind == PropKind.Spider);
                        Rect bounds = props.GetPlacementBounds().Single(p => p.Id == spider.Id).Bounds;
                        Check(new Rect(0,0,props.ActualWidth,props.ActualHeight).Contains(bounds), "Spider clipped");
                        var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                        Check(VisualTreeHelper.HitTest(props, center)?.VisualHit == props, "Spider hit target lost");
                        Check(VisualTreeHelper.HitTest(props, tether.Branch) is null, "Thread intercepts clicks");
                    }
                    else Check(props.GetPlacementBounds().All(p => p.Kind != PropKind.Spider), "Cropped spider retains hitbox");
                    try { props.VerifyLayout(); }
                    catch
                    {
                        Render(root, Path.Combine(output, $"layout-failure-{width}x{height}-{camera.X}-{camera.Y}.png"));
                        throw;
                    }
                    foreach (var anchor in SceneDecorationLayout.HalloweenAnchorsForAspect((double)width / height))
                    {
                        var actor = props.Props.First(p => p.Kind == anchor.Kind);
                        Rect actorBox = props.GetPlacementBounds().First(p => p.Id == actor.Id).Bounds;
                        Check(new Rect(0,0,props.ActualWidth,props.ActualHeight).Contains(actorBox), "Painted prop clipped: " + anchor.Kind);
                        Point actorCenter = new(actorBox.X + actorBox.Width / 2, actorBox.Y + actorBox.Height / 2);
                        Check(VisualTreeHelper.HitTest(props, actorCenter)?.VisualHit == props, "Painted prop hitbox missing: " + anchor.Kind);
                        Point expectedFoot = landscape.TransformToVisual(root).Transform(new Point(landscape.ActualWidth * anchor.X, landscape.ActualHeight * anchor.Y));
                        var actualCenter = (Point)typeof(EventPropsScene).GetMethod("RenderCenterDip",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(props,[actor])!;
                        if (anchor.Kind != PropKind.Ghost)
                        {
                            Rect artworkBounds = (Rect)artworkType.GetProperty("Bounds")!.GetValue(frozenArt[anchor.Kind])!;
                            double actorScale = (double)typeof(EventPropsScene).GetMethod("PropDipScale",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(props,[actor])!;
                            actualCenter.Y += artworkBounds.Bottom * actorScale;
                            Point actualFoot = props.TransformToVisual(root).Transform(actualCenter);
                            Check((actualFoot - expectedFoot).Length < .01, "Painted foot detached from landscape: " + anchor.Kind);
                        }
                    }
                    rows.Add(new { width, height, cameraX = camera.X, cameraY = camera.Y, branchX = actual.X, branchY = actual.Y, error = (actual-expected).Length, authoredSpiderCropped = !ordinaryAspect });
                    if (camera == new Point() || camera == new Point(1,1)) Render(root, Path.Combine(output, $"halloween-{width}x{height}-{camera.X}-{camera.Y}.png"));
                }
                if (props.GetSpiderTether() is { } idle)
                {
                    int id = props.Props.Single(p => p.Kind == PropKind.Spider).Id;
                    Check(props.ActivateForTest(id), "Spider interaction disabled"); props.AdvanceForTest(.2);
                    var dropped = props.GetSpiderTether()!.Value;
                    Check(dropped.Head.Y > idle.Head.Y + 10 && dropped.Branch == idle.Branch, "Spider drop moves branch or disconnects rope");
                }
                Render(root, Path.Combine(output, $"halloween-{width}x{height}-drop.png"));
                props.AdvanceForTest(2);
                props.Configure("halloween", false, true, true, 0, 1, true, false);
                backdrop.Configure("halloween", false, true, true); Pump();
                props.AdvanceForTest(.05);
                var reduced = props.GetSpiderTether(); props.AdvanceForTest(.2);
                Check(props.GetSpiderTether() == reduced && !props.IsAnimating, $"Reduced motion still moves spider: {reduced} -> {props.GetSpiderTether()}, animating={props.IsAnimating}");
            }
            window.Width = 1280; window.Height = 800; Pump();
            typeof(MainWindow).GetMethod("PreviewTheme", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, ["summer", false]); Pump();
            Check(props.GetSpiderTether() is null, "Spider branch leaked into another theme");
            typeof(MainWindow).GetMethod("PreviewTheme", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, ["halloween", false]); Pump();
            Check(props.GetSpiderTether() is not null, "Spider branch not restored after theme change");
            Check(((LinearGradientBrush)foreground.OpacityMask).GradientStops.Last().Color.A == 0, "Halloween floor still half transparent");
            foreach (var anchor in SceneDecorationLayout.HalloweenLandscapeAnchors)
            {
                Check(ReferenceEquals(frozenArt[anchor.Kind], artworkType.GetMethod("For")!.Invoke(null,[anchor.Kind])), "Artwork reloaded during theme change");
                props.Configure("standard", false, true, true, 64, 1, false, false);
                props.Configure("halloween", false, true, true, 64, 1, false, false); Pump();
                var actor = props.Props.First(p => p.Kind == anchor.Kind);
                props.Focus();
                typeof(EventPropsScene).GetField("_selectedPropId",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(props,actor.Id);
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
                typeof(EventPropsScene).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(props,[key]);
                Check(key.Handled && actor.State != PropState.Idle, "Painted prop keyboard action missing: " + anchor.Kind);
                props.AdvanceForTest(.5);
                Check(props.ActiveParticleCount <= 64, "Painted prop exceeded particle budget");
            }
            Check(discoveries.Count(d => d.Name == "Die vergessene Formel") == 1 && discoveries.Count(d => d.Name == "Wächter der Nacht") == 1,
                "Book/bat keyboard discoveries missing or duplicated");
            void ResetScene()
            {
                props.Configure("standard", false, true, true, 64, 1, false, false);
                props.Configure("halloween", false, true, true, 64, 1, false, false); Pump();
            }
            void Click(PropKind kind) => Check(props.ActivateForTest(props.Props.First(p => p.Kind == kind).Id), "Click action failed: " + kind);
            ResetScene(); Click(PropKind.Candle); Click(PropKind.Cauldron); Click(PropKind.Potion);
            Check(discoveries.Count == 2, "Wrong recipe order unlocked secret");
            ResetScene(); Click(PropKind.Spellbook); props.AdvanceForTest(.2);
            Render(root, Path.Combine(output,"secret-book-runes.png"));
            ResetScene();
            typeof(MainWindow).GetField("_isBusy",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,true);
            string updateStatus = ((TextBlock)window.FindName("StatusText")).Text;
            Click(PropKind.Candle); Click(PropKind.Potion); Click(PropKind.Cauldron); props.AdvanceForTest(.1);
            Check(discoveries.Count == 3 && discoveries.Last().Name == "Nachtelixier", "Correct click recipe missing");
            Check(((TextBlock)window.FindName("StatusText")).Text == updateStatus, "Secret overwrote active update status");
            Check(props.ToolTip?.ToString()?.Contains("Nachtelixier") == true && props.Props.Count(p => p.Kind == PropKind.Ghost) == 2,
                "Secret feedback or painted ghost child missing");
            Render(root, Path.Combine(output,"secret-night-elixir.png"));
            typeof(MainWindow).GetField("_isBusy",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,false);
            ResetScene(); Click(PropKind.Candle); Click(PropKind.Potion); Click(PropKind.Cauldron);
            Check(discoveries.Count == 3, "Recipe farmed repeated discovery");
            props.Configure("halloween", false, false, true,64,1,false,false);
            Check(!props.ActivateForTest(props.Props.First(p => p.Kind == PropKind.Spellbook).Id), "Disabled decorations still activate");
            props.Configure("halloween", false, true, false,64,1,false,false);
            Check(!props.ActivateForTest(props.Props.First(p => p.Kind == PropKind.Bat).Id), "Disabled interactions still activate");
            ResetScene();
            var cameraWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 120; i++)
            {
                typeof(AtmosphericBackdrop).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(backdrop, new Point(Math.Sin(i), Math.Cos(i)));
                typeof(AtmosphericBackdrop).GetMethod("ApplyCamera", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(backdrop, null);
                props.GetSpiderTether();
            }
            cameraWatch.Stop();
            Console.WriteLine($"Camera projection + prop placement: {cameraWatch.Elapsed.TotalMilliseconds / 120:F3}ms/frame (120 deterministic changes, excludes WPF composition)");
            File.WriteAllText(Path.Combine(output,"result.json"), System.Text.Json.JsonSerializer.Serialize(new { success = true, cases = rows }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("HALLOWEEN_PAINTED_OK 25 size/camera cases, 8 frozen bitmaps, grounded anchors, hit tests, keyboard/click secrets, duplicates/disabled, update status, particles, theme switch + spider regression"); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }
    }
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    private static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => {},DispatcherPriority.ApplicationIdle);
    private static void Render(FrameworkElement view,string path)
    {
        view.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);png.Save(stream);
    }
}
