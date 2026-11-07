using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>Applesoft hi-res page model (port of <c>extract_a2_p2_graphics.py</c> <c>HiresPage</c>).</summary>
internal sealed class HiresPage
{
    // HCOLOR= n -> (byte on an even byte column, byte on an odd byte column).
    private static readonly (int Even, int Odd)[] HcolorBytes =
        [(0x00, 0x00), (0x2A, 0x55), (0x55, 0x2A), (0x7F, 0x7F), (0x80, 0x80), (0xAA, 0xD5), (0xD5, 0xAA), (0xFF, 0xFF)];

    private static readonly (int X, int Y)[] Steps = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    public HiresPage(int cols = 40, int rows = 192)
    {
        Cols = cols;
        Rows = rows;
        Mem = new byte[rows][];
        for (var y = 0; y < rows; y++)
            Mem[y] = new byte[cols];
    }

    public int Cols { get; }
    public int Rows { get; }
    public byte[][] Mem { get; }

    public static HiresPage FromApple(byte[] data)
    {
        var page = new HiresPage();
        for (var y = 0; y < 192; y++)
        {
            var b = ((y & 7) << 10) | (((y >> 3) & 7) << 7) | ((y >> 6) * 40);
            data.AsSpan(b, 40).CopyTo(page.Mem[y]);
        }
        return page;
    }

    public void PokeApple(int offset, int value)
    {
        var off = offset & 0x1FFF;
        int yLo = off >> 10, rest = off & 0x3FF;
        int yMid = rest >> 7;
        rest &= 0x7F;
        if (rest >= 120)
            return;
        int third = rest / 40, col = rest % 40;
        Mem[third * 64 + yMid * 8 + yLo][col] = (byte)value;
    }

    public void Plot(int x, int y, int hcolor)
    {
        if (!(y >= 0 && y < Rows && x >= 0 && x < Cols * 7))
            return;
        int col = x / 7, bit = x % 7;
        var mask = 0x80 | (1 << bit);
        var row = Mem[y];
        var colour = (col & 1) == 0 ? HcolorBytes[hcolor].Even : HcolorBytes[hcolor].Odd;
        row[col] = (byte)((row[col] & ~mask & 0xFF) | (colour & mask));
    }

    public void Draw(List<(int Dir, bool Plot)> vectors, int x, int y, int hcolor, int scale)
    {
        foreach (var (dir, plot) in vectors)
        {
            var (dx, dy) = Steps[dir];
            for (var i = 0; i < (scale == 0 ? 256 : scale); i++)
            {
                if (plot)
                    Plot(x, y, hcolor);
                x += dx;
                y += dy;
            }
        }
    }

    private static readonly Dictionary<string, (byte R, byte G, byte B)> Rgb = new()
    {
        ["black"] = (0, 0, 0), ["white"] = (255, 255, 255), ["violet"] = (255, 68, 253), ["green"] = (20, 245, 60),
        ["blue"] = (20, 207, 253), ["orange"] = (255, 106, 60),
    };

    private static string Artifact(int x, bool high) => high ? ((x & 1) != 0 ? "orange" : "blue") : ((x & 1) != 0 ? "green" : "violet");

    /// <summary>The box (x0, y0, x1, y1) as RGBA; black is transparent when <paramref name="transparentBlack"/>.</summary>
    public RgbaImage ToImage(int x0, int y0, int x1, int y1, bool transparentBlack)
    {
        var width = Cols * 7;
        var img = new RgbaImage(x1 - x0, y1 - y0);
        var zeros = new byte[Cols];
        var bits = new int[width];
        var high = new int[width];
        for (var y = y0; y < y1; y++)
        {
            var row = y >= 0 && y < Rows ? Mem[y] : zeros;
            for (var x = 0; x < width; x++)
            {
                bits[x] = (row[x / 7] >> (x % 7)) & 1;
                high[x] = row[x / 7] >> 7;
            }
            for (var x = x0; x < x1; x++)
            {
                string name;
                if (x < 0 || x >= width)
                    name = "black";
                else
                {
                    var left = x > 0 ? bits[x - 1] : 0;
                    var right = x + 1 < width ? bits[x + 1] : 0;
                    if (bits[x] != 0)
                        name = left != 0 || right != 0 ? "white" : Artifact(x, high[x] != 0);
                    else if (left != 0 && right != 0)
                    {
                        var cLeft = Artifact(x - 1, high[x - 1] != 0);
                        var loneLeft = (x > 1 ? bits[x - 2] : 0) == 0;
                        var loneRight = (x + 2 < width ? bits[x + 2] : 0) == 0;
                        name = loneLeft && loneRight ? cLeft : "black";
                    }
                    else
                        name = "black";
                }
                var (r, g, b) = Rgb[name];
                img.Set(x - x0, y - y0, r, g, b, (byte)(transparentBlack && name == "black" ? 0 : 255));
            }
        }
        return img;
    }
}

