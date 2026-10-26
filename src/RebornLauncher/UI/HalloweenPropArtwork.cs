using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>One cached, alpha-cropped painted silhouette per interactive Halloween prop.</summary>
internal sealed record HalloweenPropArtwork(BitmapSource Image, Rect Bounds)
{
    private static readonly Dictionary<PropKind, HalloweenPropArtwork> Cache = [];

    public static HalloweenPropArtwork? For(PropKind kind)
    {
        string? name = kind switch
        {
            PropKind.Pumpkin => "pumpkin", PropKind.Gravestone => "gravestone",
            PropKind.Cauldron => "cauldron", PropKind.Candle => "candle", PropKind.Ghost => "ghost",
            PropKind.Spellbook => "spellbook", PropKind.Potion => "potion", PropKind.Bat => "bat", _ => null
        };
        if (name is null) return null;
        if (Cache.TryGetValue(kind, out var cached)) return cached;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri($"pack://application:,,,/launcher;component/assets/scenery/props/halloween-{name}.png", UriKind.Absolute);
        bitmap.EndInit(); bitmap.Freeze();
        var rgba = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0); rgba.Freeze();
        int stride = rgba.PixelWidth * 4;
        byte[] pixels = new byte[stride * rgba.PixelHeight]; rgba.CopyPixels(pixels, stride, 0);
        int left = rgba.PixelWidth, top = rgba.PixelHeight, right = -1, bottom = -1;
        for (int y = 0; y < rgba.PixelHeight; y++)
        for (int x = 0; x < rgba.PixelWidth; x++)
        {
            if (pixels[y * stride + x * 4 + 3] <= 8) continue;
            left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        if (right < left) throw new InvalidOperationException("Leeres Halloween-Prop: " + name);
        var cropped = new CroppedBitmap(rgba, new Int32Rect(left, top, right - left + 1, bottom - top + 1)); cropped.Freeze();
        var maximum = kind switch
        {
            PropKind.Pumpkin => new Size(88, 84), PropKind.Gravestone => new Size(66, 104),
            PropKind.Cauldron => new Size(96, 94), PropKind.Candle => new Size(60, 84),
            PropKind.Spellbook => new Size(86, 66), PropKind.Potion => new Size(42, 76),
            PropKind.Bat => new Size(70, 68), _ => new Size(78, 96)
        };
        double scale = Math.Min(maximum.Width / cropped.PixelWidth, maximum.Height / cropped.PixelHeight);
        double width = cropped.PixelWidth * scale, height = cropped.PixelHeight * scale;
        double foot = kind == PropKind.Ghost ? height / 2 : 25;
        var result = new HalloweenPropArtwork(cropped, new Rect(-width / 2, foot - height, width, height));
        Cache.Add(kind, result); return result;
    }
}
