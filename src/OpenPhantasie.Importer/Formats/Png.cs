using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenPhantasie.Importer.Formats;

/// <summary>
/// A decoded or to-be-encoded image: 8-bit RGBA, row-major. The importer builds art in this form and writes it with
/// <see cref="Png.Encode"/> (no Pillow, no native code).
/// </summary>
public sealed class RgbaImage
{
    public RgbaImage(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public RgbaImage(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("pixel buffer size");
        Width = width;
        Height = height;
        Pixels = rgba;
    }

    public int Width { get; }
    public int Height { get; }
    /// <summary>R, G, B, A per pixel.</summary>
    public byte[] Pixels { get; }

    public void Set(int x, int y, byte r, byte g, byte b, byte a = 255)
    {
        var i = (y * Width + x) * 4;
        Pixels[i] = r;
        Pixels[i + 1] = g;
        Pixels[i + 2] = b;
        Pixels[i + 3] = a;
    }

    public (byte R, byte G, byte B, byte A) Get(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    /// <summary>True when every pixel is opaque (written as RGB, as Pillow does for mode "RGB").</summary>
    public bool IsOpaque()
    {
        for (var i = 3; i < Pixels.Length; i += 4)
            if (Pixels[i] != 255)
                return false;
        return true;
    }

    /// <summary>Same size and same RGBA everywhere; fully transparent pixels compare equal whatever their colour.</summary>
    public bool PixelEquals(RgbaImage other)
    {
        if (Width != other.Width || Height != other.Height)
            return false;
        for (var i = 0; i < Pixels.Length; i += 4)
        {
            if (Pixels[i + 3] != other.Pixels[i + 3])
                return false;
            if (Pixels[i + 3] == 0)
                continue;
            if (Pixels[i] != other.Pixels[i] || Pixels[i + 1] != other.Pixels[i + 1] || Pixels[i + 2] != other.Pixels[i + 2])
                return false;
        }
        return true;
    }
}

/// <summary>Minimal PNG writer and reader (8-bit RGB / RGBA out; greyscale, RGB, palette, RGBA of 1–8 bits in).</summary>
public static class Png
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <param name="forceAlpha">Write RGBA even when every pixel is opaque (Pillow mode "RGBA").</param>
    public static byte[] Encode(RgbaImage image, bool forceAlpha = false)
    {
        var alpha = forceAlpha || !image.IsOpaque();
        var bpp = alpha ? 4 : 3;
        var stride = image.Width * bpp;
        var raw = new byte[(stride + 1) * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            var row = y * (stride + 1);
            raw[row] = 0; // filter: none
            for (var x = 0; x < image.Width; x++)
            {
                var s = (y * image.Width + x) * 4;
                var d = row + 1 + x * bpp;
                raw[d] = image.Pixels[s];
                raw[d + 1] = image.Pixels[s + 1];
                raw[d + 2] = image.Pixels[s + 2];
                if (alpha)
                    raw[d + 3] = image.Pixels[s + 3];
            }
        }
        using var ms = new MemoryStream();
        ms.Write(Signature);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), image.Height);
        ihdr[8] = 8;
        ihdr[9] = (byte)(alpha ? 6 : 2);
        WriteChunk(ms, "IHDR", ihdr);
        using (var z = new MemoryStream())
        {
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true))
                zs.Write(raw);
            WriteChunk(ms, "IDAT", z.ToArray());
        }
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc(Crc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        Span<byte> c = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(c, crc);
        s.Write(c);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    public static RgbaImage Decode(byte[] png)
    {
        if (!png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("not a PNG");
        int width = 0, height = 0, depth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null, trns = null;
        using var idat = new MemoryStream();
        var pos = 8;
        while (pos + 8 <= png.Length)
        {
            var len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = png.AsSpan(pos + 8, len);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    depth = data[8];
                    colorType = data[9];
                    interlace = data[12];
                    break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": trns = data.ToArray(); break;
                case "IDAT": idat.Write(data); break;
            }
            pos += 12 + len;
            if (type == "IEND")
                break;
        }
        if (interlace != 0)
            throw new NotSupportedException("interlaced PNG");
        var channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"colour type {colorType}") };
        var bitsPerPixel = channels * depth;
        var stride = (width * bitsPerPixel + 7) / 8;
        var bpp = Math.Max(1, bitsPerPixel / 8);
        idat.Position = 0;
        byte[] raw;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        using (var outMs = new MemoryStream())
        {
            z.CopyTo(outMs);
            raw = outMs.ToArray();
        }
        var cur = new byte[stride];
        var prev = new byte[stride];
        var img = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            Array.Copy(raw, y * (stride + 1) + 1, cur, 0, stride);
            Unfilter(filter, cur, prev, bpp);
            for (var x = 0; x < width; x++)
            {
                byte r, g, b, a = 255;
                switch (colorType)
                {
                    case 0:
                    {
                        var v = Sample(cur, x, depth);
                        var g8 = Scale(v, depth);
                        r = g = b = g8;
                        if (trns is { Length: >= 2 } && v == BinaryPrimitives.ReadUInt16BigEndian(trns))
                            a = 0;
                        break;
                    }
                    case 2:
                        r = cur[x * 3]; g = cur[x * 3 + 1]; b = cur[x * 3 + 2];
                        if (trns is { Length: >= 6 } && r == trns[1] && g == trns[3] && b == trns[5])
                            a = 0;
                        break;
                    case 3:
                    {
                        var idx = Sample(cur, x, depth);
                        r = palette![idx * 3]; g = palette[idx * 3 + 1]; b = palette[idx * 3 + 2];
                        if (trns is not null && idx < trns.Length)
                            a = trns[idx];
                        break;
                    }
                    case 4:
                        r = g = b = cur[x * 2]; a = cur[x * 2 + 1];
                        break;
                    default:
                        r = cur[x * 4]; g = cur[x * 4 + 1]; b = cur[x * 4 + 2]; a = cur[x * 4 + 3];
                        break;
                }
                img.Set(x, y, r, g, b, a);
            }
            (prev, cur) = (cur, prev);
        }
        return img;
    }

    private static int Sample(byte[] row, int x, int depth)
    {
        if (depth == 8)
            return row[x];
        var perByte = 8 / depth;
        var b = row[x / perByte];
        var shift = 8 - depth * (x % perByte + 1);
        return (b >> shift) & ((1 << depth) - 1);
    }

    private static byte Scale(int v, int depth) => depth == 8 ? (byte)v : (byte)(v * 255 / ((1 << depth) - 1));

    private static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp)
    {
        for (var i = 0; i < cur.Length; i++)
        {
            int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
            cur[i] = filter switch
            {
                0 => cur[i],
                1 => (byte)(cur[i] + a),
                2 => (byte)(cur[i] + b),
                3 => (byte)(cur[i] + (a + b) / 2),
                4 => (byte)(cur[i] + Paeth(a, b, c)),
                _ => throw new InvalidDataException($"PNG filter {filter}"),
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
