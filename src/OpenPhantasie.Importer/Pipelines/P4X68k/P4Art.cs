using System.Buffers.Binary;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// The X68000 art pack's PNGs (ports of <c>x68k_p4_pkh.py</c>, <c>x68k_p4_pix.py</c> and the PNG half of
/// <c>x68k_p4_fonts.py</c>) under <see cref="P4X68kPipeline.ArtRoot"/>.
/// </summary>
internal static class P4Art
{
    /// <summary>Each PKH picture's folder in the pack (the pack's own layout; every PKH on the two disks).</summary>
    private static readonly (string Folder, string[] Names)[] PkhFolders =
    [
        ("anim", ["BANK_AN", "END1ANI", "END2ANI", "END3ANI", "GUILD_AN", "INN_AN", "MYSAN", "SHIP_AN", "TOWN_1AN", "TOWN_2AN", "TOWN_3AN"]),
        ("buildings", ["BANK", "GUILD", "INN", "MYSTIC", "MYSTLK", "SHIP"]),
        ("ending", ["END1", "END2", "END3"]),
        ("hall", ["HALL_A", "HALL_B", "SC_A", "SC_B", "SC_C", "SC_D", "SC_E", "SC_F", "SC_G", "SC_H", "SC_I"]),
        ("outdoor", ["OT01", "OT02", "OT03", "OT04", "OT05", "OT06", "OT08", "OT09", "OT10", "OT13", "OT14", "OT15", "OT16", "OT17",
            "OT20", "OT21", "OT22", "OT23"]),
        ("scroll", ["SCBACK"]),
        ("title", ["OPEN", "OPENANI"]),
        ("town", ["TOWN_1", "TOWN_2", "TOWN_3"]),
    ];

    // (output folder, file, width, height, masked) as x68k_p4_pix.py's SHEETS
    private static readonly (string Folder, string File, int W, int H, bool Masked)[] Sheets =
    [
        ("dungeon/floor", "fl", 32, 32, false),
        ("dungeon/north", "yoko", 48, 32, true),
        ("dungeon/west", "tate", 16, 64, true),
        ("dungeon/party", "mn", 32, 32, true),
        ("combat/ground", "bback", 48, 40, false),
        ("combat/obstacle", "bobj", 48, 48, true),
        ("combat/missile", "fly", 32, 32, true),
        ("ui/icons", "icon", 32, 32, true),
    ];

    public static void Write(ImportContext ctx, P4Source src)
    {
        var root = P4X68kPipeline.ArtRoot;
        foreach (var (folder, names) in PkhFolders)
            foreach (var name in names)
                ctx.WritePng($"{root}/{folder}/{name}.png", Pkh(src.Read(name + ".PKH")).Image);

        // The dungeon and combat code set no palette: the shared PKH palette (OPEN.PKH's) is in effect.
        var pal = Pkh(src.Read("OPEN.PKH")).Palette;
        foreach (var (folder, file, w, h, masked) in Sheets)
        {
            var imgs = Sprites(src.Read(file + ".pix"), w, h, masked, pal);
            for (var i = 0; i < imgs.Count; i++)
                ctx.WritePng($"{root}/{folder}/{i:00}.png", imgs[i], forceAlpha: true);
        }
        var mon = src.Read("p4mon.pix");
        for (var r = 0; r < mon.Length / 0x1C20; r++)
            ctx.WritePng($"{root}/combat/sprites/{r:000}.png",
                Strip(Sprites(mon[(r * 0x1C20)..((r + 1) * 0x1C20)], 48, 48, true, pal)), forceAlpha: true);

        var img = src.X.Image;
        var b0 = 0;
        foreach (var icon in P4Fonts.Icons(img))
            ctx.WritePng($"{root}/ui/items/{b0++:00}.png", icon, forceAlpha: true);
        foreach (var (id, font) in P4Fonts.Fonts(img))
            ctx.WritePng($"{root}/fonts/{id}.png", P4Fonts.Preview(font));
    }

