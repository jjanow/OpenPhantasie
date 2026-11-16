namespace OpenPhantasie.Importer.Formats;

/// <summary>
/// Commodore 64 picture formats the SSI Phantasie ports share (s218; <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>):
/// the VIC-II palette, 8 × 8 hires characters, 24 × 21 hires sprites (with the VIC's 2× expansion), the multicolour
/// bitmap, and the <c>MX</c> sprite-band record (<see cref="SpriteBand"/>) that I's and II's monster files and I's hero
/// band start with. Generic: nothing here knows Phantasie I.
/// </summary>
public static class C64Gfx
{
    /// <summary>
    /// The VIC-II's 16 colours as Colodore (Philip "Pepto" Timmermann's 2017 measurement, colodore.com; the values of
    /// VICE 3.10's <c>colodore.vpl</c>, PAL), in VIC order: black, white, red, cyan, purple, green, blue, yellow, orange,
    /// brown, light red, dark grey, grey, light green, light blue, light grey.
    /// </summary>
    public static readonly (byte R, byte G, byte B)[] Colodore =
    [
        (0x00, 0x00, 0x00), (0xFF, 0xFF, 0xFF), (0x96, 0x28, 0x2E), (0x5B, 0xD6, 0xCE),
        (0x9F, 0x2D, 0xAD), (0x41, 0xB9, 0x36), (0x27, 0x24, 0xC4), (0xEF, 0xF3, 0x47),
        (0x9F, 0x48, 0x15), (0x5E, 0x35, 0x00), (0xDA, 0x5F, 0x66), (0x47, 0x47, 0x47),
        (0x78, 0x78, 0x78), (0x91, 0xFF, 0x84), (0x68, 0x64, 0xFF), (0xAE, 0xAE, 0xAE),
    ];

