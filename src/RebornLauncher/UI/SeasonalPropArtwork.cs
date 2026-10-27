using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>A cached, alpha-cropped sprite from a theme's shared prop atlas.</summary>
internal sealed record SeasonalPropArtwork(BitmapSource Image, Rect Bounds)
{
    private sealed record Atlas(string Theme, int Columns, int Rows, PropKind[] Kinds);
    private sealed record AuthoredSourceRect(double X, double Y, double Width, double Height);

    private static readonly Atlas[] Atlases =
    [
        new("standard", 3, 2, [PropKind.Star, PropKind.LightCrystal]),
        new("arceus", 3, 2, [PropKind.Star, PropKind.LightCrystal]),
        new("summer", 3, 2, [PropKind.Bubble, PropKind.WaterBalloon, PropKind.Firefly]),
        new("harvest", 3, 2, [PropKind.LeafPile, PropKind.Acorn, PropKind.Mushroom]),
        new("spring", 3, 2, [PropKind.Egg, PropKind.ChocolateBunny, PropKind.Nest, PropKind.Flower, PropKind.Butterfly]),
        new("newyear", 3, 2, [PropKind.Rocket, PropKind.Sparkler, PropKind.Confetti, PropKind.PartyHat, PropKind.Star]),
        new("winter", 4, 3, [PropKind.Snowman, PropKind.Snowball, PropKind.SnowPile, PropKind.Icicle,
            PropKind.PineTree, PropKind.IceCrystal, PropKind.Gift, PropKind.Ornament, PropKind.Bell, PropKind.CandyCane])
    ];
    // Normalized to the 1536x1024 authored atlas. These dividers sit in the
    // transparent spaces between the complete artwork rather than on the 3x2 grid.
    private static readonly IReadOnlyDictionary<(string Theme, int Index), AuthoredSourceRect> AuthoredSourceRects =
        new Dictionary<(string Theme, int Index), AuthoredSourceRect>
        {
            [("spring", 3)] = new(0, .5, 540d / 1536, .5),
            [("spring", 4)] = new(540d / 1536, .5, 484d / 1536, .5),
            [("harvest", 0)] = new(0, 0, 560d / 1536, .5),
            [("harvest", 1)] = new(560d / 1536, 0, 464d / 1536, .5),
            [("harvest", 2)] = new(1024d / 1536, 0, 512d / 1536, 540d / 1024)
        };
    private static readonly Dictionary<(string Theme, PropKind Kind), SeasonalPropArtwork> Cache = [];