/// <summary>Applesoft shape table (byte 0 = count, then u16 LE offsets from the table start).</summary>
internal sealed class ShapeTable
{
    private readonly byte[] _data;
    private readonly int[] _offsets;

    public ShapeTable(byte[] data)
    {
        _data = data;
        Count = data[0];
        _offsets = Enumerable.Range(0, Count).Select(i => data[2 + 2 * i] | data[3 + 2 * i] << 8).ToArray();
    }

    public int Count { get; }

    /// <summary>Shape n (1-based) -> (direction, plot) vectors.</summary>
    public List<(int Dir, bool Plot)> Vectors(int n)
    {
        var p = _offsets[n - 1];
        var o = new List<(int, bool)>();
        while (p < _data.Length)
        {
            int b = _data[p++];
            if (b == 0)
                break;
            o.Add((b & 3, (b & 4) != 0));
            b >>= 3;
            if (b == 0)
                continue;
            o.Add((b & 3, (b & 4) != 0));
            b >>= 3;
            if (b == 0)
                continue;
            o.Add((b & 3, false));
        }
        return o;
    }

    public static (int X0, int Y0, int X1, int Y1)? BBox(List<(int Dir, bool Plot)> vectors, int scale)
    {
        int x = 0, y = 0;
        int? minX = null, minY = null, maxX = null, maxY = null;
        foreach (var (dir, plot) in vectors)
        {
            var (dx, dy) = dir switch { 0 => (0, -1), 1 => (1, 0), 2 => (0, 1), _ => (-1, 0) };
            for (var i = 0; i < scale; i++)
            {
                if (plot)
                {
                    minX = Math.Min(minX ?? x, x);
                    minY = Math.Min(minY ?? y, y);
                    maxX = Math.Max(maxX ?? x, x);
                    maxY = Math.Max(maxY ?? y, y);
                }
                x += dx;
                y += dy;
            }
        }
        return minX is null ? null : (minX.Value, minY!.Value, maxX!.Value, maxY!.Value);
    }
}

/// <summary>
/// The Apple II art of Phantasie II (port of <c>extract_a2_p2_graphics.py</c>): every shape of <c>SHAPES</c>, the 80
/// monsters in both poses, the party figures and portraits, the overworld tiles, the dungeon marks, three effects and
/// the title pictures, plus <c>extraction-index.json</c>.
/// </summary>
internal sealed class A2Graphics
{
    public const string Root = "reverse-engineering/apple2/phantasie2/extracted/graphics";
    private const int PadX = 140, PadY = 200;

