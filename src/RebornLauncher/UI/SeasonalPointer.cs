using System.IO;
using System.Windows.Input;
using System.Windows.Media;

namespace PokemonRebornLauncher.UI;

/// <summary>Creates a small, theme-colored Windows cursor with a real CUR hotspot.</summary>
public static class SeasonalPointer
{
    private const int Size = 32;
    private const int XorStride = Size * 4;
    private const int AndStride = ((Size + 31) / 32) * 4;
    private const int DibHeaderSize = 40;
    private const int XorSize = XorStride * Size;
    private const int AndSize = AndStride * Size;
    private const int DibSize = DibHeaderSize + XorSize + AndSize;
    private const int CursorImageOffset = 22;

    // Classic arrow silhouette, simply filled with the current theme color.
    private static readonly Point[] Arrow =
    [
        new(3, 2), new(3, 23), new(8, 18), new(12.2, 27), new(16, 25.2),
        new(11.8, 16.2), new(20, 16.2)
    ];

    /// <summary>Builds a valid 32-bit color .cur cursor. Dispose the returned cursor on replacement.</summary>
    public static Cursor Create(Color accent)
    {
        var pixels = RenderPixels(accent);

        using var stream = new MemoryStream(CursorImageOffset + DibSize);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // CUR file header and one 32x32 image entry.
            writer.Write((ushort)0);
            writer.Write((ushort)2);
            writer.Write((ushort)1);
            writer.Write((byte)Size);
            writer.Write((byte)Size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)3); // Hotspot: arrow tip, in image pixels.
            writer.Write((ushort)2);
            writer.Write(DibSize);
            writer.Write(CursorImageOffset);

            // 32-bit BGRA XOR bitmap, bottom-up; height includes the AND mask.
            writer.Write(DibHeaderSize);
            writer.Write(Size);
            writer.Write(Size * 2);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0); // BI_RGB
            writer.Write(XorSize);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            for (var y = Size - 1; y >= 0; y--)
                writer.Write(pixels, y * XorStride, XorStride);

            // 1-bit AND mask: a one marks transparent pixels; scanlines are DWORD aligned.
            var mask = new byte[AndSize];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                if (pixels[(y * Size + x) * 4 + 3] != 0) continue;
                var bottomUpRow = Size - 1 - y;
                mask[bottomUpRow * AndStride + x / 8] |= (byte)(0x80 >> (x % 8));
            }
            writer.Write(mask);
        }

        stream.Position = 0;
        return new Cursor(stream);
    }

    private static bool Contains(double x, double y, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) == (b.Y > y)) continue;
            if (x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    internal static byte[] RenderPixels(Color accent)
    {
        const int samples = 4;
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            int covered = 0, red = 0, green = 0, blue = 0;
            for (var sy = 0; sy < samples; sy++)
            for (var sx = 0; sx < samples; sx++)
            {
                double px = x + (sx + 0.5) / samples, py = y + (sy + 0.5) / samples;
                if (!Contains(px, py, Arrow)) continue;
                bool outline = EdgeDistance(px, py) < 0.9;
                red += outline ? 26 : accent.R;
                green += outline ? 25 : accent.G;
                blue += outline ? 32 : accent.B;
                covered++;
            }
            if (covered == 0) continue;
            // CUR stores straight BGRA; coverage alpha gives smooth edges without
            // the previous thick white halo or a blurred click point.
            SetPixel(pixels, x, y, (byte)(red / covered), (byte)(green / covered),
                (byte)(blue / covered), (byte)(covered * 255 / (samples * samples)));
        }
        return pixels;
    }

    private static double EdgeDistance(double x, double y)
    {
        double nearest = double.MaxValue;
        for (int i = 0, j = Arrow.Length - 1; i < Arrow.Length; j = i++)
        {
            var a = Arrow[j]; var b = Arrow[i];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            double ex = x - a.X - t * dx, ey = y - a.Y - t * dy;
            nearest = Math.Min(nearest, Math.Sqrt(ex * ex + ey * ey));
        }
        return nearest;
    }

    private static void SetPixel(byte[] pixels, int x, int y, byte red, byte green, byte blue, byte alpha)
    {
        var offset = (y * Size + x) * 4;
        pixels[offset] = blue;
        pixels[offset + 1] = green;
        pixels[offset + 2] = red;
        pixels[offset + 3] = alpha;
    }

    private readonly record struct Point(double X, double Y);
}
