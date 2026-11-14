using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2Apple2;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P3Apple2;

/// <summary>
/// The Apple II art of Phantasie III (port of <c>tools/scripts/extract_a2_p3_graphics.py</c>; formats and evidence in
/// <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c>): every shape of <c>SHAPES</c>, the <c>MONSTER SHAPES</c>
/// sprites and the 80 monsters in both poses, the party figures and race portraits, the overworld tiles by DOS map
/// byte (plus the Netherworld's tile swaps), the 3 × 3 dungeon marks, effects, the screen frame, the title and the
/// ending (an original copy only: the <c>[cr]</c> copy's are the cracker's), the map-box panels and the hi-res font,
/// plus <c>extraction-index.json</c>; then (<see cref="Import"/>) the pack <c>graphics/apple2.json</c>.
/// Side A is the disk side holding <c>SHAPES</c> (1A, 3A, or v3.0's boot side), side B the one holding
/// <c>NIKADEMUS</c> (1B, 2A, or v3.0's utilities side).
/// </summary>
public sealed class P3Apple2Art
{
    public const string GameId = "phantasie3";
    public const string Root = "reverse-engineering/apple2/phantasie3/extracted/graphics";

    /// <summary>The <c>[cr]</c> copy's own screens (crack credits, not SSI's art): never drawn.</summary>
    private static readonly Dictionary<string, string> CrackSha1 = new(StringComparer.Ordinal)
    {
        ["ICE DRAGON"] = "49891f3766cef70d35f0ded9f89f10032f70a747",
        ["WIN PIC"] = "d5e303bd929cd39d424d7d924fd61eab1287a98d",
    };

    private static readonly string[] Terrains =
        ["GRASS", "FOREST", "HILLS", "MOUNTAINS", "BUILDINGS", "DUNGEON", "PATHWAY", "BRIDGE", "LAVA", "DESERT", "WATER", "MIST"];

    public const int TileClassBase = 300;
    public const int NetherRegion = 4;
    public const int UnseenGlyph = 99;

    /// <summary>DOS map byte → Apple terrain class, [first, last, class] (GRAPHICS.md section 5).</summary>
    public static readonly int[][] DosToApple =
    [
        [0, 25, 10], [26, 38, 4], [39, 46, 5], [47, 54, 4], [55, 55, 5], [56, 58, 4], [59, 60, 5], [61, 67, 7],
        [68, 74, 11], [75, 77, 7], [78, 87, 11], [88, 89, 8], [90, 114, 3], [115, 154, 2], [155, 187, 9],
        [188, 205, 0], [206, 209, 6], [210, 213, 8], [214, 224, 6], [225, 241, 1], [242, 255, 10],
    ];

    /// <summary>The Netherworld's own terrain (DOS overrides: Black Path, Mist / Steam, Lava).</summary>
    public static readonly int[][] Nether = [[61, 77, 7], [78, 87, 11], [88, 241, 8]];

