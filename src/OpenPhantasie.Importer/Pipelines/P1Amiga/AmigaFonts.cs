using System.Buffers.Binary;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Port of <c>tools/scripts/extract_amiga_fonts.py</c>: the two fixed-width Amiga fonts I draws with, as
/// <c>phantasie.font/v1</c> files: z6font 6 from the game disk (<c>fonts/z6font/6</c>, a DiskFontHeader hunk) and topaz 8
/// from the Kickstart 1.3 (34.5) ROM (its InitStruct TextFont at ROM 0x910a). Plus a preview sheet of each.
/// </summary>
internal static class AmigaFonts
{
    public sealed record Font(int YSize, int XSize, int Baseline, int Lo, int Hi, SortedDictionary<int, string[]> Glyphs);

    private const uint RomBase = 0xFC0000;
    private const int Topaz8Init = 0x910A;

    private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
    private static int U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o));

    /// <summary>The single code / data hunk of a disk font file (HUNK_HEADER, one hunk; RELOC32 ignored).</summary>
    private static byte[] HunkCode(byte[] data)
    {
        if (U32(data, 0) != 0x3F3)
            throw new InvalidDataException("z6font: not a hunk file");
        var p = 4;
        while (U32(data, p) != 0)
            p += 4;
        p += 4;
        var first = (int)U32(data, p + 4);
        var last = (int)U32(data, p + 8);
        p += 12 + 4 * (last - first + 1);
        var kind = U32(data, p);
        var longs = (int)U32(data, p + 4);
        if ((kind & 0x3FFFFFFF) is not (0x3E9 or 0x3EA))
            throw new InvalidDataException("z6font: first hunk is not code / data");
        return data.AsSpan(p + 8, longs * 4).ToArray();
    }

    private static Font Decode(byte[] mem, int ysize, int xsize, int baseline, int lo, int hi, int chardata, int modulo, int charloc)
    {
        var glyphs = new SortedDictionary<int, string[]>();
        for (var i = 0; i < hi - lo + 2; i++)
        {
            var offset = U16(mem, charloc + 4 * i);
            var width = U16(mem, charloc + 4 * i + 2);
            if (width == 0)
                continue;
            var rows = new string[ysize];
            for (var y = 0; y < ysize; y++)
            {
                var row = chardata + y * modulo;
                var chars = new char[width];
                for (var x = 0; x < width; x++)
                    chars[x] = (mem[row + ((offset + x) >> 3)] & (0x80 >> ((offset + x) & 7))) != 0 ? '#' : '.';
                rows[y] = new string(chars);
            }
            glyphs[i <= hi - lo ? lo + i : -1] = rows;
        }
        return new Font(ysize, xsize, baseline, lo, hi, glyphs);
    }

    public static Font Z6Font(byte[] file)
    {
        var code = HunkCode(file);
        if (U16(code, 0x12) != 0x0F80)
            throw new InvalidDataException("z6font: no DiskFontHeader");
        const int tf = 0x3A;
        int ysize = U16(code, tf + 20), xsize = U16(code, tf + 24), baseline = U16(code, tf + 26);
        int lo = code[tf + 32], hi = code[tf + 33];
        int chardata = (int)U32(code, tf + 34), modulo = U16(code, tf + 38), charloc = (int)U32(code, tf + 40);
        return Decode(code, ysize, xsize, baseline, lo, hi, chardata, modulo, charloc);
    }

    /// <summary>Topaz 8 from a Kickstart 1.3 (34.5) ROM; null when the ROM is not that one.</summary>
    public static Font? Topaz8(byte[] rom)
    {
        const int p = Topaz8Init;
        if (rom.Length < 0x40000)
            return null;
        var name = (int)(U32(rom, p) - RomBase);
        if (name < 0 || name + 11 > rom.Length || !rom.AsSpan(name, 11).SequenceEqual("topaz.font\0"u8))
            return null;
        int ysize = U16(rom, p + 6), xsize = U16(rom, p + 10), baseline = U16(rom, p + 12);
        int lo = rom[p + 18], hi = rom[p + 19];
        int chardata = (int)(U32(rom, p + 20) - RomBase), modulo = U16(rom, p + 24), charloc = (int)(U32(rom, p + 26) - RomBase);
        return Decode(rom, ysize, xsize, baseline, lo, hi, chardata, modulo, charloc);
    }

    public static JsonObject ToFontJson(string id, Font font, string source)
    {
        var glyphs = new JsonObject();
        font.Glyphs.TryGetValue(-1, out var fallbackRows);
        foreach (var (code, rows) in font.Glyphs)
        {
            if (code < 0 || code is >= 0x7F and < 0xA0)
                continue;
            glyphs[((char)code).ToString()] = Glyph(rows, font.XSize);
        }
        var fallback = "?";
        var question = glyphs["?"]?["rows"] as JsonArray;
        if (fallbackRows is not null && (question is null || !question.Select(r => (string)r!).SequenceEqual(fallbackRows)))
        {
            fallback = "U+E000";
            glyphs[fallback] = Glyph(fallbackRows, font.XSize);
        }
        return new JsonObject
        {
            ["_schema"] = "phantasie.font/v1",
            ["_note"] = $"{source} tf_YSize {font.YSize}, tf_XSize {font.XSize}, tf_Baseline {font.Baseline}, " +
                        $"characters 0x{font.Lo:x2}-0x{font.Hi:x2}; extracted by tools/scripts/extract_amiga_fonts.py. " +
                        "Fixed width: each glyph is its CharLoc bits, the pen moves tf_XSize. The fallback is the font's " +
                        "default glyph (the entry after HiChar).",
            ["id"] = id,
            ["height"] = font.YSize,
            ["line_height"] = font.YSize,
            ["space_advance"] = font.XSize,
            ["fallback"] = fallback,
            ["fold_to_upper"] = false,
            ["glyphs"] = glyphs,
        };
    }

    private static JsonObject Glyph(string[] rows, int advance) => new()
    {
        ["rows"] = new JsonArray(rows.Select(r => (JsonNode)r!).ToArray()),
        ["advance"] = advance,
    };

    /// <summary>The preview sheet: every glyph the font has (DEL and C1 included, the default glyph not), 32 a row.</summary>
    public static RgbaImage Preview(Font font)
    {
        var codes = font.Glyphs.Keys.Where(c => c >= 0).ToList();
        const int perRow = 32;
        int cw = font.XSize + 2, ch = font.YSize + 2;
        var img = AmigaGfx.Filled(perRow * cw, (codes.Count + perRow - 1) / perRow * ch, new AmigaGfx.Rgba(0, 0, 0, 255));
        for (var n = 0; n < codes.Count; n++)
        {
            int ox = n % perRow * cw + 1, oy = n / perRow * ch + 1;
            var rows = font.Glyphs[codes[n]];
            for (var y = 0; y < rows.Length; y++)
                for (var x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#')
                        img.Set(ox + x, oy + y, 238, 238, 238);
        }
        return img;
    }
}
