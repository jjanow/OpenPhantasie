using System.Buffers.Binary;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// Phantasie II Atari ST art (port of <c>extract_st_p2_graphics.py</c>): PNGs plus <c>extraction-index.json</c> and
/// <c>maps/maps.json</c>, all under the art root. Raster format: ST low-res device format (4 word-interleaved planes,
/// 8 bytes per 16 pixels); palettes are ST hardware words 0x0RGB, 3 bits per gun (FORMAT.md section 10).
/// </summary>
internal sealed class P2StGraphics
{
    public const string Root = "reverse-engineering/atari-st/phantasie2/extracted/graphics";

    private const int StartPaletteAddr = 0x156;
    private static readonly (string Name, int Addr, string Note)[] Palettes =
    [
        ("town", 0x23A62, "0x238c, 0x26xx town entry (fade in by Setcolor 0x2682), 0x2d0e; PELNOR.PIC (live-checked)"),
        ("map", 0x22956, "0x5712 overworld map, 0x5cea dungeon map (live-checked on the overworld)"),
        ("combat", 0x23A82, "combat screen (live-checked: monsters + portraits), 0x12d34 DECOR.PIC screen, " +
                            "0x130ec ending FINAL2/CROWD fade-in"),
    ];

    private const int MonsterOffsets = 0x23D74;
    private const int MonsterDims = 0x2112E;
    private const int MonsterRecords = 0x1E298 + 0x1C76;
    private const int Patterns = 0x21DD6;
    private const int PatternCount = 12;
    private const int Icons = 0x223D6;
    private static readonly string[] IconKinds = ["town", "inn", "dungeon", "party", "castle"];
    private const int Glyphs = 0x22656;
    private const int EmptyPortrait = 0x22756;
    private const int PortraitMark = 0x23CDC;

    private static readonly (string Name, int Addr, string Note)[] LineASprites =
    [
        ("combat/sparkle", 0x23C48, "0x18b16, amount >= 0: a damage spell's sparkle on a monster (bg 5, fg 2)"),
        ("combat/rock", 0x23C92, "0x18b16, amount < 0: the thrown rock on its monster; 0x18c6c: a rock's miss at (100, 10) (bg 13, fg 15)"),
    ];

    private static readonly (string Name, int Addr, int W, int H, int Frames, string Pal, string Note)[] TextBitmaps =
    [
        ("decor/demon-face", 0x13864, 80, 80, 1, "combat", "red demon face: 0x12e50 at (123,19) behind the door on DECOR; 0xdd92 at (146,114)"),
        ("decor/door", 0x144E4, 80, 90, 1, "combat", "wooden door on DECOR: 0x12c9c at (123,14); 0x12dca redraws it shorter step by step (opening)"),
        ("town/walker", 0x152F4, 16, 22, 7, "town", "0x4824: walker in the town street (y 156, x 110..183); frame 0 = blank"),
        ("maps/dungeon/compass", 0x157C4, 64, 63, 1, "map", "0x5c7a at (214,68) on the dungeon map"),
        ("decor/sorry-sign", 0x15FA4, 48, 40, 1, "combat", "sign 'Sorry, we've run out of Demons': 0x12e0c at (139,39) on the fifth run of the DECOR sequence (counter 0x231fa)"),
    ];

    private static readonly (string File, string Pal, int Top, string Note)[] Screens =
    [
        ("COVER1.PIC", "start", 0, "START.PRG title"),
        ("COVER2.PIC", "start", 65, "START.PRG credits, loaded over COVER1 at line 65"),
        ("PELNOR.PIC", "town", 0, "town backdrop (0x43ec), GEM menu bar baked in"),
        ("DECOR.PIC", "combat", 0, "DECOR screen (0x12bea: door + demon-face sequence), 155 lines"),
        ("FINAL2.PIC", "combat", 0, "ending (0x13002), 160 lines"),
    ];

    private const int CellW = 12, CellH = 9, OriginX = 4, OriginY = 15, Cols = 26, Rows = 20, PolyOff = 1500;
    private const int DngCellW = 5, DngCellH = 4;
    private static readonly int[] ColourCycle = [4, 7, 14];