    public static SeasonalPropArtwork? For(string? theme, PropKind kind)
    {
        string key = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
        Atlas? atlas = Atlases.FirstOrDefault(a => a.Theme == key && a.Kinds.Contains(kind));
        if (atlas is null) return null;
        var cacheKey = (key, kind);
        if (Cache.TryGetValue(cacheKey, out SeasonalPropArtwork? cached)) return cached;

        var uri = new Uri($"pack://application:,,,/launcher;component/assets/scenery/props/{key}-props.png", UriKind.Absolute);
        var resource = Application.GetResourceStream(uri);
        if (resource is null) return null;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        using (resource.Stream) { bitmap.StreamSource = resource.Stream; bitmap.EndInit(); }
        bitmap.Freeze();
        int index = Array.IndexOf(atlas.Kinds, kind);
        Int32Rect sourceRect = SourceRectForTheme(key, bitmap.PixelWidth, bitmap.PixelHeight, index);
        var cell = new CroppedBitmap(bitmap, sourceRect);
        cell.Freeze();
        var converted = new FormatConvertedBitmap(cell, PixelFormats.Pbgra32, null, 0);
        converted.Freeze();
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        BitmapSource rgba = converted;
        int left = rgba.PixelWidth, top = rgba.PixelHeight, right = -1, bottom = -1;
        int hardBottom = -1;
        for (int y = 0; y < rgba.PixelHeight; y++)
        for (int x = 0; x < rgba.PixelWidth; x++)
        {
            byte alpha = pixels[y * stride + x * 4 + 3];
            if (alpha > 8)
            {
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            if (alpha >= 128) hardBottom = Math.Max(hardBottom, y);
        }
        if (right < left || hardBottom < top) throw new InvalidOperationException($"Leeres Sprite-Atlas-Feld: {key}/{kind}.");
        // Retain Halloween-style soft alpha around the sprite, but ground at the
        // bottommost painted body pixel instead of a detached low-alpha haze.
        int contactBottom = IsGrounded(kind) ? hardBottom : bottom;
        var cropped = new CroppedBitmap(rgba, new Int32Rect(left, top, right - left + 1, contactBottom - top + 1));
        cropped.Freeze();
        Size maximum = MaximumSize(kind);
        double scale = Math.Min(maximum.Width / cropped.PixelWidth, maximum.Height / cropped.PixelHeight);
        double width = cropped.PixelWidth * scale, height = cropped.PixelHeight * scale;
        // Grounded art uses its lower visible edge as the anchor; airborne art is centered.
        double foot = IsGrounded(kind) ? 0 : height / 2;
        var result = new SeasonalPropArtwork(cropped, new Rect(-width / 2, foot - height, width, height));
        Cache.Add(cacheKey, result);
        return result;
    }

    public static IReadOnlyList<PropKind> KindsForTheme(string? theme)
    {
        string key = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
        return Atlases.FirstOrDefault(a => a.Theme == key)?.Kinds ?? Array.Empty<PropKind>();
    }

    public static (int Columns, int Rows) GridForTheme(string? theme)
    {
        string key = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
        Atlas? atlas = Atlases.FirstOrDefault(a => a.Theme == key);
        return atlas is null ? (0, 0) : (atlas.Columns, atlas.Rows);
    }

    public static Int32Rect CellRectForTheme(string? theme, int pixelWidth, int pixelHeight, int index)
    {
        var (columns, rows) = GridForTheme(theme);
        int count = KindsForTheme(theme).Count;
        if (columns == 0 || rows == 0 || pixelWidth < columns || pixelHeight < rows || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), "Atlasraster, Pixelmaß oder Zellindex ist ungültig.");
        int cellWidth = pixelWidth / columns, cellHeight = pixelHeight / rows;
        var rect = new Int32Rect((index % columns) * cellWidth, (index / columns) * cellHeight, cellWidth, cellHeight);
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
            || rect.X + rect.Width > pixelWidth || rect.Y + rect.Height > pixelHeight)
            throw new InvalidOperationException($"Atlaszelle {index} liegt außerhalb von {pixelWidth}x{pixelHeight}.");
        return rect;
    }

    public static Int32Rect SourceRectForTheme(string? theme, int pixelWidth, int pixelHeight, int index)
    {
        string key = string.IsNullOrWhiteSpace(theme) ? "standard" : theme.Trim().ToLowerInvariant();
        Int32Rect cell = CellRectForTheme(key, pixelWidth, pixelHeight, index);
        if (!AuthoredSourceRects.TryGetValue((key, index), out AuthoredSourceRect? authored)) return cell;
        var rect = new Int32Rect(
            (int)Math.Round(authored.X * pixelWidth, MidpointRounding.AwayFromZero),
            (int)Math.Round(authored.Y * pixelHeight, MidpointRounding.AwayFromZero),
            (int)Math.Round(authored.Width * pixelWidth, MidpointRounding.AwayFromZero),
            (int)Math.Round(authored.Height * pixelHeight, MidpointRounding.AwayFromZero));
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
            || rect.X + rect.Width > pixelWidth || rect.Y + rect.Height > pixelHeight)
            throw new InvalidOperationException($"Authored Sprite-SourceRect {key}/{index} liegt außerhalb von {pixelWidth}x{pixelHeight}.");
        return rect;
    }

    private static bool IsGrounded(PropKind kind) => kind is not
        (PropKind.Bubble or PropKind.Firefly or PropKind.Butterfly or PropKind.Star or PropKind.Confetti or
         PropKind.Icicle or PropKind.Ornament or PropKind.Bell);

    private static Size MaximumSize(PropKind kind) => kind switch
    {
        PropKind.Snowman => new(50, 69), PropKind.Snowball => new(38, 38), PropKind.SnowPile => new(50, 26),
        PropKind.Icicle => new(34, 54), PropKind.PineTree => new(48, 67), PropKind.IceCrystal or PropKind.LightCrystal => new(40, 58),
        PropKind.Gift => new(44, 44), PropKind.Ornament => new(30, 36), PropKind.Bell => new(36, 40),
        PropKind.CandyCane => new(28, 48), PropKind.Egg => new(34, 44), PropKind.ChocolateBunny => new(38, 65),
        PropKind.Nest => new(58, 42), PropKind.Flower => new(40, 62), PropKind.Butterfly => new(52, 44),
        PropKind.Bubble => new(32, 32), PropKind.WaterBalloon => new(36, 42), PropKind.Firefly => new(24, 24),
        PropKind.LeafPile => new(62, 45), PropKind.Acorn => new(34, 43), PropKind.Mushroom => new(46, 51),
        PropKind.Rocket => new(24, 63), PropKind.Sparkler => new(26, 55), PropKind.Confetti => new(48, 44),
        PropKind.PartyHat => new(40, 48), PropKind.Star => new(44, 44), _ => new(48, 48)
    };
}
