using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application();
        var expected = new Dictionary<string, (int Columns, int Rows, PropKind[] Kinds)>
        {
            ["standard"] = (3, 2, [PropKind.Star, PropKind.LightCrystal]),
            ["arceus"] = (3, 2, [PropKind.Star, PropKind.LightCrystal]),
            ["summer"] = (3, 2, [PropKind.Bubble, PropKind.WaterBalloon, PropKind.Firefly]),
            ["harvest"] = (3, 2, [PropKind.LeafPile, PropKind.Acorn, PropKind.Mushroom]),
            ["spring"] = (3, 2, [PropKind.Egg, PropKind.ChocolateBunny, PropKind.Nest, PropKind.Flower, PropKind.Butterfly]),
            ["newyear"] = (3, 2, [PropKind.Rocket, PropKind.Sparkler, PropKind.Confetti, PropKind.PartyHat, PropKind.Star]),
            ["winter"] = (4, 3, [PropKind.Snowman, PropKind.Snowball, PropKind.SnowPile, PropKind.Icicle,
                PropKind.PineTree, PropKind.IceCrystal, PropKind.Gift, PropKind.Ornament, PropKind.Bell, PropKind.CandyCane])
        };
        var errors = new List<string>();
        int loadedThemeCount = 0;
        foreach (var (theme, spec) in expected)
        {
            Check(SeasonalPropArtwork.GridForTheme(theme) == (spec.Columns, spec.Rows), $"{theme}: falsches Raster.");
            Check(SeasonalPropArtwork.KindsForTheme(theme).SequenceEqual(spec.Kinds), $"{theme}: falsche Zellreihenfolge.");
            BitmapSource? atlas = TryLoadAtlas(theme);
            if (atlas is null) continue; // Root adds harvest/newyear atlases later.
            loadedThemeCount++;
            Check(atlas.PixelWidth == 1536 && atlas.PixelHeight == 1024, $"{theme}: unerwartete Bildgröße {atlas.PixelWidth}x{atlas.PixelHeight}.");
            for (int index = 0; index < spec.Kinds.Length; index++)
            {
                Int32Rect sourceRect = SeasonalPropArtwork.SourceRectForTheme(theme, atlas.PixelWidth, atlas.PixelHeight, index);
                int cellMaximum = MaxAlpha(Crop(atlas, sourceRect));
                Check(cellMaximum >= 250, $"{theme}/{spec.Kinds[index]}: Motivkern ist leer oder zu transparent (max alpha {cellMaximum}).");
                string hardEdgeContacts = HardEdgeContacts(atlas, sourceRect);
                Check(hardEdgeContacts.Length == 0,
                    $"{theme}/{spec.Kinds[index]}: Motiv berührt SourceRect-Rand mit alpha >=128: {hardEdgeContacts}.");

                SeasonalPropArtwork? artwork = SeasonalPropArtwork.For(theme, spec.Kinds[index]);
                Check(artwork is not null, $"{theme}/{spec.Kinds[index]}: For() konnte keine Resource laden.");
                if (artwork is null) continue;
                Check(MaxAlpha(artwork.Image) >= 250, $"{theme}/{spec.Kinds[index]}: alpha-cropped Motivkern fehlt.");
                Check(artwork.Image.PixelWidth > 0 && artwork.Image.PixelHeight > 0
                    && artwork.Bounds.Width > 0 && artwork.Bounds.Height > 0,
                    $"{theme}/{spec.Kinds[index]}: Alpha-Bounds sind leer.");
                if (IsGrounded(spec.Kinds[index]))
                {
                    Check(Math.Abs(artwork.Bounds.Bottom) < .001, $"{theme}/{spec.Kinds[index]}: Fußanker liegt nicht auf Bounds.Bottom.");
                    Check(MaxAlpha(Crop(artwork.Image, new Int32Rect(0, artwork.Image.PixelHeight - 1,
                        artwork.Image.PixelWidth, 1))) >= 128,
                        $"{theme}/{spec.Kinds[index]}: gecropptes Bild endet unterhalb des gemalten Fußkontakts.");
                }
                else
                    Check(Math.Abs(artwork.Bounds.Top + artwork.Bounds.Height / 2) < .001,
                        $"{theme}/{spec.Kinds[index]}: Flugmotiv ist nicht um sein Zentrum ausgerichtet.");
            }
            var sourceRects = Enumerable.Range(0, spec.Kinds.Length)
                .Select(index => SeasonalPropArtwork.SourceRectForTheme(theme, atlas.PixelWidth, atlas.PixelHeight, index)).ToArray();
            for (int first = 0; first < sourceRects.Length; first++)
            for (int second = first + 1; second < sourceRects.Length; second++)
                Check(!Overlaps(sourceRects[first], sourceRects[second]),
                    $"{theme}: SourceRects {first} und {second} überlappen.");
        }
        Check(loadedThemeCount >= 5, $"Erwartet mindestens fünf echte Atlasressourcen, geladen: {loadedThemeCount}.");

        Check(SeasonalPropArtwork.KindsForTheme("halloween").Count == 0, "Halloween muss außerhalb des Seasonal-Atlaspfads bleiben.");
        Check(SeasonalPropArtwork.GridForTheme("unknown") == (0, 0), "Unbekannte Themes dürfen keinen Atlas wählen.");
        var winterCells = Enumerable.Range(0, 10).Select(i => SeasonalPropArtwork.CellRectForTheme("winter", 1536, 1024, i)).ToArray();
        Check(winterCells.All(r => r.Width == 384 && r.Height == 341 && r.X + r.Width <= 1536 && r.Y + r.Height <= 1024),
            "Winterzellen müssen floor-sized 384x341 Pixel und vollständig innerhalb des Atlasses liegen.");
        Check(winterCells[9].Y + winterCells[9].Height == 1023, "Das einzelne untere Winter-Restpixel muss unbenutzt bleiben.");
        Check(SeasonalPropArtwork.SourceRectForTheme("spring", 1536, 1024, 3) == new Int32Rect(0, 512, 540, 512)
            && SeasonalPropArtwork.SourceRectForTheme("spring", 1536, 1024, 4) == new Int32Rect(540, 512, 484, 512),
            "Spring-SourceRects müssen im transparenten Zwischenraum bei x=540 getrennt sein.");
        Check(SeasonalPropArtwork.SourceRectForTheme("harvest", 1536, 1024, 0) == new Int32Rect(0, 0, 560, 512)
            && SeasonalPropArtwork.SourceRectForTheme("harvest", 1536, 1024, 1) == new Int32Rect(560, 0, 464, 512)
            && SeasonalPropArtwork.SourceRectForTheme("harvest", 1536, 1024, 2) == new Int32Rect(1024, 0, 512, 540),
            "Harvest-SourceRects müssen Blatt/Acorn bei x=560 und Mushroom bei y=540 trennen.");

        if (args.Length == 2 && args[0] == "--winter-probe")
        {
            try { RenderWinterProbe(args[1]); }
            catch (Exception ex) { errors.Add("Winter-WPF-Probe fehlgeschlagen: " + ex); }
        }
        else if (args.Length == 2 && args[0] == "--seasonal-gallery")
        {
            try { RenderSeasonalGallery(args[1]); }
            catch (Exception ex) { errors.Add("Saisonale Grounding-Galerie fehlgeschlagen: " + ex); }
        }
        else if (args.Length != 0) errors.Add("Aufruf: SeasonalPropArtworkTests [--winter-probe <PNG-Datei> | --seasonal-gallery <Ordner>]");

        int layoutCases = 0;
        foreach (string theme in expected.Keys)
        foreach (bool festival in theme == "winter" ? new[] { false, true } : new[] { false })
        foreach ((double w, double h) in new[] { (960d, 1000d), (960d, 640d), (1280d, 800d), (1920d, 640d), (1920d, 1080d) })
        {
            string context = $"{theme}{(festival ? "/festival" : "")} {w}x{h}";
            var themeArray = SceneDecorationLayout.ForTheme(theme, festival);
            // These are scenery contacts, including airborne/branch discoveries.
            // GroundFootY is only the old screen-relative fallback. Parent-bound
            // children inherit their contact instead of receiving another anchor.
            var anchors = SceneDecorationLayout.SeasonalAnchorsForAspect(theme, w / h, festival);
            int[] independentIndices = Enumerable.Range(0, themeArray.Count)
                .Where(index => themeArray[index].ParentKind is null).ToArray();
            Check(anchors.Select(a => a.Index).Order().SequenceEqual(independentIndices),
                $"{context}: Bildanker müssen jeden unabhängigen Prop genau einmal abdecken.");
            Check(anchors.All(a => a.Index >= 0 && a.Index < themeArray.Count
                && a.X is >= 0 and <= 1 && a.Y is >= 0 and <= 1),
                $"{context}: ungültiger Bildanker oder Theme-Index.");
            double scale = Math.Max((w + 48) / 1672, (h + 28) / 941);
            double left = (w - 1672 * scale) / 2, top = h - 941 * scale + 14;
            var points = anchors.ToDictionary(a => a.Index,
                a => new Point(a.X * 1672 * scale + left, a.Y * 941 * scale + top));
            foreach (var (index, point) in points)
                Check(point.X >= 0 && point.X <= w && point.Y >= 0 && point.Y <= h,
                    $"{context}: Bildanker {index} liegt außerhalb des Cover-Ausschnitts: {point}.");
            try
            {
                var props = new EventPropsScene { Width = w, Height = h };
                props.Configure(theme, christmas: festival, enabled: true, interactive: false,
                    particleLimit: 0, intensity: 0, reduceMotion: true, active: false);
                props.SetTerrainAttachments(points);
                props.Measure(new Size(w, h)); props.Arrange(new Rect(0, 0, w, h));
                props.VerifyLayout();
                Check(props.GetPlacementBounds().Count == themeArray.Count,
                    $"{context}: Ein Prop fehlt in der tatsächlichen Platzierung.");
                foreach (var (index, point) in points)
                {
                    SceneDecorationAnchor anchor = themeArray[index];
                    if (!IsGrounded(anchor.Kind)) continue;
                    int occurrence = themeArray.Take(index).Count(a => a.Kind == anchor.Kind);
                    EventProp prop = props.Props.Where(p => p.Kind == anchor.Kind && !p.IsTemporary)
                        .OrderBy(p => p.Id).ElementAt(occurrence);
                    Check(Math.Abs(prop.HomeX * w / 1000 - point.X) < .001
                        && Math.Abs(prop.HomeY * h / 400 - point.Y) < .001,
                        $"{context}/{index}/{anchor.Kind}: Sprite-Fuß löst sich vom Bildkontakt.");
                }
                layoutCases++;
            }
            catch (Exception ex) { errors.Add($"{context}: WPF-Layout fehlgeschlagen: {ex.Message}"); }
        }

        Console.WriteLine(errors.Count == 0
            ? $"PASS: echte Atlasressourcen, Alpha-Bounds, Fußanker, Zellränder und {layoutCases} saisonale WPF-Coverlayouts."
            : string.Join(Environment.NewLine, errors));
        app.Shutdown();
        return errors.Count == 0 ? 0 : 1;

        void Check(bool condition, string message) { if (!condition) errors.Add(message); }
    }

    private static void RenderWinterProbe(string outputPath)
    {
        const int width = 960, height = 1000;
        BitmapSource background = LoadAtlas("winter-landscape", "inhabited");
        double scale = Math.Max((width + 48d) / background.PixelWidth, (height + 28d) / background.PixelHeight);
        double artWidth = background.PixelWidth * scale, artHeight = background.PixelHeight * scale;
        var root = new Canvas { Width = width, Height = height, ClipToBounds = true };
        var image = new Image { Source = background, Width = artWidth, Height = artHeight, Stretch = Stretch.Fill };
        Canvas.SetLeft(image, (width - artWidth) / 2); Canvas.SetTop(image, height - artHeight + 14);
        root.Children.Add(image);

        var props = new EventPropsScene { Width = width, Height = height, IsHitTestVisible = false };
        root.Children.Add(props);
        props.Configure("winter", christmas: false, enabled: true, interactive: false, particleLimit: 0,
            intensity: 0, reduceMotion: true, active: false);
        var points = new Dictionary<int, Point>();
        foreach (var anchor in SceneDecorationLayout.SeasonalAnchorsForAspect("winter", width / (double)height))
            points[anchor.Index] = new Point(anchor.X * background.PixelWidth * scale + (width - artWidth) / 2,
                anchor.Y * background.PixelHeight * scale + height - artHeight + 14);
        props.SetTerrainAttachments(points);

        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        props.VerifyLayout();
        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var file = File.Create(outputPath); encoder.Save(file);
    }

    private static void RenderSeasonalGallery(string outputDirectory)
    {
        string fullOutput = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutput);
        foreach (string theme in new[] { "standard", "arceus", "spring", "summer", "harvest", "newyear", "winter" })
        foreach ((int width, int height) in new[] { (1280, 800), (960, 1000) })
        {
            BitmapSource baseImage = theme == "winter"
                ? LoadAtlas("winter-landscape", "inhabited")
                : LoadAtlas(theme + "-far", "parallax");
            BitmapSource terrain = theme == "winter" ? baseImage : LoadAtlas(theme + "-terrain", "parallax");
            BitmapSource? near = TryLoadAtlas(theme == "winter" ? "winter-foreground" : theme + "-near", theme == "winter" ? "scenery" : "parallax");
            double scale = Math.Max((width + 48d) / 1672, (height + 28d) / 941);
            double artWidth = 1672 * scale, artHeight = 941 * scale;
            double left = (width - artWidth) / 2, top = height - artHeight + 14;
            var root = new Canvas { Width = width, Height = height, ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(9, 10, 23)) };
            AddLayer(root, baseImage, artWidth, artHeight, left, top);
            if (theme != "winter") AddLayer(root, terrain, artWidth, artHeight, left, top);
            if (near is not null) AddLayer(root, near, artWidth, artHeight, left, top);
            var props = new EventPropsScene { Width = width, Height = height, IsHitTestVisible = true };
            root.Children.Add(props);
            props.Configure(theme, christmas: false, enabled: true, interactive: true, particleLimit: 0,
                intensity: 0, reduceMotion: true, active: false);
            var points = new Dictionary<int, Point>();
            foreach (var anchor in SceneDecorationLayout.SeasonalAnchorsForAspect(theme, width / (double)height))
                points[anchor.Index] = new Point(anchor.X * 1672 * scale + left, anchor.Y * 941 * scale + top);
            props.SetTerrainAttachments(points);

            root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
            props.VerifyLayout();
            var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
            string path = Path.Combine(fullOutput, $"{theme}-{width}x{height}.png");
            using var file = File.Create(path); encoder.Save(file);
        }
    }

    private static void AddLayer(Canvas root, BitmapSource source, double width, double height, double left, double top)
    {
        var image = new Image { Source = source, Width = width, Height = height, Stretch = Stretch.Fill, IsHitTestVisible = false };
        Canvas.SetLeft(image, left); Canvas.SetTop(image, top);
        root.Children.Add(image);
    }

    private static BitmapSource? TryLoadAtlas(string theme) => TryLoadAtlas(theme, "props");

    private static BitmapSource? TryLoadAtlas(string name, string folder)
    {
        string fileName = folder == "props" ? name + "-props" : name;
        var uri = new Uri($"pack://application:,,,/launcher;component/assets/scenery/{folder}/{fileName}.png", UriKind.Absolute);
        Stream? stream = null;
        try { stream = Application.GetResourceStream(uri)?.Stream; }
        catch (IOException) { }
        if (stream is null)
        {
            string copiedFolder = folder == "scenery" ? "" : folder + Path.DirectorySeparatorChar;
            string copied = Path.Combine(AppContext.BaseDirectory, "assets", "scenery", copiedFolder, fileName + ".png");
            if (File.Exists(copied)) stream = File.OpenRead(copied);
        }
        if (stream is null) return null;
        var image = new BitmapImage();
        using (stream) { image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); }
        image.Freeze();
        return image;
    }

    private static BitmapSource LoadAtlas(string name, string folder) => TryLoadAtlas(name, folder)
        ?? throw new InvalidOperationException($"Bildresource fehlt: {folder}/{name}.");

    private static BitmapSource Crop(BitmapSource image, Int32Rect rect)
    {
        var crop = new CroppedBitmap(image, rect); crop.Freeze();
        return new FormatConvertedBitmap(crop, PixelFormats.Pbgra32, null, 0);
    }

    private static int MaxAlpha(BitmapSource image)
    {
        var rgba = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
        int stride = rgba.PixelWidth * 4; byte[] pixels = new byte[stride * rgba.PixelHeight]; rgba.CopyPixels(pixels, stride, 0);
        int max = 0; for (int p = 3; p < pixels.Length; p += 4) max = Math.Max(max, pixels[p]);
        return max;
    }

    private static bool Overlaps(Int32Rect a, Int32Rect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static string HardEdgeContacts(BitmapSource image, Int32Rect cell)
    {
        var rgba = Crop(image, cell);
        int stride = rgba.PixelWidth * 4;
        byte[] pixels = new byte[stride * rgba.PixelHeight];
        rgba.CopyPixels(pixels, stride, 0);
        var contacts = new List<string>();
        Add("top", Enumerable.Range(0, rgba.PixelWidth).Where(x => pixels[x * 4 + 3] >= 128), cell.Y);
        Add("bottom", Enumerable.Range(0, rgba.PixelWidth).Where(x => pixels[(rgba.PixelHeight - 1) * stride + x * 4 + 3] >= 128), cell.Y + rgba.PixelHeight - 1);
        Add("left", Enumerable.Range(0, rgba.PixelHeight).Where(y => pixels[y * stride + 3] >= 128), cell.X);
        Add("right", Enumerable.Range(0, rgba.PixelHeight).Where(y => pixels[y * stride + (rgba.PixelWidth - 1) * 4 + 3] >= 128), cell.X + rgba.PixelWidth - 1);
        return string.Join("; ", contacts);

        void Add(string side, IEnumerable<int> indices, int coordinate)
        {
            int[] values = indices.ToArray();
            if (values.Length == 0) return;
            string runs = string.Join(",", ToRuns(values));
            contacts.Add($"{side} global={coordinate}, count={values.Length}, local runs={runs}");
        }

        static IEnumerable<string> ToRuns(int[] values)
        {
            int start = values[0], end = start;
            foreach (int value in values.Skip(1))
            {
                if (value == end + 1) { end = value; continue; }
                yield return start == end ? start.ToString() : $"{start}-{end}";
                start = end = value;
            }
            yield return start == end ? start.ToString() : $"{start}-{end}";
        }
    }

    private static bool IsGrounded(PropKind kind) => kind is not
        (PropKind.Bubble or PropKind.Firefly or PropKind.Butterfly or PropKind.Star or PropKind.Confetti or
         PropKind.Icicle or PropKind.Ornament or PropKind.Bell);
}