    /// <summary>The palette as <c>#RRGGBB</c> strings (a pack's <c>palette</c>).</summary>
    public static IEnumerable<string> ColodoreHex => Colodore.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}");

    /// <summary>Put VIC colour <paramref name="colour"/> (low nibble) at (x, y), opaque.</summary>
    public static void Plot(RgbaImage img, int x, int y, int colour)
    {
        var (r, g, b) = Colodore[colour & 15];
        img.Set(x, y, r, g, b);
    }

    /// <summary>
    /// A hires character (8 bytes, bit 7 leftmost) at (x, y): set bits in <paramref name="fg"/>, clear bits in
    /// <paramref name="bg"/> or left untouched when <paramref name="bg"/> is null (transparent).
    /// </summary>
    public static void DrawChar(RgbaImage img, int x, int y, ReadOnlySpan<byte> glyph, int fg, int? bg)
    {
        for (var row = 0; row < 8; row++)
        for (var bit = 0; bit < 8; bit++)
        {
            if ((glyph[row] & (0x80 >> bit)) != 0)
                Plot(img, x + bit, y + row, fg);
            else if (bg is { } b)
                Plot(img, x + bit, y + row, b);
        }
    }

    /// <summary>One character picture: <paramref name="glyph"/> in <paramref name="fg"/> on <paramref name="bg"/> (null: transparent).</summary>
    public static RgbaImage Char(ReadOnlySpan<byte> glyph, int fg, int? bg)
    {
        var img = new RgbaImage(8, 8);
        DrawChar(img, 0, 0, glyph, fg, bg);
        return img;
    }

    public const int SpriteWidth = 24, SpriteHeight = 21, SpriteBlock = 64;

    /// <summary>
    /// A hires sprite (63 bytes, 3 per row, bit 7 leftmost) at (x, y) in <paramref name="colour"/>; clear bits are
    /// transparent. Expanded axes draw every pixel twice, as the VIC does (<c>$D01D</c> x, <c>$D017</c> y).
    /// </summary>
    public static void DrawSprite(RgbaImage img, int x, int y, ReadOnlySpan<byte> data, int colour, bool xExpand = false,
        bool yExpand = false)
    {
        var sx = xExpand ? 2 : 1;
        var sy = yExpand ? 2 : 1;
        for (var row = 0; row < SpriteHeight; row++)
        for (var col = 0; col < SpriteWidth; col++)
        {
            if ((data[row * 3 + col / 8] & (0x80 >> (col % 8))) == 0)
                continue;
            for (var dy = 0; dy < sy; dy++)
            for (var dx = 0; dx < sx; dx++)
            {
                var px = x + col * sx + dx;
                var py = y + row * sy + dy;
                if (px >= 0 && py >= 0 && px < img.Width && py < img.Height)
                    Plot(img, px, py, colour);
            }
        }
    }

    /// <summary>One sprite as a 24 × 21 picture (transparent ground).</summary>
    public static RgbaImage Sprite(ReadOnlySpan<byte> data, int colour)
    {
        var img = new RgbaImage(SpriteWidth, SpriteHeight);
        DrawSprite(img, 0, 0, data, colour);
        return img;
    }

    /// <summary>
    /// A multicolour bitmap screen (VIC bitmap mode with <c>$D016</c> bit 4): 8,000 bitmap bytes (40 × 25 cells of 8 bytes),
    /// 1,000 screen bytes, 1,000 colour-RAM bytes and the background (<c>$D021</c>). Each cell's bit pairs pick 00
    /// background, 01 the screen byte's high nibble, 10 its low nibble, 11 the colour-RAM nibble; a pair is two pixels
    /// wide, so the picture is 320 × 200 with doubled pixels (160 × 200 of resolution).
    /// </summary>
    public static RgbaImage MulticolourBitmap(ReadOnlySpan<byte> bitmap, ReadOnlySpan<byte> screen, ReadOnlySpan<byte> colour,
        int background)
    {
        var img = new RgbaImage(320, 200);
        for (var cell = 0; cell < 1000; cell++)
        {
            int cx = cell % 40 * 8, cy = cell / 40 * 8;
            for (var row = 0; row < 8; row++)
            {
                var b = bitmap[cell * 8 + row];
                for (var pair = 0; pair < 4; pair++)
                {
                    var c = ((b >> (6 - 2 * pair)) & 3) switch
                    {
                        0 => background,
                        1 => screen[cell] >> 4,
                        2 => screen[cell] & 15,
                        _ => colour[cell] & 15,
                    };
                    Plot(img, cx + 2 * pair, cy + row, c);
                    Plot(img, cx + 2 * pair + 1, cy + row, c);
                }
            }
        }
        return img;
    }

    /// <summary>The bounding box (x, y, w, h) of the non-transparent pixels, or null for an empty picture.</summary>
    public static (int X, int Y, int W, int H)? Bounds(RgbaImage img)
    {
        int x0 = img.Width, y0 = img.Height, x1 = -1, y1 = -1;
        for (var y = 0; y < img.Height; y++)
        for (var x = 0; x < img.Width; x++)
            if (img.Pixels[(y * img.Width + x) * 4 + 3] != 0)
            {
                x0 = Math.Min(x0, x);
                y0 = Math.Min(y0, y);
                x1 = Math.Max(x1, x);
                y1 = Math.Max(y1, y);
            }
        return x1 < 0 ? null : (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>A copy of the rectangle (x, y, w, h) of <paramref name="img"/>.</summary>
    public static RgbaImage Crop(RgbaImage img, int x, int y, int w, int h)
    {
        var o = new RgbaImage(w, h);
        for (var row = 0; row < h; row++)
            Array.Copy(img.Pixels, ((y + row) * img.Width + x) * 4, o.Pixels, row * w * 4, w * 4);
        return o;
    }
}

/// <summary>
/// The 38-byte sprite-band record at the head of an <c>MX n</c> monster file (and of <c>FONT+HER</c>, I's hero band): one
/// band of up to 8 VIC sprites as the raster interrupt of <c>UTIL-3.O</c> (<c>$C529</c>) loads it into the VIC
/// (<c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> section 2) [C]. The sprite blocks follow the record (block 0 at
/// record + 38). Bytes 0, 25 and 37 are set at run time (band y, block base, next raster line); the file's values are
/// templates.
/// </summary>
public sealed record SpriteBand(
    int BaseY, (int X, int Y)[] Positions, int[] Colours, int PointerBase, int[] Blocks,
    int XExpand, int YExpand, int Enable, int NextRaster)
{
    public const int Size = 38;

    public static SpriteBand Parse(ReadOnlySpan<byte> r)
    {
        if (r.Length < Size)
            throw new InvalidDataException("sprite band: shorter than 38 bytes");
        var pos = new (int, int)[8];
        for (var i = 0; i < 8; i++)
            pos[i] = (r[1 + 2 * i], r[2 + 2 * i]);
        return new SpriteBand(r[0], pos, r.Slice(17, 8).ToArray().Select(b => b & 15).ToArray(), r[25],
            r.Slice(26, 8).ToArray().Select(b => (int)b).ToArray(), r[34], r[35], r[36], r[37]);
    }

    public bool Enabled(int sprite) => (Enable & (1 << sprite)) != 0;

    /// <summary>
    /// How many sprites make one figure: the first sprites up to the first repeated block index (and no more than the
    /// enabled ones) [~] (GRAPHICS.md 2.2: a group repeats the figure's blocks for every monster).
    /// </summary>
    public int PartsPerFigure
    {
        get
        {
            var seen = new HashSet<int>();
            var n = 0;
            for (var i = 0; i < 8 && Enabled(i); i++)
            {
                if (!seen.Add(Blocks[i]))
                    break;
                n++;
            }
            return Math.Max(1, n);
        }
    }

    /// <summary>
    /// The first figure of the band drawn as the VIC shows it, cut to its sprites' box: sprites 0 ..
    /// <see cref="PartsPerFigure"/> − 1 at their (x, y), expanded as their bits say, each showing block
    /// <c>Blocks[i]</c> of <paramref name="blocks"/> in its colour; <paramref name="replace"/> shows another block in one
    /// sprite (the attacking frame).
    /// </summary>
    public RgbaImage Figure(ReadOnlySpan<byte> blocks, (int Sprite, int Block)? replace = null)
    {
        var parts = PartsPerFigure;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        for (var i = 0; i < parts; i++)
        {
            var (w, h) = Size2(i);
            x0 = Math.Min(x0, Positions[i].X);
            y0 = Math.Min(y0, Positions[i].Y);
            x1 = Math.Max(x1, Positions[i].X + w);
            y1 = Math.Max(y1, Positions[i].Y + h);
        }
        var img = new RgbaImage(x1 - x0, y1 - y0);
        for (var i = 0; i < parts; i++)
        {
            var block = replace is { } r && r.Sprite == i ? r.Block : Blocks[i];
            if (block < 0 || block * C64Gfx.SpriteBlock + 63 > blocks.Length)
                throw new InvalidDataException($"sprite band: block {block} past the file's sprite data");
            C64Gfx.DrawSprite(img, Positions[i].X - x0, Positions[i].Y - y0,
                blocks.Slice(block * C64Gfx.SpriteBlock, 63), Colours[i], (XExpand & (1 << i)) != 0, (YExpand & (1 << i)) != 0);
        }
        return img;
    }

    private (int W, int H) Size2(int i) =>
        ((XExpand & (1 << i)) != 0 ? 48 : 24, (YExpand & (1 << i)) != 0 ? 42 : 21);
}
