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

    // Points form a slightly chunky pixel-art arrow with its tip at the top left.
    private static readonly Point[] Arrow =
    [
        new(2, 1), new(2, 24), new(8, 18), new(14, 29), new(19, 27),
        new(14, 15), new(27, 15)
    ];

    /// <summary>Builds a valid 32-bit color .cur cursor. Dispose the returned cursor on replacement.</summary>
    public static Cursor Create(Color accent)
    {
        var pixels = new byte[Size * Size * 4];
        var silhouette = new bool[Size * Size];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
            silhouette[y * Size + x] = Contains(x + 0.5, y + 0.5, Arrow);

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var isInside = silhouette[y * Size + x];
            var nearEdge = false;
            for (var oy = -1; oy <= 1 && !nearEdge; oy++)
            for (var ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                var nx = x + ox;
                var ny = y + oy;
                var neighborInside = nx >= 0 && nx < Size && ny >= 0 && ny < Size && silhouette[ny * Size + nx];
                if (neighborInside != isInside) { nearEdge = true; break; }
            }

            if (isInside)
            {
                if (nearEdge) SetPixel(pixels, x, y, 18, 16, 22, 255); // ink outline
                else SetAccentPixel(pixels, x, y, accent);
            }
            else if (nearEdge)
            {
                SetPixel(pixels, x, y, 255, 255, 255, 255); // clear white edge against any theme
            }
        }

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
            writer.Write((ushort)2); // Hotspot: arrow tip, in image pixels.
            writer.Write((ushort)1);
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

    private static void SetAccentPixel(byte[] pixels, int x, int y, Color accent)
    {
        // A small, brighter shoulder gives the arrow dimension without muting the theme color.
        var highlight = y < 9 && x < 9;
        static byte Lift(byte value) => (byte)Math.Min(255, value + 54);
        SetPixel(pixels, x, y,
            highlight ? Lift(accent.R) : accent.R,
            highlight ? Lift(accent.G) : accent.G,
            highlight ? Lift(accent.B) : accent.B,
            255);
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