    private static readonly string[] Terrains = ["GRASS", "FOREST", "HILLS", "MOUNTAINS", "BUILDINGS", "DUNGEON", "PATHWAY", "NETHERWORLD", "LAVA",
        "DESERT", "WATER", "MIST", "ASTRAL PLANE"];
    private static readonly string[] Races = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
        "GOBLIN", "LIZARD MAN", "MINOTAUR", "UNDEAD"];
    private static readonly (int Shape, string Label)[] Bodies = [(19, "human-elf-fighter"), (25, "small-races"), (26, "monster-races"), (27, "human-elf-robed")];
    private static readonly (int Shape, string Use)[] DungeonMarks =
        [(31, "wall"), (32, "cells-185-199"), (33, "cells-200-214"), (34, "cells-215-217"), (35, "cells-218-220"), (36, "cells-221-up"), (30, "party-cursor")];

    private readonly ImportContext? _ctx;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public A2Graphics(ImportContext? ctx) => _ctx = ctx;

    public JsonObject Index { get; private set; } = new();

    /// <summary>Written files (path under the root -> PNG bytes); also sent to the context when there is one.</summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    private void Save(string rel, RgbaImage img)
    {
        var bytes = Png.Encode(img, forceAlpha: true);
        _files[rel] = bytes;
        _ctx?.WriteFile($"{Root}/{rel}", bytes);
    }

    private JsonObject Render(ShapeTable table, (int Shape, int Hcolor)[] parts, int x, int scale, string rel, (int W, int H)? cell = null)
    {
        var page = new HiresPage(80, 400);
        var boxes = new List<(int X0, int Y0, int X1, int Y1)>();
        foreach (var (shape, hcolor) in parts)
        {
            var vec = table.Vectors(shape);
            page.Draw(vec, PadX + x, PadY, hcolor, scale);
            if (ShapeTable.BBox(vec, scale) is { } b)
                boxes.Add(b);
        }
        int x0, y0, x1, y1;
        if (cell is not null || boxes.Count == 0)
        {
            var (w, h) = cell ?? (1, 1);
            (x0, y0, x1, y1) = (PadX + x, PadY, PadX + x + w, PadY + h);
        }
        else
        {
            x0 = PadX + x + boxes.Min(b => b.X0) - 1;
            y0 = PadY + boxes.Min(b => b.Y0);
            x1 = PadX + x + boxes.Max(b => b.X1) + 2;
            y1 = PadY + boxes.Max(b => b.Y1) + 1;
        }
        var img = page.ToImage(x0, y0, x1, y1, transparentBlack: true);
        Save(rel, img);
        return J.O(("file", rel), ("size", J.Ints([img.Width, img.Height])), ("origin", J.Ints([PadX + x - x0, PadY - y0])),
            ("shapes", J.Ints(parts.Select(p => p.Shape))), ("hcolors", J.Ints(parts.Select(p => p.Hcolor))), ("scale", scale),
            ("screen_x", x));
    }

    private JsonObject Blank(string rel, int w, int h)
    {
        var img = new RgbaImage(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                img.Set(x, y, 0, 0, 0, 255);
        Save(rel, img);
        return J.O(("file", rel), ("size", J.Ints([w, h])));
    }

    private static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }

    private static int PartyBodyShape(int race, int cls) =>
        race > 6 ? 26 : race is not (0 or 2) ? 25 : cls == 1 || cls > 3 ? 27 : 19;

    public void Run(A2Source src)
    {
        var table = new ShapeTable(src.A("SHAPES"));
        JsonObject shapes = new(), monsters = new(), figures = new(), portraits = new(), tiles = new(), dungeon = new(),
            effects = new(), screens = new();
        for (var n = 1; n <= table.Count; n++)
            shapes[n.ToString()] = Render(table, [(n, 3)], 0, 1, $"shapes/{n:D2}.png");

        var recs = A2Tables.MonsterRecords(src);
        for (var i = 0; i < recs.Count; i++)
        {
            var rec = recs[i];
            var name = Encoding.ASCII.GetString(rec[0]);
            int Field(string f) => int.Parse(Encoding.ASCII.GetString(rec[Array.IndexOf(A2Tables.MonsterFields, f)]).Trim(),
                System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture);
            int s = Field("a2_shape"), c = Field("a2_scale"), h = Field("a2_hcolor");
            var file = $"{i:D2}-{Slug(name)}.png";
            var idle = Render(table, [(s, h)], h, c, $"monsters/idle/{file}");
            var act = Render(table, [(s + 1, h)], h, c, $"monsters/attacking/{file}");
            monsters[i.ToString()] = J.O(("name", name), ("shape", s), ("scale", c), ("hcolor", h),
                ("group_height", Field("a2_group_height")), ("idle", idle), ("attacking", act));
        }

        foreach (var (body, label) in Bodies)
            foreach (var (overlay, pose) in new[] { (20, "idle"), (22, "attacking"), (21, "casting") })
                figures[$"{body}.{pose}"] = Render(table, [(body, 3), (overlay, 3)], 0, 1, $"figures/{pose}/{body}-{label}.png");
        for (var race = 0; race < Races.Length; race++)
        {
            var body = PartyBodyShape(race, 3);
            foreach (var (overlay, pose) in new[] { (20, "idle"), (22, "attacking") })
            {
                var e = Render(table, [(body, 3), (overlay, 3)], 0, 1, $"portraits/{pose}/{race:D2}-{Slug(Races[race])}.png");
                e["body"] = body;
                portraits[$"{race}.{pose}"] = e;
            }
        }

        for (var t = 0; t < 12; t++)
        {
            var hc = (int)(1 + 0.51 * t);
            var slug = Slug(Terrains[t]);
            tiles[$"class.{t}.odd"] = Render(table, [(37 + t, hc)], 147, 1, $"overworld/odd/{t:D2}-{slug}.png", (7, 6));
            for (var b = t * 10; b < t * 10 + 10; b++)
                tiles[b.ToString()] = Render(table, [(37 + t, hc)], 140, 1, $"overworld/tiles/{b:D3}.{slug}.png", (7, 6));
        }
        for (var b = 120; b < 256; b++)
            tiles[b.ToString()] = Blank($"overworld/tiles/{b:D3}.fog.png", 7, 6);
        tiles["party"] = Render(table, [(43, 3)], 140, 1, "overworld/party.png", (7, 6));

        foreach (var (n, what) in DungeonMarks)
        {
            var e = Render(table, [(n, 3)], 144, 1, $"dungeon/glyph-{n}.{what}.png", (4, 4));
            e["use"] = what;
            dungeon[n.ToString()] = e;
        }
        dungeon["0"] = Blank("dungeon/glyph-0.blank.png", 4, 4);

        foreach (var (n, what) in new[] { (23, "spell-sparkle"), (24, "spell-hit"), (59, "rock") })
        {
            var e = Render(table, [(n, 3)], 0, 1, $"effects/{n:D2}-{what}.png");
            e["use"] = what;
            effects[n.ToString()] = e;
        }

        var page = HiresPage.FromApple(src.A("ICE DRAGON"));
        Save("screens/ice-dragon.png", page.ToImage(0, 0, 280, 192, transparentBlack: false));
        screens["ice-dragon"] = J.O(("file", "screens/ice-dragon.png"), ("size", J.Ints([280, 192])));
        var nums = BootData(src.A("PHANTASIE II"));
        for (var addr = 21341; addr < 21366; addr++)
            page.PokeApple(addr - 16384, 0);
        for (var r = 0; r < 7; r++)
        {
            var row = nums.GetRange(r * 26, 26);
            for (var i = 1; i < row.Count; i++)
                page.PokeApple(row[0] + 8192 + (i - 1) - 16384, row[i]);
        }
        for (var addr = 21469; addr < 21494; addr++)
            page.PokeApple(addr - 16384, 0);
        Save("screens/title.png", page.ToImage(0, 0, 280, 192, transparentBlack: false));
        screens["title"] = J.O(("file", "screens/title.png"), ("size", J.Ints([280, 192])),
            ("note", "ICE DRAGON + the boot program's POKE rows (lines 243-257); the HPLOT lightning (lines 50-94, random) is not drawn."));

        Index = J.O(
            ("_source", "Phantasie II Apple II (4am crack v2/5/86) side A: SHAPES ($451F, 59 shapes), ICE DRAGON " +
                        "(hi-res page), MN20..MN29 (monster shape / scale / hcolor). tools/scripts/" +
                        "extract_a2_p2_graphics.py; notes reverse-engineering/apple2/phantasie2/GRAPHICS.md."),
            ("origin_note", "origin = where the DRAW point sits inside the PNG (x, y); screen_x = the in-game x " +
                            "the colour phase was taken from."),
            ("shapes", shapes), ("monsters", monsters), ("figures", figures), ("portraits", portraits), ("tiles", tiles),
            ("dungeon", dungeon), ("effects", effects), ("screens", screens));
        var bytes = PyJson.SerializeUtf8(Index, 1, ensureAscii: true);
        _files["extraction-index.json"] = bytes;
        _ctx?.WriteFile($"{Root}/extraction-index.json", bytes);
    }

    /// <summary>The numbers of the boot program's DATA lines 260-290, in order.</summary>
    private static List<int> BootData(byte[] program)
    {
        var nums = new List<int>();
        foreach (var (_, ln, text) in A2Text.Lines(program))
        {
            var t = A2Text.Strip(text);
            if (ln is >= 260 and <= 290 && t.StartsWith("DATA", StringComparison.Ordinal))
                nums.AddRange(t[4..].Replace(" ", "").Split(',').Where(v => v.Length > 0)
                    .Select(v => int.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
        }
        return nums;
    }
}
