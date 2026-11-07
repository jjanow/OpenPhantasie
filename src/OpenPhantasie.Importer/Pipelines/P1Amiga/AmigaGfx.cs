using System.Buffers.Binary;
using System.Text;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Shared Amiga planar-graphics helpers (port of <c>tools/scripts/amiga_gfx_common.py</c>). The art is raw bitplane
/// blobs (Kroah / Sprite Viewer layout), not IFF ILBM; palette words are OCS 12-bit <c>0x0RGB</c>. Every I screen sets
/// its table with LoadRGB4, so on screen a 4-bit gun <c>c</c> is <c>c * 17</c> (<c>hw4</c>); the Amiga→ST nibble LUT
/// is kept for the palette swatches.
/// </summary>
public static class AmigaGfx
{
    /// <summary>Amiga nibble → 8 bits on the house 3-bit ramp: <c>round(n * 255 / 14)</c> (Python's round half to even).</summary>
    public static readonly byte[] AmigaToSt = Enumerable.Range(0, 16)
        .Select(n => (byte)Math.Min(255, Math.Round(n * 255 / 14.0, MidpointRounding.ToEven))).ToArray();

    public const int PaletteCombatOff = 0x194BC;
    public const int PaletteAltOff = 0x194DC;
    public const int PaletteTownOff = 0x1947C;
    public const int PaletteMapOff = 0x1949C;

    public const int MonsterCount = 80;
    public const int MonsterAddrOff = 0x16842;
    public const int MonsterDimOff = 0x1EA8;
    public const int MonsterNameOff = 0xC88;
    public const int MonsterNameStride = 58;

    public const int RaceCount = 16;
    public const int RaceNameOff = 0x69BE;
    public const int RaceBytes = 32 * 32 * 4 / 8;

    public static int Word(byte[] d, int off) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(off));
    public static short SWord(byte[] d, int off) => BinaryPrimitives.ReadInt16BigEndian(d.AsSpan(off));
    public static uint Long(byte[] d, int off) => BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(off));

    /// <summary>NUL-terminated ASCII; bytes above 0x7f become U+FFFD (Python's <c>errors="replace"</c>).</summary>
    public static string CString(byte[] d, int off)
    {
        var end = Array.IndexOf(d, (byte)0, off);
        if (end < 0)
            throw new InvalidDataException($"unterminated string at 0x{off:x}");
        var sb = new StringBuilder(end - off);
        for (var i = off; i < end; i++)
            sb.Append(d[i] < 0x80 ? (char)d[i] : '�');
        return sb.ToString();
    }

    public readonly record struct Rgba(byte R, byte G, byte B, byte A);

    /// <summary>The 16 colours at <paramref name="address"/>: the ST nibble LUT, or with <paramref name="hw4"/> c * 17.</summary>
    public static Rgba[] LoadPalette(byte[] game, int address, bool hw4 = false, int n = 16)
    {
        var colours = new Rgba[n];
        for (var i = 0; i < n; i++)
        {
            var rgb = Word(game, address + 2 * i);
            int r = (rgb >> 8) & 0xF, g = (rgb >> 4) & 0xF, b = rgb & 0xF;
            colours[i] = hw4
                ? new Rgba((byte)(r * 17), (byte)(g * 17), (byte)(b * 17), 255)
                : new Rgba(AmigaToSt[r], AmigaToSt[g], AmigaToSt[b], 255);
        }
        return colours;
    }

    public static string[] PaletteRawWords(byte[] game, int address, int n = 16) =>
        Enumerable.Range(0, n).Select(i => Word(game, address + 2 * i).ToString("x3")).ToArray();

    /// <summary>Sequential (non-interleaved) bitplanes: the Amiga PAT / .pic layout.</summary>
    public static RgbaImage DecodePlanar(byte[] data, int address, int width, int height, Rgba[] palette, int bp = 4)
    {
        if (width % 8 != 0)
            throw new ArgumentException($"width must be multiple of 8, got {width}");
        var scanline = width / 8;
        var plane = width * height / 8;
        if (address + plane * bp > data.Length)
            throw new InvalidDataException($"planar read past EOF: addr={address} need={plane * bp} len={data.Length}");
        var img = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = 0;
                for (var p = 0; p < bp; p++)
                    if ((data[address + (x >> 3) + p * plane + y * scanline] & (1 << (7 - (x % 8)))) != 0)
                        color |= 1 << p;
                var c = palette[color];
                img.Set(x, y, c.R, c.G, c.B, c.A);
            }
        return img;
    }

    /// <summary>
    /// A graphics.library SimpleSprite (2 control words, then per row word A = bit 0 and word B = bit 1): colour 0
    /// transparent, colours 1-3 as given.
    /// </summary>
    public static RgbaImage DecodeSprite(byte[] d, int offset, int height, Rgba[] colours13)
    {
        var colours = new Rgba[] { new(0, 0, 0, 0), colours13[0], colours13[1], colours13[2] };
        var img = new RgbaImage(16, height);
        var b0 = offset + 4;
        for (var y = 0; y < height; y++)
        {
            var a = Word(d, b0 + 4 * y);
            var b = Word(d, b0 + 4 * y + 2);
            for (var x = 0; x < 16; x++)
            {
                var bit = 15 - x;
                var c = colours[((a >> bit) & 1) | (((b >> bit) & 1) << 1)];
                img.Set(x, y, c.R, c.G, c.B, c.A);
            }
        }
        return img;
    }

    public static Rgba Gun4(int word) =>
        new((byte)(((word >> 8) & 15) * 17), (byte)(((word >> 4) & 15) * 17), (byte)((word & 15) * 17), 255);

    public static string Slug(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace(" ", "-").Replace(".", "");
        return string.Concat(s.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-'));
    }

    public static RgbaImage Crop(RgbaImage src, int x0, int y0, int x1, int y1)
    {
        var img = new RgbaImage(x1 - x0, y1 - y0);
        for (var y = y0; y < y1; y++)
            Array.Copy(src.Pixels, (y * src.Width + x0) * 4, img.Pixels, ((y - y0) * img.Width) * 4, img.Width * 4);
        return img;
    }

    /// <summary>Copy <paramref name="src"/> to (x, y), clipped (Pillow's paste of an opaque image).</summary>
    public static void Paste(RgbaImage dst, RgbaImage src, int x, int y)
    {
        for (var sy = 0; sy < src.Height; sy++)
        {
            var dy = y + sy;
            if (dy < 0 || dy >= dst.Height)
                continue;
            for (var sx = 0; sx < src.Width; sx++)
            {
                var dx = x + sx;
                if (dx < 0 || dx >= dst.Width)
                    continue;
                Array.Copy(src.Pixels, (sy * src.Width + sx) * 4, dst.Pixels, (dy * dst.Width + dx) * 4, 4);
            }
        }
    }

    public static RgbaImage Filled(int w, int h, Rgba c)
    {
        var img = new RgbaImage(w, h);
        for (var i = 0; i < img.Pixels.Length; i += 4)
        {
            img.Pixels[i] = c.R;
            img.Pixels[i + 1] = c.G;
            img.Pixels[i + 2] = c.B;
            img.Pixels[i + 3] = c.A;
        }
        return img;
    }

    /// <summary>Python's floor division.</summary>
    public static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);
}
