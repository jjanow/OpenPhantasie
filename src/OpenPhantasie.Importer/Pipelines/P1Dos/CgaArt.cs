using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// The DOS CGA art pack of I: map tiles (<c>extract_dos_map_tiles.py</c>), the town picture
/// (<c>extract_dos_town_picture.py</c>) and the party-defeat pictures (<c>extract_dos_defeat_pictures.py</c>, s157).
/// Paths are relative to the pack root (<c>graphics/dos-cga.json</c> root).
/// </summary>
internal static class CgaArt
{
    /// <summary>CGA palette 1 high intensity as DOSBox shows it (capture s10).</summary>
    private static readonly (byte R, byte G, byte B)[] Palette1 = [(0, 0, 0), (0x55, 0xFF, 0xFF), (0xFF, 0x55, 0xFF), (0xFF, 0xFF, 0xFF)];

    /// <summary>CGA palette 0 high, which OV1 0x2c7 sets for the defeat screens.</summary>
    private static readonly (byte R, byte G, byte B)[] Palette0 = [(0, 0, 0), (0x55, 0xFF, 0x55), (0xFF, 0x55, 0x55), (0xFF, 0xFF, 0x55)];

    private const int OwW = 12, OwH = 10, OwBytes = 30, OwPatterns = 15;
    private const int FogOffset = 0x7E2E;            // OV2 file offset of the 3-byte fog row
    private const int GlyphOffset = 0xC09A - 0x433D; // OV2 file 0x7d5d
    private const int GlyphW = 5, GlyphH = 4;

    private static readonly string[] PatternNames =
    [
        "class0", "class1", "class2", "class3", "town", "dungeon", "class6-path", "class7-bridge", "class8", "class9",
        "water", "pool", "party", "inn", "realm",
    ];

    private static readonly string[] GlyphNames = ["wall", "floor", "event", "lock-v", "lock-h", "exit", "stairs", "party"];

