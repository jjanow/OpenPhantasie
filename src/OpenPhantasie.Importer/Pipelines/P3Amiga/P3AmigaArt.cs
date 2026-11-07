using System.Buffers.Binary;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P3Amiga;

/// <summary>
/// Phantasie III Amiga picture decoding (the decoding half of <c>tools/scripts/extract_amiga_p3_graphics.py</c>; format
/// notes in <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c>; program addresses are offsets in the flat image of
/// <c>PhantasieIII</c>, see <see cref="AmigaHunks.LoadFlat"/>).
/// <list type="bullet">
/// <item><c>.cmp</c> / <c>.csh</c> / <c>D/s6</c>: 32-B palette (16 words), u16 packed length, packed data (loader
/// 0x123c). The first packed byte picks the unpacker (0x1208): 1 = rows in screen order (0x2b72), else each of the 4
/// planes column by column (0x2bc6). Runs: c = 0 → u16 count + 1 byte repeated; c &gt; 0 → c literal bytes; c &lt; 0 →
/// −c copies of the next byte. The result is a 320x200 screen, 4 sequential planes of 8000 B.</item>
/// <item><c>.csh</c>: a .cmp screen of up to 15 shapes in 48x48 cells (0x293e); sizes in screen row 0.</item>
/// <item>Palettes: words 0x0RGB, 3 bits per gun, loaded as <c>(w &amp; 0x777) &lt;&lt; 1</c> (0x11ae), shown through
/// the same Amiga→ST nibble table as Phantasie I's Amiga pack.</item>
/// </list>
/// </summary>
public static class P3AmigaArt
{
    public const int ScreenBytes = 32000;

    /// <summary><c>amiga_gfx_common.AMIGA_TO_ST</c>: <c>min(255, round(n * 255 / 14))</c> (round half to even).</summary>
    public static readonly byte[] AmigaToSt =
        Enumerable.Range(0, 16).Select(n => (byte)Math.Min(255, Math.Round(n * 255.0 / 14, MidpointRounding.ToEven))).ToArray();

    public sealed record Rgb(byte R, byte G, byte B)
    {
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    public static Rgb[] Palette(ReadOnlySpan<byte> words)
    {
        var output = new Rgb[16];
        for (var i = 0; i < 16; i++)
        {
            var w = (BinaryPrimitives.ReadUInt16BigEndian(words[(i * 2)..]) & 0x777) << 1; // 0x11ae
            output[i] = new Rgb(AmigaToSt[(w >> 8) & 0xF], AmigaToSt[(w >> 4) & 0xF], AmigaToSt[w & 0xF]);
        }
        return output;
    }

    private static int Runs(byte[] d, int p, Action<byte> sink, int limit)
    {
        var n = 0;
        while (n < limit)
        {
            int c = d[p++];
            int count;
            bool run;
            if (c == 0)
            {
                count = (d[p] << 8) | d[p + 1];
                run = true;
                p += 2;
            }
            else if (c < 0x80)
            {
                count = c;
                run = false;
            }
            else
            {
                count = 0x100 - c;
                run = true;
            }
            if (run)
            {
                var b = d[p++];
                for (var i = 0; i < count; i++)
                    sink(b);
            }
            else
            {
                for (var i = 0; i < count; i++)
                    sink(d[p++]);
            }
            n += count;
        }
        return p;
    }

    /// <summary>0x1208: a 320x200x4 screen (4 sequential planes of 8000 B) from a .cmp body.</summary>
    public static byte[] UnpackScreen(byte[] packed)
    {
        byte[] screen;
        int p;
        if (packed[0] == 1) // 0x2b72: screen order
        {
            var buf = new List<byte>(ScreenBytes);
            p = Runs(packed, 1, buf.Add, ScreenBytes);
            screen = new byte[ScreenBytes];
            buf.CopyTo(0, screen, 0, Math.Min(buf.Count, ScreenBytes));
        }
        else // 0x2bc6: plane by plane, 8-pixel column by column
        {
            screen = new byte[ScreenBytes];
            p = 1;
            for (var plane = 0; plane < 4; plane++)
            {
                var k = 0;
                var pl = plane;
                p = Runs(packed, p, b =>
                {
                    var (col, row) = Math.DivRem(k, 200);
                    if (k < 8000)
                        screen[pl * 8000 + row * 40 + col] = b;
                    k++;
                }, 8000);
            }
        }
        if (p != packed.Length)
            throw new InvalidDataException($"packed length {packed.Length} but the unpacker used {p}");
        return screen;
    }

    public sealed record Cmp(Rgb[] Palette, byte[] Screen, byte[] PaletteWords);

    public static Cmp ReadCmp(byte[] d)
    {
        var n = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(32));
        return new Cmp(Palette(d.AsSpan(0, 32)), UnpackScreen(d.AsSpan(34, n).ToArray()), d.AsSpan(0, 32).ToArray());
    }

