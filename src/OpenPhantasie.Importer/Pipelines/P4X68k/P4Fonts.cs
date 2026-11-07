using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using static OpenPhantasie.Importer.Pipelines.P4X68k.P4Util;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// P4.X's own glyph tables (port of <c>tools/scripts/x68k_p4_fonts.py</c>, FORMAT.md 6.12): the pack's four
/// <c>phantasie.font/v1</c> files, their preview sheets, and font 2's 50 item icons.
/// </summary>
internal static class P4Fonts
{
    private static readonly Dictionary<int, int> Tables = new() { [0] = 0x36E8A, [1] = 0x37BB6 };
    private const int IconTable = 0x39902, IconCount = 50;
    private const int Italic = 2, Bold = 1;

    private static (uint[] Rows, int Advance) Glyph(byte[] img, int table, int b)
    {
        var p = (int)BinaryPrimitives.ReadUInt32BigEndian(img.AsSpan(table + 4 * (b - 0x20)));
        int perRow = img[p], rows = img[p + 1], advance = img[p + 2];
        if (perRow is not (1 or 2 or 4))
            throw new InvalidDataException($"glyph 0x{b:X2}: {perRow} bytes per row");
        var outRows = new uint[rows];
        for (var r = 0; r < rows; r++)
        {
            uint v = 0;
            for (var k = 0; k < perRow; k++)
                v = v << 8 | img[p + 4 + r * perRow + k];
            outRows[r] = perRow == 4 ? v : v << (32 - 8 * perRow);
        }
        return (outRows, advance);
    }

    private static (uint[] Rows, int Advance) Styled(uint[] rows, int advance, int style)
    {
        rows = (uint[])rows.Clone();
        if ((style & Bold) != 0)
        {
            for (var i = 0; i < rows.Length; i++)
                rows[i] |= rows[i] >> 1;
            advance++;
        }
        if ((style & Italic) != 0)
        {
            int d = rows.Length / 2, i = 0;
            while (d != 0)
            {
                for (var k = 0; k < 2; k++)
                {
                    if (i < rows.Length)
                        rows[i] >>= d;
                    i++;
                }
                d--;
            }
        }
        return (rows, advance);
    }

    private static int CodePoint(int b, int font)
    {
        if (b == 0x7F || (font == 0 && b is 0x7B or 0x7D))
            return 0xE000 + b;
        if (b is >= 0x20 and <= 0x7E)
            return b;
        if (b is >= 0xA1 and <= 0xDF)
            return 0xFF61 + b - 0xA1;
        return 0xE000 + b;
    }

    private static string KeyOf(int cp) => cp is >= 0x20 and <= 0x7E ? ((char)cp).ToString() : $"U+{cp:X4}";

    private static JsonObject BuildFont(byte[] img, int font, int style, string id, string note)
    {
        var table = Tables[font];
        int height = img[table + 896 + 2];
        var glyphs = new JsonObject();
        for (var b = 0x20; b < 0x100; b++)
        {
            var (rows, advance) = Glyph(img, table, b);
            (rows, advance) = Styled(rows, advance, style);
            if (b != 0x20 && rows.All(r => r == 0))
                continue;
            var width = Math.Max(rows.Where(r => r != 0).Select(r => 32 - BitOperations.TrailingZeroCount(r)).DefaultIfEmpty(0).Max(), 1);
            var text = rows.Select(r =>
            {
                var sb = new StringBuilder(width);
                for (var c = 0; c < width; c++)
                    sb.Append(((r >> (31 - c)) & 1) != 0 ? '#' : '.');
                return sb.ToString();
            }).ToList();
            while (text.Count < height)
                text.Add(new string('.', width));
            glyphs[KeyOf(CodePoint(b, font))] = Obj(("rows", Arr(text)), ("advance", advance));
        }
        return Obj(("_schema", "phantasie.font/v1"), ("_note", note), ("id", id), ("height", height), ("line_height", height),
            ("space_advance", glyphs[" "]!["advance"]!.DeepClone()), ("fallback", "?"), ("fold_to_upper", false), ("glyphs", glyphs));
    }

