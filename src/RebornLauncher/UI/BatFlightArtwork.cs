using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokemonRebornLauncher.UI;

internal static class BatFlightArtwork
{
    private static BitmapSource[]? _frames;
    // Torso anchors in each 512px cell. Asset-specific registration preserves
    // wing extent and body size, rather than stretching six different alpha boxes.
    private static readonly Point[] BodyAnchors =
    [new(257,305),new(265,305),new(264,307),new(262,277),new(264,279),new(263,280)];
    public static int FrameIndex(double flightSeconds) => (int)Math.Floor(Math.Max(0, flightSeconds) * 32) % 6;
    public static (BitmapSource Image, Rect Bounds) Frame(double flightSeconds, Rect perchedBounds)
    {
        if (_frames is null)
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri("pack://application:,,,/launcher;component/assets/scenery/props/halloween-bat-flight.png",UriKind.Absolute);
            bitmap.EndInit(); bitmap.Freeze();
            if (bitmap.PixelWidth % 3 != 0 || bitmap.PixelHeight % 2 != 0) throw new InvalidOperationException("Ungültiges Fledermaus-Spritesheet.");
            int width = bitmap.PixelWidth / 3, height = bitmap.PixelHeight / 2;
            _frames = Enumerable.Range(0,6).Select(i =>
            {
                var frame = new CroppedBitmap(bitmap,new Int32Rect(i%3*width,i/3*height,width,height)); frame.Freeze(); return (BitmapSource)frame;
            }).ToArray();
        }
        int index = FrameIndex(flightSeconds);
        var image = _frames[index]; var anchor = BodyAnchors[index];
        double scale = perchedBounds.Height / 190;
        double bodyY = perchedBounds.Top + perchedBounds.Height * .53;
        return (image,new Rect(-anchor.X*scale,bodyY-anchor.Y*scale,image.PixelWidth*scale,image.PixelHeight*scale));
    }
}