    /// <summary>Colour index of every pixel of a planar screen, row-major 320x200.</summary>
    public static byte[] ScreenIndices(byte[] screen)
    {
        var output = new byte[320 * 200];
        for (var y = 0; y < 200; y++)
            for (var xb = 0; xb < 40; xb++)
            {
                int p0 = screen[y * 40 + xb], p1 = screen[8000 + y * 40 + xb], p2 = screen[16000 + y * 40 + xb],
                    p3 = screen[24000 + y * 40 + xb];
                for (var bit = 0; bit < 8; bit++)
                {
                    var s = 7 - bit;
                    output[y * 320 + xb * 8 + bit] = (byte)(((p0 >> s) & 1) | (((p1 >> s) & 1) << 1)
                                                            | (((p2 >> s) & 1) << 2) | (((p3 >> s) & 1) << 3));
                }
            }
        return output;
    }

    /// <summary>The whole screen in <paramref name="pal"/> (Pillow mode "RGB": opaque).</summary>
    public static RgbaImage ScreenImage(byte[] screen, Rgb[] pal)
    {
        var idx = ScreenIndices(screen);
        var im = new RgbaImage(320, 200);
        for (var i = 0; i < idx.Length; i++)
        {
            var c = pal[idx[i]];
            im.Set(i % 320, i / 320, c.R, c.G, c.B);
        }
        return im;
    }

    /// <summary>A part of a screen in <paramref name="pal"/> (Pillow mode "RGBA"; colour 0 transparent when asked).</summary>
    public static RgbaImage IndexedCrop(byte[] screen, (int X0, int Y0, int X1, int Y1) box, Rgb[] pal, bool transparent0 = false)
    {
        var idx = ScreenIndices(screen);
        var im = new RgbaImage(box.X1 - box.X0, box.Y1 - box.Y0);
        for (var y = box.Y0; y < box.Y1; y++)
            for (var x = box.X0; x < box.X1; x++)
            {
                var v = idx[y * 320 + x];
                if (transparent0 && v == 0)
                    im.Set(x - box.X0, y - box.Y0, 0, 0, 0, 0);
                else
                    im.Set(x - box.X0, y - box.Y0, pal[v].R, pal[v].G, pal[v].B);
            }
        return im;
    }

    /// <summary>0x293e: (k, x, y, width px, rows) of each non-empty shape k &lt; 15.</summary>
    public static List<(int K, int X, int Y, int W, int H)> CshShapes(byte[] screen)
    {
        var output = new List<(int, int, int, int, int)>();
        for (var k = 0; k < 15; k++)
        {
            var off = (k % 4) * 8000 + (k >> 2) * 2;
            int rows = screen[off] + 1, wbytes = screen[off + 1];
            if (rows > 1 && wbytes is > 0 and < 0x80)
                output.Add((k, (k % 5) * 48, (k / 5) * 48 + 1, wbytes * 8, rows));
        }
        return output;
    }

    /// <summary>Tile <paramref name="i"/> of a <c>phantasy.set</c> tile block (16x16, 4 interleaved plane words a row).</summary>
    public static RgbaImage TileImage(ReadOnlySpan<byte> tiles, int i, Rgb[] pal, bool transparent0 = false)
    {
        var im = new RgbaImage(16, 16);
        var t = tiles.Slice(i * 128, 128);
        Span<int> ws = stackalloc int[4];
        for (var r = 0; r < 16; r++)
        {
            for (var pl = 0; pl < 4; pl++)
                ws[pl] = BinaryPrimitives.ReadUInt16BigEndian(t[(r * 8 + pl * 2)..]);
            for (var x = 0; x < 16; x++)
            {
                var v = 0;
                for (var pl = 0; pl < 4; pl++)
                    v |= ((ws[pl] >> (15 - x)) & 1) << pl;
                if (transparent0 && v == 0)
                    im.Set(x, r, 0, 0, 0, 0);
                else
                    im.Set(x, r, pal[v].R, pal[v].G, pal[v].B);
            }
        }
        return im;
    }

    /// <summary>Pillow's <c>crop</c> of an opaque image: outside the source is opaque black (RGB zero).</summary>
    public static RgbaImage Crop(RgbaImage src, int x0, int y0, int x1, int y1)
    {
        var im = new RgbaImage(x1 - x0, y1 - y0);
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                if (x >= 0 && y >= 0 && x < src.Width && y < src.Height)
                {
                    var (r, g, b, a) = src.Get(x, y);
                    im.Set(x - x0, y - y0, r, g, b, a);
                }
                else
                    im.Set(x - x0, y - y0, 0, 0, 0);
            }
        return im;
    }

    /// <summary>Pillow's <c>paste</c> without a mask: the pixels are replaced.</summary>
    public static void Paste(RgbaImage dst, RgbaImage src, int x0, int y0)
    {
        for (var y = 0; y < src.Height; y++)
            Array.Copy(src.Pixels, y * src.Width * 4, dst.Pixels, ((y0 + y) * dst.Width + x0) * 4, src.Width * 4);
    }
}
