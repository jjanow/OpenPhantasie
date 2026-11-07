using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using static OpenPhantasie.Importer.Pipelines.P1Amiga.AmigaGfx;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Port of <c>tools/scripts/extract_amiga_map_graphics.py</c>: the overworld region pictures (background pattern +
/// area-filled polygons from <c>OUT*.DAT + 0x3e8</c>, map module h56), fill patterns, icons, the party sprite, the
/// dungeon glyphs and compass, and <c>maps/maps.json</c>. The region pictures follow I's DOS terrain where the Amiga
/// cell class differs (user s53) and the remaster's <c>map-patches.json</c> (D-060), so they need I's
/// <c>overworld.json</c>; without it the regions and <c>maps.json</c> are skipped (the patterns, icons, glyphs and
/// compass need nothing else and are still written).
/// </summary>
internal static class P1AmigaMaps
{
    private const int PaletteOff = 0x1949C;
    private const int PatternOff = 0x196BC;
    private const int PatternCount = 12;
    private const int IconOff = 0x195E4;
    private static readonly string[] IconKinds = ["town", "inn", "dungeon"];
    private const int SpriteOff = 0x19550;
    private static readonly int[] SpriteColours = [0x000, 0xE00, 0xEB0];

    private const int CellW = 12, CellH = 9, OriginX = 4, OriginY = 15, Cols = 26, Rows = 20;
    private const int PolyOff = 0x3E8, Fog = 120;
    private static readonly int[] Regions = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 19];

    private const int GlyphOff = 0x19CBC, GlyphW = 5, GlyphH = 4;
    private const int CompassOff = 0x1BF1C, CompassW = 64, CompassH = 63;

    private const int ScreenW = 320, ScreenH = 200;

    public static void Run(ImportContext ctx, byte[] game, Action<string, RgbaImage> png)
    {
        var palette = LoadPalette(game, PaletteOff, hw4: true);
        var patterns = Enumerable.Range(0, PatternCount)
            .Select(i => DecodePlanar(game, PatternOff + 128 * i, 16, 16, palette)).ToList();
        for (var i = 0; i < patterns.Count; i++)
            png($"maps/overworld/patterns/{i:00}.png", patterns[i]);
        for (var i = 0; i < IconKinds.Length; i++)
            png($"maps/overworld/icons/{IconKinds[i]}.png",
                Crop(DecodePlanar(game, IconOff + 0x48 * i, 16, 9, palette), 0, 0, CellW, CellH));
        png("maps/overworld/icons/party.png", DecodeSprite(game, SpriteOff, 9, SpriteColours.Select(Gun4).ToArray()));

        var glyphs = new JsonObject();
        for (var i = 0; i < 8; i++)
        {
            var id = 0xE6 + i;
            var name = $"dungeon/glyph-{id:x2}.png";
            png("maps/" + name, Crop(DecodePlanar(game, GlyphOff + 32 * i, 16, GlyphH, palette), 0, 0, GlyphW, GlyphH));
            glyphs[id.ToString()] = name;
        }
        png("maps/dungeon/compass.png", DecodePlanar(game, CompassOff, CompassW, CompassH, palette));

        var overworld = ctx.ReadGameData("phantasie1", "overworld.json")?["regions"] as JsonObject;
        if (overworld is null)
        {
            ctx.Warn("Phantasie I's overworld.json (from the DOS version) is missing, so the Amiga map pictures " +
                     "(maps/overworld/regions, maps/maps.json) were not built; import the DOS copy of Phantasie I and " +
                     "import the Amiga copy again.");
            return;
        }
        var patchesAll = ctx.ReadGameData("phantasie1", "map-patches.json")?["overworld"] as JsonArray;

        var regions = new JsonObject();
        foreach (var n in Regions)
        {
            var data = ctx.Files.Read($"out{n}.dat");
            if (overworld[$"OUT{n}.DAT"]?["tiles"] is not JsonArray tiles)
            {
                ctx.Warn($"Phantasie I's overworld.json has no region OUT{n}.DAT; its Amiga map picture was not built.");
                continue;
            }
            var patches = (patchesAll ?? []).Where(p => (int)p!["region"]! == n)
                .Select(p => (X: (int)p!["x"]!, Y: (int)p["y"]!, Tile: (int)p["tile"]!)).ToList();
            var dos = tiles.Select(row => ((JsonArray)row!).Select(t =>
            {
                var v = (int)t!;
                return v >= Fog ? v - Fog : v;
            }).ToArray()).ToArray();
            foreach (var p in patches)
                dos[p.Y][p.X] = p.Tile;
            var rel = $"overworld/regions/out{n:00}.png";
            png("maps/" + rel, RenderRegion(data, patterns, dos));
            var icons = IconCells(data, patches);
            regions[n.ToString()] = new JsonObject { ["image"] = rel, ["icons"] = icons };
        }

        var manifest = new JsonObject
        {
            ["_source"] = "Amiga game h56 map module (overworld 0x5a4 icons, 0x6bc polygons, 0x948 geometry; dungeon 0xd10 " +
                          "geometry, 0xf76 glyphs, 0xe98 compass), h77 0xaf4/0xbc0/0xdfe; " +
                          "tools/scripts/extract_amiga_map_graphics.py",
            ["overworld"] = new JsonObject
            {
                ["cell_width"] = CellW, ["cell_height"] = CellH,
                ["fog_threshold"] = Fog,
                ["icons"] = new JsonObject(IconKinds.Select(k =>
                    new KeyValuePair<string, JsonNode?>(k, $"overworld/icons/{k}.png"))),
                ["party"] = "overworld/icons/party.png",
                ["regions"] = regions,
            },
            ["dungeon"] = new JsonObject
            {
                ["glyph_width"] = GlyphW, ["glyph_height"] = GlyphH,
                ["glyphs"] = glyphs,
                ["compass"] = "dungeon/compass.png",
            },
        };
        ctx.WriteJsonFile(P1AmigaPipeline.Root + "maps/maps.json", manifest, ensureAscii: true);
    }

    private static int AmigaTile(byte[] data, int x, int y)
    {
        var t = data[(Cols - 1 - x) * Rows + y];
        return t >= Fog ? t - Fog : t;
    }

    /// <summary>h56:0x5a4: columns outer, rows inner; the last hit per kind wins; patches override cells first.</summary>
    private static JsonObject IconCells(byte[] data, List<(int X, int Y, int Tile)> patches)
    {
        var patched = new Dictionary<(int, int), int>();
        foreach (var p in patches)
            patched[(p.X, p.Y)] = p.Tile;
        var order = new List<string>();
        var found = new Dictionary<string, (int X, int Y)>();
        void Hit(string kind, int x, int y)
        {
            if (!found.ContainsKey(kind))
                order.Add(kind);
            found[kind] = (x, y);
        }
        for (var x = 0; x < Cols; x++)
            for (var y = 0; y < Rows; y++)
            {
                var tile = patched.TryGetValue((x, y), out var t) ? t : AmigaTile(data, x, y);
                if (tile / 10 == 4)
                    Hit(tile % 10 == 1 ? "inn" : "town", x, y);
                else if (tile / 10 == 5)
                    Hit("dungeon", x, y);
            }
        var obj = new JsonObject();
        foreach (var k in order)
            obj[k] = new JsonArray(found[k].X, found[k].Y);
        return obj;
    }

    private static List<(int X, int Y)> BlitterLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x1 > x0 ? 1 : -1, sy = y1 > y0 ? 1 : -1;
        var xMajor = dx >= dy;
        int dmax = xMajor ? dx : dy, dmin = xMajor ? dy : dx;
        int err = 2 * dmin - dmax, x = x0, y = y0;
        var pts = new List<(int, int)> { (x, y) };
        for (var i = 0; i < dmax; i++)
        {
            if (err >= 0)
            {
                if (xMajor)
                    y += sy;
                else
                    x += sx;
                err += 2 * (dmin - dmax);
            }
            else
                err += 2 * dmin;
            if (xMajor)
                x += sx;
            else
                y += sy;
            pts.Add((x, y));
        }
        return pts;
    }

    /// <summary>What AreaMove / AreaDraw / AreaEnd covers: the scan-line interior (Pillow's fill) plus every edge as a blitter line.</summary>
    private static bool[] AreaMask(List<(int X, int Y)> pts)
    {
        var mask = new bool[ScreenW * ScreenH];
        PillowPolygon.Fill(mask, ScreenW, ScreenH, pts);
        for (var i = 0; i < pts.Count; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[(i + 1) % pts.Count];
            foreach (var (x, y) in BlitterLine(x0, y0, x1, y1))
                if (x is >= 0 and < ScreenW && y is >= 0 and < ScreenH)
                    mask[y * ScreenW + x] = true;
        }
        return mask;
    }

    private static RgbaImage RenderRegion(byte[] data, List<RgbaImage> patterns, int[][] dos)
    {
        var screen = Filled(ScreenW, ScreenH, new Rgba(0, 0, 0, 255));
        void FillWith(int pattern, Func<int, int, bool> inside)
        {
            var pat = patterns[pattern];
            for (var y = 0; y < ScreenH; y++)
                for (var x = 0; x < ScreenW; x++)
                    if (inside(x, y))
                        Array.Copy(pat.Pixels, ((y & 15) * 16 + (x & 15)) * 4, screen.Pixels, (y * ScreenW + x) * 4, 4);
        }

        int rx0 = OriginX, ry0 = OriginY, rx1 = OriginX + Cols * CellW, ry1 = OriginY + Rows * CellH;
        var off = PolyOff;
        int background = SWord(data, off);
        off += 2;
        FillWith(background, (x, y) => x >= rx0 && x < rx1 && y >= ry0 && y < ry1);
        var polys = new List<(int Pattern, bool[] Mask)>();
        while (true)
        {
            int count = SWord(data, off), pattern = SWord(data, off + 2);
            if (count == -1)
                break;
            off += 4;
            var pts = new List<(int, int)>(count);
            for (var i = 0; i < count; i++)
                pts.Add((SWord(data, off + 4 * i), SWord(data, off + 4 * i + 2)));
            off += 4 * count;
            polys.Add((pattern, AreaMask(pts)));
        }
        for (var y = 0; y < Rows; y++)
            for (var x = 0; x < Cols; x++)
            {
                int ami = AmigaTile(data, x, y) / 10, want = dos[y][x] / 10;
                if (ami == want || ami is 4 or 5 || want is 4 or 5)
                    continue; // towns, inns and dungeons are icons drawn over the terrain, not polygon classes
                var cx = OriginX + x * CellW + CellW / 2;
                var cy = OriginY + y * CellH + CellH / 2;
                for (var k = polys.Count - 1; k >= 0; k--)
                    if (polys[k].Pattern == ami && polys[k].Mask[cy * ScreenW + cx])
                    {
                        polys[k] = (want, polys[k].Mask);
                        break;
                    }
            }
        foreach (var (pattern, mask) in polys)
            FillWith(pattern, (x, y) => mask[y * ScreenW + x]);
        return Crop(screen, rx0, ry0, rx1, ry1);
    }
}