    // ---- PKH ---------------------------------------------------------------------------------------------------------

    public static (byte R, byte G, byte B)[] Palette(ReadOnlySpan<byte> words)
    {
        var outPal = new (byte, byte, byte)[16];
        for (var k = 0; k < 16; k++)
        {
            int w = BinaryPrimitives.ReadUInt16BigEndian(words[(2 * k)..]);
            int g = (w >> 11) & 31, r = (w >> 6) & 31, b = (w >> 1) & 31, i = w & 1;
            byte C(int c) => (byte)(((c << 1 | i) * 255 + 31) / 63);
            outPal[k] = (C(r), C(g), C(b));
        }
        return outPal;
    }

    /// <summary>The pixel words as _scsvb1 writes them.</summary>
    private static List<byte> Unpack(ReadOnlySpan<byte> data)
    {
        var px = new List<byte>();
        var p = 0;
        while (p < data.Length)
        {
            var c = data[p++];
            var n = c & 0x7F;
            if ((c & 0x80) != 0)
            {
                var b = data[p++];
                for (var k = 0; k < (n == 0 ? 256 : n); k++)
                    px.Add(b);
            }
            else
            {
                var k = n == 0 ? 256 : n;
                while (true)
                {
                    var b = data[p++];
                    px.Add((byte)(b >> 4));
                    if (--k == 0)
                        break;
                    px.Add(b);
                    if (--k == 0)
                        break;
                }
            }
        }
        return px;
    }

    public static (RgbaImage Image, (byte R, byte G, byte B)[] Palette) Pkh(byte[] raw)
    {
        if (raw.Length < 0x38 || !raw.AsSpan(0, 6).SequenceEqual("X68000"u8))
            throw new InvalidDataException("PKH: no X68000 signature");
        var pal = Palette(raw.AsSpan(0x0C, 32));
        int w = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(0x34)), h = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(0x36));
        var px = Unpack(raw.AsSpan(0x38));
        var img = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var v = i < px.Count ? px[i] & 15 : 0; // a short picture is padded with colour 0, as Pillow's putdata
            var (r, g, b) = pal[v];
            img.Set(i % w, i / w, r, g, b);
        }
        return (img, pal);
    }

    // ---- PIX ---------------------------------------------------------------------------------------------------------

    private static RgbaImage Sprite(ReadOnlySpan<byte> data, int w, int h, bool masked, (byte R, byte G, byte B)[] pal)
    {
        var img = new RgbaImage(w, h);
        var mask = masked ? data[..(w * h / 8)] : default;
        var px = masked ? data[(w * h / 8)..] : data;
        for (var i = 0; i < w * h; i++)
        {
            if (masked && ((mask[i / 8] >> (7 - i % 8)) & 1) != 0)
                continue;
            var b = px[i / 2];
            var (r, g, bl) = pal[i % 2 == 0 ? b >> 4 : b & 15];
            img.Set(i % w, i / w, r, g, bl);
        }
        return img;
    }

    private static List<RgbaImage> Sprites(byte[] raw, int w, int h, bool masked, (byte R, byte G, byte B)[] pal)
    {
        var size = w * h / 2 + (masked ? w * h / 8 : 0);
        var n = raw.Length / size;
        if (n * size != raw.Length)
            throw new InvalidDataException($"sprite file of {raw.Length} bytes is not a multiple of {size}");
        return Enumerable.Range(0, n).Select(i => Sprite(raw.AsSpan(i * size, size), w, h, masked, pal)).ToList();
    }

    private static RgbaImage Strip(List<RgbaImage> images)
    {
        var outImg = new RgbaImage(images.Sum(i => i.Width), images[0].Height);
        for (var k = 0; k < images.Count; k++)
        {
            var im = images[k];
            for (var y = 0; y < im.Height; y++)
                for (var x = 0; x < im.Width; x++)
                {
                    var (r, g, b, a) = im.Get(x, y);
                    outImg.Set(k * im.Width + x, y, r, g, b, a);
                }
        }
        return outImg;
    }
}