    private static readonly (int Font, int Style, string Id, string Extra)[] Jobs =
    [
        (0, 0, "x68k-p4-font0", "The window font (_font(0, 0, ...)): the Mystic's bubble, every window's ANK text."),
        (0, Italic, "x68k-p4-font0-italic",
            "Style 2 (_wfopt bit 1, italic: rows sheared right in pairs by rows/2 ... 1, advance unchanged): _dsp_exit's " +
            "'Exit' on the fight's exit square (_font(-1, 2, 15, 16)); matches the XEiJ shot s120 pixel for pixel."),
        (0, Bold, "x68k-p4-font0-bold",
            "Style 1 (_wfopt bit 0, bold: row |= row >> 1, advance + 1): _tload's town name on the street banner " +
            "(_font(0, 1, 15, 16)) and 'Congraturation!' after the ending; matches the XEiJ shot s135 (ending street). " +
            "_fsize centres with the plain advances (advance - 1 here), s136."),
        (1, 0, "x68k-p4-font1", "Font 1 (_font(1, ...)): the thin variant (_page1, _dsp_spell, _getmagic, _endstory, ...)."),
    ];

    /// <summary>The four font files by id, in the script's order.</summary>
    public static List<(string Id, JsonObject Font)> Fonts(byte[] img)
    {
        var list = new List<(string, JsonObject)>();
        foreach (var (font, style, id, extra) in Jobs)
        {
            var table = Tables[font];
            var common = $"Phantasie IV X68000, P4.X's own glyph table 0x{table:X5} (_wfinit({font}), FORMAT.md 6.12); extracted by " +
                         "tools/scripts/x68k_p4_fonts.py. Proportional: each glyph's pen advance is P4.X's. Rows are top-aligned " +
                         $"in the {img[table + 896 + 2]}-row cell as _wfnt draws them.";
            list.Add((id, BuildFont(img, font, style, id, common + " " + extra)));
        }
        return list;
    }

    /// <summary>The font's preview sheet (16 cells a row, 26 px wide, ink white on grey, advance line), scaled x3.</summary>
    public static RgbaImage Preview(JsonObject font)
    {
        var cells = ((JsonObject)font["glyphs"]!).Select(kv => kv.Value!).ToList();
        var h = (int)font["height"]!;
        const int cols = 16;
        int w = cols * 26, hh = (cells.Count + cols - 1) / cols * (h + 4);
        var img = new RgbaImage(w, hh);
        for (var y = 0; y < hh; y++)
            for (var x = 0; x < w; x++)
                img.Set(x, y, 40, 40, 40);
        for (var i = 0; i < cells.Count; i++)
        {
            int x0 = i % cols * 26 + 1, y0 = i / cols * (h + 4) + 1;
            var rows = cells[i]["rows"]!.AsArray();
            for (var y = 0; y < rows.Count; y++)
            {
                var row = (string)rows[y]!;
                for (var x = 0; x < row.Length; x++)
                    if (row[x] == '#')
                        img.Set(x0 + x, y0 + y, 255, 255, 255);
            }
            var adv = (int)cells[i]["advance"]!;
            for (var x = 0; x < adv; x++)
                img.Set(x0 + x, y0 + h + 1, 120, 120, 120);
        }
        var big = new RgbaImage(w * 3, hh * 3);
        for (var y = 0; y < hh * 3; y++)
            for (var x = 0; x < w * 3; x++)
            {
                var (r, g, b, a) = img.Get(x / 3, y / 3);
                big.Set(x, y, r, g, b, a);
            }
        return big;
    }

    /// <summary>Font 2's 32 x 32 glyphs 0x20 + b0 (ink black, transparent elsewhere), by b0.</summary>
    public static IEnumerable<RgbaImage> Icons(byte[] img)
    {
        for (var b0 = 0; b0 < IconCount; b0++)
        {
            var (rows, _) = Glyph(img, IconTable, 0x20 + b0);
            var pic = new RgbaImage(32, 32);
            for (var y = 0; y < rows.Length; y++)
                for (var x = 0; x < 32; x++)
                    if (((rows[y] >> (31 - x)) & 1) != 0)
                        pic.Set(x, y, 0, 0, 0);
            yield return pic;
        }
    }
}