    private readonly ImportContext _ctx;
    private readonly P2StSource _src;

    public P2StGraphics(ImportContext ctx, P2StSource src)
    {
        _ctx = ctx;
        _src = src;
    }

    /// <summary>The extraction index (extraction-index.json), built and written by <see cref="Run"/>.</summary>
    public JsonObject Index { get; private set; } = new();

    // ---- colour and raster helpers -----------------------------------------------------------------------------

    public static (byte R, byte G, byte B) StRgb(int word) =>
        ((byte)((word >> 8 & 7) * 255 / 7), (byte)((word >> 4 & 7) * 255 / 7), (byte)((word & 7) * 255 / 7));

    public static string HexCol(int word)
    {
        var (r, g, b) = StRgb(word);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static RgbaImage DecodeInterleaved(byte[] data, int off, int w, int h, int[] pal)
    {
        var wpl = (w + 15) / 16;
        var im = new RgbaImage(w, h);
        for (var y = 0; y < h; y++)
            for (var g = 0; g < wpl; g++)
            {
                var at = off + (y * wpl + g) * 8;
                int p0 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at)), p1 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at + 2));
                int p2 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at + 4)), p3 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at + 6));
                for (var b = 0; b < 16; b++)
                {
                    var x = g * 16 + b;
                    if (x >= w)
                        break;
                    var s = 15 - b;
                    var c = (p0 >> s & 1) | (p1 >> s & 1) << 1 | (p2 >> s & 1) << 2 | (p3 >> s & 1) << 3;
                    var (r, gg, bb) = StRgb(pal[c]);
                    im.Set(x, y, r, gg, bb);
                }
            }
        return im;
    }

    private static RgbaImage DecodeUdpat(byte[] data, int off, int[] pal)
    {
        var im = new RgbaImage(16, 16);
        for (var y = 0; y < 16; y++)
        {
            var planes = new int[4];
            for (var p = 0; p < 4; p++)
                planes[p] = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(off + 32 * p + 2 * y));
            for (var x = 0; x < 16; x++)
            {
                var c = 0;
                for (var p = 0; p < 4; p++)
                    c |= (planes[p] >> (15 - x) & 1) << p;
                var (r, g, b) = StRgb(pal[c]);
                im.Set(x, y, r, g, b);
            }
        }
        return im;
    }

    private static (RgbaImage Image, JsonObject Header) DecodeLineASprite(byte[] data, int off, int[] pal)
    {
        int I16(int a) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(off + a));
        int hotX = I16(0), hotY = I16(2), fmt = I16(4), bg = I16(6), fg = I16(8);
        var im = new RgbaImage(16, 16);
        for (var y = 0; y < 16; y++)
        {
            int back = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(off + 10 + 4 * y));
            int fore = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(off + 12 + 4 * y));
            for (var x = 0; x < 16; x++)
            {
                var s = 15 - x;
                if ((fore >> s & 1) != 0)
                {
                    var (r, g, b) = StRgb(pal[fg]);
                    im.Set(x, y, r, g, b);
                }
                else if ((back >> s & 1) != 0)
                {
                    var (r, g, b) = StRgb(pal[bg]);
                    im.Set(x, y, r, g, b);
                }
            }
        }
        return (im, J.O(("hot_spot", J.Ints([hotX, hotY])), ("format", fmt), ("background", bg), ("foreground", fg)));
    }

    private static RgbaImage PalettePng(int[] words)
    {
        var im = new RgbaImage(128, 8);
        for (var i = 0; i < words.Length; i++)
        {
            var (r, g, b) = StRgb(words[i]);
            for (var y = 0; y < 8; y++)
                for (var x = i * 8; x < i * 8 + 8; x++)
                    im.Set(x, y, r, g, b);
        }
        return im;
    }

    private static RgbaImage Crop(RgbaImage src, int x0, int y0, int x1, int y1)
    {
        var im = new RgbaImage(x1 - x0, y1 - y0);
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var (r, g, b, a) = src.Get(x, y);
                im.Set(x - x0, y - y0, r, g, b, a);
            }
        return im;
    }

    /// <summary>PIL <c>paste</c> at (dx, dy), clipped to the target.</summary>
    private static void Paste(RgbaImage dst, RgbaImage src, int dx, int dy)
    {
        for (var y = 0; y < src.Height; y++)
            for (var x = 0; x < src.Width; x++)
            {
                int tx = dx + x, ty = dy + y;
                if (tx < 0 || ty < 0 || tx >= dst.Width || ty >= dst.Height)
                    continue;
                var (r, g, b, a) = src.Get(x, y);
                dst.Set(tx, ty, r, g, b, a);
            }
    }

    /// <summary>Python's slug(): lower-case alphanumerics, other characters '-', runs collapsed, ends trimmed.</summary>
    public static string Slug(string s)
    {
        var t = Py.Strip(s).TrimEnd('.');
        var chars = t.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray();
        var o = new string(chars);
        while (o.Contains("--"))
            o = o.Replace("--", "-");
        return o.Trim('-');
    }

    private string Save(RgbaImage im, string relPath)
    {
        _ctx.WritePng($"{Root}/{relPath}", im);
        return relPath;
    }

    // ---- overworld map ---------------------------------------------------------------------------------------

    /// <summary>0x5028: the background pattern, then each polygon filled even-odd with its pattern (screen-aligned).</summary>
    private static RgbaImage RenderRegion(byte[] data, RgbaImage[] patterns)
    {
        int x0 = OriginX, y0 = OriginY, x1 = OriginX + Cols * CellW, y1 = OriginY + Rows * CellH;
        var owner = new int[320 * 200];
        var off = PolyOff;
        int bg = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(off));
        off += 2;
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
                owner[y * 320 + x] = bg;
        while (true)
        {
            int count = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(off));
            int pattern = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(off + 2));
            if (count == -1)
                break;
            off += 4;
            var pts = new (int X, int Y)[count];
            for (var k = 0; k < count; k++)
                pts[k] = (BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(off + 4 * k)),
                    BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(off + 4 * k + 2)));
            off += 4 * count;
            foreach (var (x, y) in ScanlineMask(pts, x0, y0, x1, y1))
                owner[y * 320 + x] = pattern;
        }
        var im = new RgbaImage(x1 - x0, y1 - y0);
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var (r, g, b, _) = patterns[owner[y * 320 + x]].Get(x & 15, y & 15);
                im.Set(x - x0, y - y0, r, g, b);
            }
        return im;
    }

    /// <summary>
    /// GEM's even-odd scanline fill, perimeter off: rows min_y + 1 .. max_y, an edge counts on row y when
    /// min_y &lt;= y &lt; max_y, crossings rounded half up, spans inclusive (s135: matches Hatari's OUT2).
    /// </summary>
    private static IEnumerable<(int X, int Y)> ScanlineMask((int X, int Y)[] pts, int rx0, int ry0, int rx1, int ry1)
    {
        var minY = pts.Min(p => p.Y);
        var maxY = pts.Max(p => p.Y);
        for (var y = Math.Max(ry0, minY + 1); y < Math.Min(ry1, maxY + 1); y++)
        {
            var xs = new List<double>();
            for (var i = 0; i < pts.Length; i++)
            {
                var (ax, ay) = pts[i];
                var (bx, by) = pts[(i + 1) % pts.Length];
                if (ay != by && Math.Min(ay, by) <= y && y < Math.Max(ay, by))
                    xs.Add(ax + (double)((y - ay) * (bx - ax)) / (by - ay));
            }
            xs.Sort();
            for (var k = 0; k + 1 < xs.Count; k += 2)
                for (var x = Math.Max(rx0, (int)Math.Floor(xs[k] + 0.5)); x <= Math.Min(rx1 - 1, (int)Math.Floor(xs[k + 1] + 0.5)); x++)
                    yield return (x, y);
        }
    }

    private static int Tile(byte[] data, int x, int y) => P2StSource.Unfog(data[(Cols - 1 - x) * Rows + y]);

    /// <summary>0x4cd0 icon scan: town / inn keep the last hit, castle up to 5 cells, dungeon up to 8.</summary>
    private static JsonObject IconCells(byte[] data)
    {
        var found = new Dictionary<string, List<int[]>> { ["town"] = [], ["inn"] = [], ["dungeon"] = [], ["castle"] = [] };
        for (var x = 0; x < Cols; x++)
            for (var y = 0; y < Rows; y++)
            {
                var t = Tile(data, x, y);
                if (t / 10 == 4)
                {
                    var kind = (t % 10) switch { 0 => "town", 1 => "inn", _ => "castle" };
                    if (kind is "town" or "inn")
                        found[kind] = [[x, y]];
                    else if (found[kind].Count < 5)
                        found[kind].Add([x, y]);
                }
                else if (t / 10 == 5 && found["dungeon"].Count < 8)
                    found["dungeon"].Add([x, y]);
            }
        var o = new JsonObject();
        foreach (var k in new[] { "town", "inn", "dungeon", "castle" })
            if (found[k].Count > 0)
                o[k] = J.A(found[k].Select(c => (JsonNode?)J.Ints(c)));
        return o;
    }

    // ---- main ------------------------------------------------------------------------------------------------

    public void Run()
    {
        var prg = _src.Phant;
        var img = prg.Image;
        var start = _src.Start.Image;

        int[] Words(byte[] buf, int a) =>
            Enumerable.Range(0, 16).Select(i => (int)BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(a + 2 * i))).ToArray();

        var pals = new Dictionary<string, int[]> { ["start"] = Words(start, StartPaletteAddr) };
        foreach (var (name, addr, _) in Palettes)
            pals[name] = Words(img, addr);
        var palettes = new JsonObject();
        foreach (var (name, words16) in pals)
        {
            string where;
            if (name == "start")
                where = "START.PRG image 0x156 (Setpalette before COVER1/COVER2)";
            else
            {
                var p = Palettes.First(q => q.Name == name);
                where = $"PHANT.PRG image {Py.HexAddr(p.Addr)} (data+{Py.HexAddr(p.Addr - prg.TextSize)}): {p.Note}";
            }
            palettes[name] = J.O(("source", where), ("st_words", J.Strs(words16.Select(w => w.ToString("x3")))),
                ("rgb", J.Strs(words16.Select(HexCol))), ("swatch", Save(PalettePng(words16), $"palettes/{name}.png")));
        }
        palettes["map"]!["colour_cycle"] = J.O(("registers", J.Ints(ColourCycle)),
            ("note", "VBL routine 0x1382e rotates hardware colours 4 -> 7 -> 14 -> 4 (counter 0x13862/0x13863)"));
        var index = J.O(
            ("_source", "Phantasie II Atari ST v1.0 (unpacked/disk1, disk2); tools/scripts/extract_st_p2_graphics.py"),
            ("_format", "reverse-engineering/atari-st/phantasie2/FORMAT.md §10"),
            ("palettes", palettes));

        // screens
        var screens = new JsonArray();
        var shots = new Dictionary<string, RgbaImage>();
        foreach (var (name, pal, top, note) in Screens)
        {
            var data = _src.Read(name);
            var h = data.Length / 160;
            var im = DecodeInterleaved(data, 0, 320, h, pals[pal]);
            var slug = Slug(name.Split('.')[0]);
            shots[slug] = im;
            screens.Add(J.O(("file", name), ("path", Save(im, $"screens/{slug}.png")), ("width", 320), ("height", h),
                ("top", top), ("palette", pal), ("note", note)));
        }
        var c1 = Crop(shots["cover1"], 0, 0, shots["cover1"].Width, shots["cover1"].Height);
        Paste(c1, shots["cover2"], 0, 65);
        screens.Add(J.O(("file", "COVER1.PIC + COVER2.PIC"), ("path", Save(c1, "screens/title-credits.png")),
            ("width", 320), ("height", 200), ("top", 0), ("palette", "start"),
            ("note", "the title screen after START.PRG's 250-VBL wait")));
        index["screens"] = screens;
        {
            const int x0 = 40, y0 = 127, w = 235, h = 31, frames = 3;
            var data = _src.Read("CROWD.PIC");
            var stride = (w + 15) / 16 * 8 * h;
            if (stride * frames != data.Length)
                throw new InvalidDataException($"CROWD.PIC: {data.Length} bytes, expected {stride * frames}");
            index["crowd"] = J.O(("file", "CROWD.PIC"), ("x", x0), ("y", y0), ("width", w), ("height", h), ("palette", "combat"),
                ("note", "ending: frames 0/1 alternate 10 times over FINAL2 (330-tick waits), then frame 2"),
                ("frames", J.Strs(Enumerable.Range(0, frames).Select(k =>
                    Save(DecodeInterleaved(data, k * stride, w, h, pals["combat"]), $"screens/crowd-{k}.png")))));
        }

        // monsters
        var mstr1 = _src.Read("MSTR1.PAT");
        var mstr2 = _src.Read("MSTR2.PAT");
        var monsters = new JsonArray();
        for (var i = 0; i < P2StExtract.MonsterCount; i++)
        {
            var off = (int)prg.U32(MonsterOffsets + 4 * i);
            int w = prg.U16(MonsterDims + 4 * i), h = prg.U16(MonsterDims + 4 * i + 2);
            var nm = prg.CStr(MonsterRecords + 58 * i, 20);
            if (off + (w + 15) / 16 * 8 * h > mstr1.Length)
                throw new InvalidDataException($"MSTR1.PAT: monster {i} runs past the file");
            var fn = $"{i:00}-{Slug(nm)}.png";
            monsters.Add(J.O(("index", i), ("name", nm), ("width", w), ("height", h), ("pat_offset", off),
                ("idle", Save(DecodeInterleaved(mstr1, off, w, h, pals["combat"]), $"monsters/idle/{fn}")),
                ("attacking", Save(DecodeInterleaved(mstr2, off, w, h, pals["combat"]), $"monsters/attacking/{fn}"))));
        }
        index["monsters"] = monsters;

        // portraits
        var raceNames = P2StExtract.Names(prg)["races"]!.AsArray().Select(n => (string)n!).ToList();
        var party1 = _src.Read("PARTY1.PAT");
        var party2 = _src.Read("PARTY2.PAT");
        var races = new JsonArray();
        for (var i = 0; i < 16; i++)
        {
            var nm = i < raceNames.Count ? raceNames[i] : $"race-{i}";
            var fn = $"{i:00}-{Slug(nm)}.png";
            races.Add(J.O(("index", i), ("name", nm),
                ("idle", Save(DecodeInterleaved(party1, 512 * i, 32, 32, pals["combat"]), $"portraits/idle/{fn}")),
                ("attacking", Save(DecodeInterleaved(party2, 512 * i, 32, 32, pals["combat"]), $"portraits/attacking/{fn}"))));
        }
        index["races"] = races;

        // small bitmaps
        var bitmaps = new JsonArray();
        foreach (var (name, addr, w, h, frames, pal, note) in TextBitmaps)
        {
            var size = (w + 15) / 16 * 8 * h;
            var paths = Enumerable.Range(0, frames).Select(k => Save(DecodeInterleaved(img, addr + k * size, w, h, pals[pal]),
                $"{name}{(frames > 1 ? $"-{k}" : "")}.png")).ToList();
            bitmaps.Add(J.O(("name", name), ("address", Py.HexAddr(addr)), ("width", w), ("height", h), ("palette", pal),
                ("note", note), ("frames", J.Strs(paths))));
        }
        foreach (var (name, addr, w, h, note) in new[]
                 {
                     ("ui/empty-portrait", EmptyPortrait, 32, 32, "0x12bc0: empty party slot"),
                     ("ui/ghost-mark", PortraitMark, 16, 19, "white ghost, 0x18a02: drawn at portrait + (8,13) [?] which status"),
                 })
            bitmaps.Add(J.O(("name", name), ("address", Py.HexAddr(addr)), ("width", w), ("height", h), ("palette", "combat"),
                ("note", note), ("frames", J.Strs([Save(DecodeInterleaved(img, addr, w, h, pals["combat"]), $"{name}.png")]))));
        index["bitmaps"] = bitmaps;

        var sprites = new JsonArray();
        foreach (var (name, addr, note) in LineASprites)
        {
            var (sprite, header) = DecodeLineASprite(img, addr, pals["combat"]);
            var e = J.O(("name", name), ("address", Py.HexAddr(addr)), ("width", 16), ("height", 16), ("palette", "combat"),
                ("note", note));
            foreach (var (k, v) in header.ToList())
            {
                header.Remove(k);
                e[k] = v;
            }
            _ctx.WritePng($"{Root}/{name}.png", sprite, forceAlpha: true);
            e["path"] = $"{name}.png";
            sprites.Add(e);
        }
        index["sprites"] = sprites;

        // overworld + dungeon map (maps.json paths are relative to its folder: the "maps/" prefix is dropped)
        var patterns = Enumerable.Range(0, PatternCount).Select(i => DecodeUdpat(img, Patterns + 128 * i, pals["map"])).ToArray();
        string M(string p) => p.StartsWith("maps/", StringComparison.Ordinal) ? p["maps/".Length..] : p;
        var patternPaths = new JsonObject();
        for (var i = 0; i < PatternCount; i++)
            patternPaths[i.ToString()] = M(Save(patterns[i], $"maps/overworld/patterns/{i:00}.png"));
        var icons = new JsonObject();
        for (var i = 0; i < IconKinds.Length; i++)
            if (IconKinds[i] != "party")
                icons[IconKinds[i]] = M(Save(Crop(DecodeInterleaved(img, Icons + 128 * i, 16, 16, pals["map"]), 0, 0, CellW, CellH),
                    $"maps/overworld/icons/{IconKinds[i]}.png"));
        var party = M(Save(Crop(DecodeInterleaved(img, Icons + 128 * 3, 16, 16, pals["map"]), 0, 0, CellW, CellH),
            "maps/overworld/icons/party.png"));
        var glyphs = new JsonObject();
        for (var i = 0; i < 8; i++)
            glyphs[(0xE6 + i).ToString()] = M(Save(Crop(DecodeInterleaved(img, Glyphs + 32 * i, 16, 4, pals["map"]), 0, 0, DngCellW, DngCellH),
                $"maps/dungeon/glyph-{0xE6 + i:x2}.png"));
        var regions = new JsonObject();
        foreach (var n in P2StSource.Regions)
        {
            var data = _src.Region(n);
            var cells = IconCells(data);
            var first = new JsonObject();
            foreach (var (k, v) in cells)
                first[k] = v!.AsArray()[0]!.DeepClone();
            regions[n.ToString()] = J.O(
                ("image", M(Save(RenderRegion(data, patterns), $"maps/overworld/regions/out{n:00}.png"))),
                ("background_pattern", (int)BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(PolyOff))),
                ("icons", first), ("icon_cells", cells));
        }
        var maps = J.O(
            ("_source", "PHANT.PRG 0x5760 geometry, 0x5028 map draw, 0x53ee/0x545a patterns, 0x5230 icons, 0x4cd0 icon scan, " +
                        "0x5b06 dungeon geometry, 0x5e50 glyphs, 0x5c7a compass; tools/scripts/extract_st_p2_graphics.py"),
            ("palette", "map"),
            ("overworld", J.O(("cell_width", CellW), ("cell_height", CellH), ("origin", J.Ints([OriginX, OriginY])),
                ("fog_threshold", P2StSource.Fog), ("patterns", patternPaths), ("icons", icons), ("party", party),
                ("regions", regions))),
            ("dungeon", J.O(("glyph_width", DngCellW), ("glyph_height", DngCellH), ("origin", J.Ints([10, 16])),
                ("glyphs", glyphs), ("compass", "dungeon/compass.png"))));
        _ctx.WriteJsonFile($"{Root}/maps/maps.json", maps, indent: 2, ensureAscii: true);
        index["maps"] = "maps/maps.json";
        _ctx.WriteJsonFile($"{Root}/extraction-index.json", index, indent: 2);
        Index = index;
    }
}