    private static readonly string[] Races = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
        "GOBLIN", "LIZARD MAN", "MINOTAUR", "UNDEAD"];
    private static readonly (int Shape, string Label)[] Bodies =
        [(19, "human-elf-fighter"), (25, "small-races"), (26, "monster-races"), (27, "human-elf-robed"), (4, "high-social-class")];
    private static readonly (int Arm, string Pose)[] Poses = [(20, "idle"), (22, "attacking"), (21, "parrying"), (5, "casting")];
    private static readonly (int Shape, string Use)[] DungeonMarks =
        [(31, "wall"), (32, "cells-185-199"), (33, "cells-200-214"), (34, "cells-215-217"), (35, "cells-218-220"), (36, "cells-221-up"), (30, "party-cursor")];
    private static readonly (int Shape, string Use, int X)[] Effects =
        [(1, "arrow", 220), (2, "bow", 147), (3, "bow-fired", 147), (6, "body-down", 147), (23, "spell-sparkle", 161), (24, "spell-hit", 150), (47, "spell-hit-sp0", 150)];
    private static readonly (string File, char Side, string What)[] Views =
        [("PIC1", 'a', "wilderness"), ("PIC2", 'a', "strange-lands"), ("PIC3", 'a', "dungeons"), ("OUTPIC", 'b', "empty-map-box"), ("PIC", 'b', "saved-dungeon-map")];

    private const int MonsterColumn = 21, PartyX = 147, TileX = 14, TileOddX = 7, DungeonX = 3, PadX = 140, PadY = 200;

    private readonly ImportContext? _ctx;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public P3Apple2Art(ImportContext? ctx) => _ctx = ctx;

    public JsonObject Index { get; private set; } = new();

    /// <summary>Written files (path under <see cref="Root"/> → bytes); also sent to the context when there is one.</summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    /// <summary>
    /// The import step: pick sides A and B from the run's containers (an original copy before the <c>[cr]</c> one),
    /// write the art under <see cref="Root"/> and the pack <c>GameData/phantasie3/graphics/apple2.json</c>.
    /// </summary>
    public static void Import(ImportContext context)
    {
        var (a, b) = Sides(context.Files.Containers);
        var art = new P3Apple2Art(context);
        art.Run(Lookup(a), Lookup(b));
        context.WriteGameDataBytes(GameId, "graphics/apple2.json",
            PyJson.SerializeUtf8(P3Apple2ArtManifest.Pack(art.Index), 2, ensureAscii: true));
    }

    /// <summary>Side A (<c>SHAPES</c>) and side B (<c>NIKADEMUS</c>); a side with the original screens wins.</summary>
    public static (Container A, Container B) Sides(IEnumerable<Container> containers)
    {
        var list = containers.ToList();
        static ImportFile? F(Container c, string name) => c.Files.FirstOrDefault(f => f.Path == name);
        var a = list.Where(c => F(c, "SHAPES") is not null && F(c, "MONSTER SHAPES") is not null && F(c, "INTRO3") is not null)
            .OrderBy(c => F(c, "ICE DRAGON") is { } f && f.Sha1 == CrackSha1["ICE DRAGON"] ? 1 : 0).FirstOrDefault()
            ?? throw new FileNotFoundException("Phantasie III Apple II: no side holding SHAPES / MONSTER SHAPES / INTRO3");
        var b = list.Where(c => F(c, "NIKADEMUS") is not null && F(c, "WIN PIC") is not null)
            .OrderBy(c => F(c, "WIN PIC") is { } f && f.Sha1 == CrackSha1["WIN PIC"] || F(c, "ROUTINE") is null ? 1 : 0).FirstOrDefault()
            ?? throw new FileNotFoundException("Phantasie III Apple II: no side holding NIKADEMUS / WIN PIC");
        return (a, b);
    }

    private static Func<string, byte[]?> Lookup(Container c) => name => c.Files.FirstOrDefault(f => f.Path == name)?.Data;

    private void Save(string rel, RgbaImage img)
    {
        var bytes = Png.Encode(img, forceAlpha: true);
        _files[rel] = bytes;
        _ctx?.WriteFile($"{Root}/{rel}", bytes);
    }

    private void SaveBytes(string rel, byte[] bytes)
    {
        _files[rel] = bytes;
        _ctx?.WriteFile($"{Root}/{rel}", bytes);
    }

    public static int? ClassOf(int b, int[][] table)
    {
        foreach (var r in table)
            if (b >= r[0] && b <= r[1])
                return r[2];
        return null;
    }

    private static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }

    /// <summary>TOWN3 2350-2356 (race, class 0-based).</summary>
    public static int PartyBodyShape(int race, int cls) =>
        race > 6 ? 26 : race is not (0 or 2) ? 25 : cls == 1 || cls > 3 ? 27 : 19;

    private JsonObject Shape(ShapeTable table, (int Shape, int Hcolor)[] parts, int x, string rel, (int W, int H)? cell = null)
    {
        var page = new HiresPage(80, 400);
        var boxes = new List<(int X0, int Y0, int X1, int Y1)>();
        foreach (var (shape, hcolor) in parts)
        {
            var vec = table.Vectors(shape);
            page.Draw(vec, PadX + x, PadY, hcolor, 1);
            if (ShapeTable.BBox(vec, 1) is { } b)
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
            ("shapes", J.Ints(parts.Select(p => p.Shape))), ("hcolors", J.Ints(parts.Select(p => p.Hcolor))), ("scale", 1),
            ("screen_x", x));
    }

    /// <summary>MONSTER PLOT: 3 bytes × 21 rows (42 for shape ≥ 21) from (shape − 1) · 64, at byte column 21.</summary>
    private JsonObject Sprite(byte[] data, int shape, string rel)
    {
        var rows = shape >= 21 ? 42 : 21;
        var b = (shape - 1) * 64;
        var page = new HiresPage(40, rows);
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < 3; c++)
                page.Mem[r][MonsterColumn + c] = data[(b & ~0xFF) | ((b + r * 3 + c) & 0xFF)];
        var x0 = MonsterColumn * 7;
        var img = page.ToImage(x0, 0, x0 + 21, rows, transparentBlack: true);
        Save(rel, img);
        return J.O(("file", rel), ("size", J.Ints([img.Width, img.Height])), ("shape", shape), ("rows", rows), ("byte_column", MonsterColumn));
    }

    private JsonObject Page(byte[] data, string rel)
    {
        Save(rel, HiresPage.FromApple(Pad8K(data)).ToImage(0, 0, 280, 192, transparentBlack: false));
        return J.O(("file", rel), ("size", J.Ints([280, 192])));
    }

    private JsonObject Panel(byte[] data, string rel)
    {
        var page = new HiresPage(40, 96);
        for (var r = 0; r < 96; r++)
            data.AsSpan(r * 12, 12).CopyTo(page.Mem[r]);
        Save(rel, page.ToImage(0, 0, 84, 96, transparentBlack: false));
        return J.O(("file", rel), ("size", J.Ints([84, 96])), ("screen", J.Ints([0, 96])));
    }

    private JsonObject Block(string rel, int w, int h, byte v)
    {
        var img = new RgbaImage(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                img.Set(x, y, v, v, v, 255);
        Save(rel, img);
        return J.O(("file", rel), ("size", J.Ints([w, h])));
    }

    private static byte[] Pad8K(byte[] data)
    {
        if (data.Length >= 8192)
            return data;
        var o = new byte[8192];
        data.CopyTo(o, 0);
        return o;
    }

    /// <summary>
    /// MN30..MN39: 8 text records of 22 CR-ended fields (OUTDOORS3 line 910); name first ('=' is a space; control
    /// characters dropped: BEEPING BLOB's name rings the bell, CHR$(7)), S% last.
    /// </summary>
    public static List<(int Index, string Name, int Shape)> Monsters(Func<string, byte[]?> read)
    {
        var o = new List<(int, string, int)>();
        for (var f = 0; f < 10; f++)
        {
            var raw = read($"MN3{f}") ?? throw new FileNotFoundException($"MN3{f} not found");
            var text = Encoding.ASCII.GetString(raw.Select(c => (byte)(c & 0x7F)).ToArray()).Split('\r');
            for (var r = 0; r < 8; r++)
                o.Add((f * 8 + r, new string(text[r * 22].Where(ch => ch >= ' ').ToArray()).Replace('=', ' '),
                    int.Parse(text[r * 22 + 21], System.Globalization.CultureInfo.InvariantCulture)));
        }
        return o;
    }

    /// <summary>WIN (v3.0) lines 83-85: ROUTINE 30000 (stride 1, 8, … 253), then 30050 with the same strides.</summary>
    public static byte[] DecodeWinPic(byte[] data)
    {
        var mem = Pad8K(data)[..8192];
        for (var step = 1; step < 256; step += 7)
        {
            int v = 0x40, p = 0;
            do
            {
                v = (v + 1) & 0xFF;
                mem[p] ^= (byte)v;
                p = (p + step) & 0x1FFF;
            }
            while (p != 0);
        }
        for (var step = 1; step < 256; step += 7)
        {
            int v = 0x40, p = 0;
            do
            {
                var carry = 0;
                do
                {
                    var c = v & 1;
                    v = (v >> 1) | (carry << 7);
                    carry = c;
                }
                while (v == 0);
                int a = mem[p];
                if (a != 0 && (a ^ v) != 0)
                    mem[p] = (byte)(a ^ v);
                p = (p + step) & 0x1FFF;
            }
            while (p != 0);
        }
        return mem;
    }

    /// <summary>The 8 bytes IMPTEXT draws for <paramref name="ch"/> ($A0-$DF): table $1800 / $1900, file at $1828.</summary>
    public static byte[] FontGlyph(byte[] font, int ch)
    {
        var addr = ((ch < 0xC0 ? 0x18 : 0x19) << 8) | ((ch << 3) & 0xFF);
        var off = addr - 0x1828;
        return off >= 0 && off <= font.Length - 8 ? font[off..(off + 8)] : new byte[8];
    }

    /// <summary>Game code → the character its glyph shows (GRAPHICS.md section 7).</summary>
    public static IReadOnlyDictionary<int, char> FontMap { get; } = BuildFontMap();

    private static Dictionary<int, char> BuildFontMap()
    {
        var m = new Dictionary<int, char>();
        for (var c = 0xB0; c < 0xBA; c++)
            m[c] = (char)(c & 0x7F);
        for (var c = 0xC1; c < 0xDB; c++)
            m[c] = (char)(c & 0x7F);
        m[0xBD] = ' ';
        m[0xC0] = ':';
        m[0xBA] = '-';
        m[0xBB] = '"';
        m[0xBC] = ',';
        m[0xBE] = '?';
        m[0xBF] = ')';
        return m;
    }

    public static JsonObject FontJson(byte[] font, string version)
    {
        var glyphs = new JsonObject();
        foreach (var (code, ch) in FontMap.OrderBy(kv => kv.Value))
        {
            var g = FontGlyph(font, code);
            var rows = Enumerable.Range(0, 8).Select(r => r < 7
                ? string.Concat(Enumerable.Range(0, 7).Select(b => ((g[r] >> b) & 1) != 0 ? '#' : '.'))
                : ".......");
            glyphs[ch.ToString()] = J.O(("rows", J.Strs(rows)), ("advance", 7));
        }
        return J.O(("_schema", "phantasie.font/v1"),
            ("_note", $"Phantasie III Apple II hi-res text font (IMPTEXT, v{version}; tools/scripts/extract_a2_p3_graphics.py, " +
                      "GRAPHICS.md section 7): 7 x 8 cells, the routine draws rows 0-6 (bit 0 = leftmost pixel). " +
                      "Keyed by the character each glyph shows (the game prints '=' for a space, '@' for ':'...). No " +
                      "'.', '!', '/', '(' or lower case: not the pack's font."),
            ("id", "apple2-p3-hires"), ("height", 8), ("line_height", 8), ("space_advance", 7), ("fallback", " "),
            ("fold_to_upper", true), ("glyphs", glyphs));
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

    public void Run(Func<string, byte[]?> sideA, Func<string, byte[]?> sideB)
    {
        byte[] A(string n) => sideA(n) ?? throw new FileNotFoundException($"{n} not found on side A", n);
        byte[] B(string n) => sideB(n) ?? throw new FileNotFoundException($"{n} not found on side B", n);
        var intro = Encoding.ASCII.GetString(A("INTRO3").Select(c => (byte)(c & 0x7F)).ToArray());
        var version = intro.Contains("VERSION 3.0", StringComparison.Ordinal) ? "3.0"
            : intro.Contains("VERSION 1.0", StringComparison.Ordinal) ? "1.0" : "?";
        var table = new ShapeTable(A("SHAPES"));
        JsonObject shapes = new(), sprites = new(), monsters = new(), figures = new(), portraits = new(), tiles = new(),
            dungeon = new(), effects = new(), screens = new(), views = new(), font = new();
        var swaps = new JsonArray();

        for (var n = 1; n <= table.Count; n++)
            shapes[n.ToString()] = Shape(table, [(n, 3)], 0, $"shapes/{n:D2}.png");

        var spr = A("MONSTER SHAPES");
        var nik = spr[..0x500].Concat(B("NIKADEMUS")).Concat(spr[0x600..]).ToArray();
        foreach (var s in Enumerable.Range(1, 20).Append(21).Append(23))
            sprites[s.ToString()] = Sprite(spr, s, $"sprites/{s:D2}.png");
        foreach (var s in new[] { 21, 23 })
            sprites[$"nikademus.{s}"] = Sprite(nik, s, $"sprites/nikademus-{s:D2}.png");
        foreach (var (i, name, s) in Monsters(sideA))
        {
            var src = i is 77 or 78 ? nik : spr;
            var file = $"{i:D2}-{Slug(name)}.png";
            var idle = Sprite(src, s, $"monsters/idle/{file}");
            var act = Sprite(src, s + 1 + (s == 21 ? 1 : 0), $"monsters/attacking/{file}");
            monsters[i.ToString()] = J.O(("name", name), ("shape", s), ("idle", idle), ("attacking", act));
        }

        foreach (var (body, label) in Bodies)
            foreach (var (arm, pose) in Poses)
                figures[$"{body}.{pose}"] = Shape(table, [(body, 3), (arm, 3)], PartyX, $"figures/{pose}/{body:D2}-{label}.png");
        for (var race = 0; race < Races.Length; race++)
        {
            var body = PartyBodyShape(race, 3);
            foreach (var (arm, pose) in Poses[..2])
            {
                var e = Shape(table, [(body, 3), (arm, 3)], PartyX, $"portraits/{pose}/{race:D2}-{Slug(Races[race])}.png");
                e["body"] = body;
                portraits[$"{race}.{pose}"] = e;
            }
        }

        for (var c = 0; c < Terrains.Length; c++)
        {
            var hc = 1 + c / 2;
            tiles[$"class.{c}"] = Shape(table, [(37 + c, hc)], TileX, $"overworld/tiles/{TileClassBase + c}.class-{Slug(Terrains[c])}.png", (7, 6));
            tiles[$"class.{c}.odd"] = Shape(table, [(37 + c, hc)], TileOddX, $"overworld/odd/{c:D2}-{Slug(Terrains[c])}.png", (7, 6));
        }
        for (var b = 0; b < 256; b++)
        {
            var c = ClassOf(b, DosToApple)!.Value;
            var e = Shape(table, [(37 + c, 1 + c / 2)], TileX, $"overworld/tiles/{b:D3}.{Slug(Terrains[c])}.png", (7, 6));
            e["class"] = c;
            tiles[b.ToString()] = e;
        }
        for (var b = 0; b < 256; b++)
            if (ClassOf(b, Nether) is { } c && c != ClassOf(b, DosToApple))
                swaps.Add(J.Ints([NetherRegion, b, TileClassBase + c]));
        tiles["party"] = Shape(table, [(43, 3)], TileX, "overworld/party.png", (7, 6));

        foreach (var (n, what) in DungeonMarks)
        {
            var e = Shape(table, [(n, 3)], DungeonX, $"dungeon/glyph-{n}.{what}.png", (3, 3));
            e["use"] = what;
            dungeon[n.ToString()] = e;
        }
        dungeon["0"] = Block("dungeon/glyph-0.blank.png", 3, 3, 0);
        dungeon[UnseenGlyph.ToString()] = Block($"dungeon/glyph-{UnseenGlyph}.unseen.png", 3, 3, 255);

        foreach (var (n, what, x) in Effects)
        {
            var e = Shape(table, [(n, 3)], x, $"effects/{n:D2}-{what}.png");
            e["use"] = what;
            effects[n.ToString()] = e;
        }

        screens["frame"] = Page(A("SCREEN"), "screens/frame.png");
        var ice = A("ICE DRAGON");
        if (Hashing.Sha1(ice) == CrackSha1["ICE DRAGON"])
            screens["_title"] = "ICE DRAGON is the [cr] copy's crack screen: no title picture.";
        else
        {
            screens["title-picture"] = Page(ice, "screens/title-picture.png");
            var page = HiresPage.FromApple(Pad8K(ice));
            var nums = BootData(A("PHANTASIE III"));
            for (var r = 0; r < 7; r++)
            {
                var row = nums.GetRange(r * 26, 26);
                for (var i = 1; i < row.Count; i++)
                    page.PokeApple(row[0] - 6 + (i - 1) - 8192, row[i]);
            }
            Save("screens/title.png", page.ToImage(0, 0, 280, 192, transparentBlack: false));
            screens["title"] = J.O(("file", "screens/title.png"), ("size", J.Ints([280, 192])),
                ("note", "ICE DRAGON + the boot program's DATA rows (lines 245-290)."));
        }
        var win = B("WIN PIC");
        if (sideB("ROUTINE") is not null && Hashing.Sha1(win) != CrackSha1["WIN PIC"])
        {
            var e = Page(DecodeWinPic(win), "screens/ending.png");
            e["note"] = "WIN PIC after ROUTINE 30000 / 30050 (WIN lines 83-85).";
            screens["ending"] = e;
        }
        else
            screens["_ending"] = "WIN PIC is the [cr] copy's crack screen (its WIN is a stub): no ending picture.";

        foreach (var (name, side, what) in Views)
        {
            var data = side == 'a' ? sideA(name) : sideB(name);
            if (data is null)
                continue;
            var e = Panel(data, $"views/{name.ToLowerInvariant()}-{what}.png");
            e["source"] = name;
            views[Slug(what)] = e;
        }

        foreach (var (name, label) in new[] { ("IMPTEXT", "imptext"), ("IMPREVERSE", "impreverse") })
        {
            var data = A(name);
            var page = new HiresPage(40, 16);
            for (var i = 0; i < 64; i++)
            {
                var g = FontGlyph(data, 0xA0 + i);
                for (var r = 0; r < 8; r++)
                    page.Mem[(i / 32) * 8 + r][i % 32] = (byte)(g[r] & 0x7F);
            }
            Save($"font/{label}.png", page.ToImage(0, 0, 224, 16, transparentBlack: false));
            font[label] = J.O(("file", $"font/{label}.png"), ("size", J.Ints([224, 16])), ("note", "characters $A0-$DF, 32 per row"));
        }
        SaveBytes("font/apple2-p3-hires.json", PyJson.SerializeUtf8(FontJson(A("IMPTEXT"), version), 1, ensureAscii: true));
        font["json"] = J.O(("file", "font/apple2-p3-hires.json"));

        Index = J.O(
            ("_source", $"Phantasie III Apple II v{version}, disk 1 sides A / B: SHAPES ($45DC, 48 shapes), " +
                        "MONSTER SHAPES + NIKADEMUS (MONSTER PLOT sprites), SCREEN / ICE DRAGON / WIN PIC (hi-res pages), " +
                        "PIC1-3 / OUTPIC / PIC (map-box panels), IMPTEXT (font), MN30..MN39 (S%). " +
                        "tools/scripts/extract_a2_p3_graphics.py; notes reverse-engineering/apple2/phantasie3/GRAPHICS.md."),
            ("version", version),
            ("origin_note", "origin = where the DRAW point sits inside the PNG (x, y); screen_x = the in-game x the colour " +
                            "phase was taken from."),
            ("shapes", shapes), ("sprites", sprites), ("monsters", monsters), ("figures", figures), ("portraits", portraits),
            ("tiles", tiles), ("tile_swaps", swaps), ("dungeon", dungeon), ("effects", effects), ("screens", screens),
            ("views", views), ("font", font));
        SaveBytes("extraction-index.json", PyJson.SerializeUtf8(Index, 1, ensureAscii: true));
    }
}