    /// <summary>2-bpp CGA rows (4 pixels a byte, high bits first) to an RGBA image.</summary>
    private static RgbaImage CgaRows(IReadOnlyList<byte[]> rows, int width, (byte R, byte G, byte B)[] palette)
    {
        var im = new RgbaImage(width, rows.Count);
        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < width; x++)
            {
                var c = palette[(rows[y][x / 4] >> (6 - 2 * (x % 4))) & 3];
                im.Set(x, y, c.R, c.G, c.B);
            }
        return im;
    }

    private static string Hex(this (byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>tiles/: 15 overworld patterns, the fog cell, 8 dungeon glyphs, and tiles.json.</summary>
    public static (Dictionary<string, RgbaImage> Images, JsonObject Meta) Tiles(byte[] pat, byte[] ov2)
    {
        if (pat.Length != OwPatterns * OwBytes)
            throw Py.Bad($"PAT: expected {OwPatterns * OwBytes} bytes, got {pat.Length}");
        var images = new Dictionary<string, RgbaImage>(StringComparer.Ordinal);
        var patterns = new JsonArray();
        for (var p = 0; p < OwPatterns; p++)
        {
            var rows = Enumerable.Range(0, OwH).Select(r => pat.AsSpan(p * OwBytes + r * 3, 3).ToArray()).ToList();
            var name = $"overworld/pat-{p:00}-{PatternNames[p]}.png";
            images[name] = CgaRows(rows, OwW, Palette1);
            patterns.Add(new JsonObject { ["index"] = p, ["image"] = name });
        }
        var fog = ov2.AsSpan(FogOffset, 3).ToArray();
        images["overworld/fog.png"] = CgaRows(Enumerable.Repeat(fog, OwH).ToList(), OwW, Palette1);

        var glyphs = new JsonArray();
        for (var i = 0; i < GlyphNames.Length; i++)
        {
            var gid = 0xE6 + i;
            var px = ov2.AsSpan(GlyphOffset + i * 20, 20);
            var im = new RgbaImage(GlyphW, GlyphH);
            for (var k = 0; k < px.Length; k++)
            {
                if (px[k] >= 4)
                    throw Py.Bad($"OV2.OVR: dungeon glyph {gid:x2} pixel {k} = {px[k]}");
                var c = Palette1[px[k]];
                im.Set(k % GlyphW, k / GlyphW, c.R, c.G, c.B);
            }
            var name = $"dungeon/glyph-{gid:x2}-{GlyphNames[i]}.png";
            images[name] = im;
            glyphs.Add(new JsonObject { ["glyph"] = gid, ["image"] = name });
        }

        var meta = new JsonObject
        {
            ["_source"] = "DOS PAT (450 B) + OV2.OVR fog row 0x7e2e + glyph table 0x7d5d; " +
                          "see logic-specs/map-tile-graphics.md",
            ["palette"] = Py.Strings(Palette1.Select(c => c.Hex())),
            ["overworld"] = new JsonObject
            {
                ["tile_width"] = OwW, ["tile_height"] = OwH, ["fog_image"] = "overworld/fog.png", ["patterns"] = patterns,
            },
            ["dungeon"] = new JsonObject { ["glyph_width"] = GlyphW, ["glyph_height"] = GlyphH, ["glyphs"] = glyphs },
        };
        return (images, meta);
    }

    private const int ScreenW = 320, ScreenH = 200, RowBytes = 80;

    /// <summary>A CGA screen dump: even rows at 0x0000, odd rows at 0x2000, 80 bytes a row.</summary>
    private static List<byte[]> ScreenRows(byte[] screen) =>
        Enumerable.Range(0, ScreenH)
            .Select(y => Py.Slice(screen, (y & 1) * 0x2000 + (y >> 1) * RowBytes, RowBytes))
            .ToList();

    /// <summary>ui/town.png from PELNOR.IBM (every town shows this one picture).</summary>
    public static RgbaImage Town(byte[] pelnor)
    {
        if (pelnor.Length != 16384)
            throw Py.Bad($"PELNOR.IBM: expected 16384 bytes, got {pelnor.Length}");
        return CgaRows(ScreenRows(pelnor), ScreenW, Palette1);
    }

    /// <summary>COM 0x544: bytes copied; <c>0, n</c> = n + 1 zeros.</summary>
    private static byte[] UnRle(byte[] src)
    {
        var output = new List<byte>(16384);
        var i = 0;
        while (i < src.Length)
        {
            var b = src[i++];
            output.Add(b);
            if (b == 0 && i < src.Length)
            {
                for (var k = 0; k < src[i]; k++)
                    output.Add(0);
                i++;
            }
        }
        return output.ToArray();
    }

    private static RgbaImage Sprite(ReadOnlySpan<byte> data)
    {
        if (data.Length != 256)
            throw Py.Bad($"defeat sprite: {data.Length} bytes, expected 256");
        var rows = new List<byte[]>(32);
        for (var r = 0; r < 32; r++)
            rows.Add(data.Slice(r * 8, 8).ToArray());
        return CgaRows(rows, 32, Palette0);
    }

    private const int Doorway = 0x2A0; // OV1 0x2fd: bank offset of the blank strip (bank row 8, byte 32)
    private const int Strip = 0x12;
    private const int Odd = 0x2000;
    private const int GhostOffset = 0x5A23; // OV1 file offset of DS 0xba1a

    /// <summary>
    /// ui/defeat-*.png (endgame.md section 4): the temple with the face (CEMEN.IBM unpacked), the temple with the
    /// doorway blanked as OV1 0x2fd..0x378 does it (the 0x31f quirk included), the ghost (OV1) and the 16 race figures
    /// (PRT1IBM.PAT), in CGA palette 0 high.
    /// </summary>
    public static Dictionary<string, RgbaImage> Defeat(byte[] cemen, byte[] ov1, byte[] prt1)
    {
        var demon = UnRle(cemen);
        if (demon.Length != 16384)
            throw Py.Bad($"CEMEN.IBM: unpacked to {demon.Length} bytes, expected 16384");
        var temple = (byte[])demon.Clone();
        var off = Doorway;
        while (off < Doorway + Strip)
            temple[off++] = 0;
        Array.Copy(temple, off, temple, Odd + off, Strip);
        off = 0x2F0;
        while (off < 0x1100)
        {
            Array.Copy(temple, Doorway, temple, off, Strip);
            Array.Copy(temple, Doorway, temple, Odd + off, Strip);
            off += RowBytes;
        }
        if (prt1.Length != 4096)
            throw Py.Bad($"PRT1IBM.PAT: expected 4096 bytes, got {prt1.Length}");
        var images = new Dictionary<string, RgbaImage>(StringComparer.Ordinal)
        {
            ["defeat-demon.png"] = CgaRows(ScreenRows(demon), ScreenW, Palette0),
            ["defeat-temple.png"] = CgaRows(ScreenRows(temple), ScreenW, Palette0),
            ["defeat-ghost.png"] = Sprite(ov1.AsSpan(GhostOffset, 256)),
        };
        for (var race = 0; race < 16; race++)
            images[$"defeat-figures/{race:00}.png"] = Sprite(prt1.AsSpan(race * 256, 256));
        return images;
    }
}
