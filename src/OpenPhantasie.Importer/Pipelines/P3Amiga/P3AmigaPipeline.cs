using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Profiles;
using static OpenPhantasie.Importer.Pipelines.P3Amiga.P3AmigaArt;

namespace OpenPhantasie.Importer.Pipelines.P3Amiga;

/// <summary>
/// Phantasie III, Amiga v1.0 (SSI 1987): III's tables (<see cref="P3AmigaTables.Write"/>, s210: an Amiga-only import is
/// a playable III, with DOS III's values where the Amiga's differ, user s209; DOS III's tables win when that copy is
/// imported too), the Amiga art pack (port of <c>tools/scripts/extract_amiga_p3_graphics.py</c>) plus the raw files
/// its audio reads verbatim (<c>D/m0</c>, <c>D/m1</c> tunes, <c>PhantasieIII</c> sound effects).
/// <para>Inputs: the Amiga disk's <c>Graphics/</c>, <c>D/</c>, <c>monsinfo.dat</c>; III's <c>rule-tables.json</c>
/// (<c>core_race</c>, the portrait file names); for the map previews only, DOS III's <c>WORLD.DAT</c> /
/// <c>PLNS.DAT</c> (profile <c>p3-dos-1988</c>, when handed in the same run). Without the DOS copy the Light / Dark /
/// Netherworld previews are drawn from the Amiga's own <c>D/pln1</c>, <c>D/pln2</c>, <c>D/pln4</c> (the same bytes as
/// the three <c>PLNS.DAT</c> parts, FORMAT.md section 4), and only <c>previews/scandor-dos.png</c> and the Scandor
/// DOS-vs-Amiga cell list are left out. The previews are not used by the pack.</para>
/// <para><c>pack.json</c> is a copy of the shipped <c>graphics/amiga.json</c> (our layout file, class a), as the script
/// does; the script does not write <c>amiga.json</c>.</para>
/// </summary>
public sealed class P3AmigaPipeline : IImportPipeline
{
    public string ProfileId => "p3-amiga-1.0";
    public int RecipeVersion => 2; // 2 (s210): III's tables from the Amiga alone (P3AmigaTables)

    /// <summary>DOS III's tables win when both copies are imported in one run: DOS runs first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p3-dos-1988"];

    private const string Root = "reverse-engineering/amiga/phantasie3/extracted/graphics/";
    private const string Unpacked = "reverse-engineering/amiga/phantasie3/unpacked/PhantasieIII/";

    private const int TileCount = 250;
    private const int PartyTile = 89;

    // Sheet order of the encounter pictures (pointer table DATA 0x220a6, loaded packed by 0x7f3c).
    private static readonly string[] Sheets = ["heros", "purple1", "yellow1", "purple2", "yellow2", "giant1", "giant2"];

    // Race -> first picture of its pair on heros.cmp (byte table 0x239a0, read by 0x1b2b2 with the record's race byte).
    private static readonly int[] RacePicture = [0, 2, 4, 6, 6, 0, 14, 16, 16, 10, 12, 14, 16, 8, 8, 20, 22, 18, 24];

    // The remaster's Race enum (Phantasie I's order), by core_race name.
    private static readonly string[] CoreRaces = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE",
        "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN", "LIZARDMAN", "MINOTAUR", "UNDEAD"];

    // Body-area figure (0x9694): shapes 16 + 7 * state + piece.
    private const int BodyFirstSlot = 16, BodyLastSlot = 36;
    // Text-scroll frame (0x1e2c2): scroll.csh slots 54..68, blitted 8 px wide by 0x1e838.
    private const int ScrollFirstSlot = 54, ScrollLastSlot = 68;
    // The flash (0x1b814): anatomy2.csh slots 37..39.
    private static readonly int[] FlashSlots = [37, 38, 39];
    // dlgbttns.cmp (cut by 0x8012 into slots 1..10): 5 buttons of 64x18, normal at y 0, second state at y 19.
    private static readonly string[] Buttons = ["ok", "cancel", "more", "yes", "no"];

    // Remaster fix (D3-041): byte 27 >= 48 has no picture on the Amiga, so these two borrow the game's own art.
    private static readonly Dictionary<int, (string Note, (string Sheet, int Picture, bool Giant) Cell)> BorrowedPicture = new()
    {
        [65] = ("Wild Dog's picture (byte 27 = 0)", ("yellow1", 0, false)),
        [48] = ("the party elemental's picture (heros cell 22 = frame-box picture 11)", ("heros", 11, false)),
    };

    private sealed class Writer(ImportContext ctx)
    {
        public readonly JsonObject Index = new();
        public readonly Dictionary<string, RgbaImage> Written = new(StringComparer.Ordinal);

        /// <param name="rgba">Pillow mode "RGBA" (written with alpha even when opaque).</param>
        public void Save(RgbaImage im, string rel, bool rgba, JsonObject meta)
        {
            ctx.WritePng(Root + rel, im, forceAlpha: rgba);
            Written[rel] = im;
            Index[rel] = meta;
        }
    }

    private static JsonObject Meta(params (string Key, JsonNode? Value)[] pairs)
    {
        var o = new JsonObject();
        foreach (var (k, v) in pairs)
            o[k] = v;
        return o;
    }

    private static JsonArray Hexes(Rgb[] pal) => new(pal.Select(c => (JsonNode)c.Hex).ToArray());

    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }

    private static List<(string Name, int Byte27)> MonsterRecords(byte[] d)
    {
        var output = new List<(string, int)>();
        for (var i = 0; i < 80; i++)
        {
            var raw = d.AsSpan(i * 38, 14);
            var z = raw.IndexOf((byte)0);
            var name = Encoding.ASCII.GetString(z < 0 ? raw : raw[..z]).Trim();
            output.Add((name, d[i * 38 + 27]));
        }
        return output;
    }

    /// <summary>0x1b4c8: record byte 27 → (sheet, picture on the sheet, giant), or null (&gt;= 48: nothing drawn).</summary>
    private static (string Sheet, int Picture, bool Giant)? MonsterCell(int picture)
    {
        if (picture < 15)
            return ("yellow1", picture, false);
        if (picture < 28)
            return picture - 15 == 12 ? ("heros", 10, false) : ("purple1", picture - 15, false);
        if (picture < 36)
            return ("purple2", picture - 28, false);
        if (picture < 42)
            return ("yellow2", picture - 36, false);
        if (picture < 44)
            return ("giant1", picture - 42, true);
        if (picture < 48)
            return ("giant2", picture - 44, true);
        return null;
    }

    /// <summary>0x1b5f2 (32x48, 10 per row) / 0x1b654 (giants, 80x72, 4 per row); frame 0 idle, 1 attacking.</summary>
    private static (int X0, int Y0, int X1, int Y1) FrameBox(int p, int frame, bool giant)
    {
        var i = 2 * p;
        if (giant)
        {
            int gx = ((i % 4) + frame) * 80, gy = (i / 4) * 72;
            return (gx, gy, gx + 80, gy + 72);
        }
        int x = ((i % 10) + frame) * 32, y = (i / 10) * 48;
        return (x, y, x + 32, y + 48);
    }

    public void Run(ImportContext ctx)
    {
        // III's tables first (unless DOS III's are imported): the portraits read rule-tables.json's core_race.
        ctx.Progress("tables");
        var tables = P3AmigaTables.Write(ctx);

        var files = ctx.Files;
        var w = new Writer(ctx);
        var palettes = new JsonObject();

        // Full screens.
        (string Name, string Src, string Note)[] screens =
        [
            ("title", "Graphics/title.cmp", "title picture (0x73fe)"),
            ("w0", "D/w0.cmp", "D/WX.cmp (string 35), X = 0: the reward scene for an evil win, Pluto (0x1ee82, outcome 3)"),
            ("w1", "D/w1.cmp", "D/WX.cmp, X = 1: the reward scene for a good win, Zeus (0x1ee82, outcome 2)"),
            ("w2", "D/w2.cmp", "D/WX.cmp, X = 2: the party's defeat, the gates of the Astral Plane (0x1dc16 / 0x1dc70)"),
            ("w3", "D/w3.cmp", "town shop-front pieces, loaded at start (0x7788) [~] use"),
            ("w4", "D/w4.cmp", "town picture, loaded at start (0x7776)"),
            ("scroll-iv", "D/s6", "Scroll IV's picture (D/s6 loaded and unpacked by 0x1f7ee)"),
        ];
        ctx.Progress("screens");
        foreach (var (name, src, note) in screens)
        {
            var cmp = ReadCmp(files.Read(src));
            palettes[name] = Hexes(cmp.Palette);
            w.Save(ScreenImage(cmp.Screen, cmp.Palette), $"screens/{name}.png", false, Meta(("source", src), ("note", note)));
        }
        var sheets = new Dictionary<string, Cmp>();
        foreach (var name in Sheets.Append("dlgbttns"))
        {
            var cmp = ReadCmp(files.Read($"Graphics/{name}.cmp"));
            palettes[name] = Hexes(cmp.Palette);
            sheets[name] = cmp;
            w.Save(ScreenImage(cmp.Screen, cmp.Palette), $"sheets/{name}.png", false,
                Meta(("source", $"Graphics/{name}.cmp"), ("note", "whole sheet in its own file palette")));
        }

        // Encounter and party pictures: every picture shows in the heros.cmp palette (the combat screen, 0x19906).
        var combatPal = sheets["heros"].Palette;
        palettes["combat"] = Hexes(combatPal);
        var sheetImg = Sheets.ToDictionary(n => n, n => ScreenImage(sheets[n].Screen, combatPal));

        ctx.Progress("monsters");
        var pictured = new JsonObject();
        var unpictured = new JsonObject();
        var records = MonsterRecords(files.Read("monsinfo.dat"));
        for (var mid = 0; mid < records.Count; mid++)
        {
            var (name, pic) = records[mid];
            var cell = MonsterCell(pic);
            string? borrowed = null;
            if (cell is null)
            {
                unpictured[mid.ToString()] = Meta(("name", name), ("byte27", pic));
                if (!BorrowedPicture.TryGetValue(pic, out var b))
                    continue;
                (borrowed, cell) = (b.Note, b.Cell);
            }
            var (sheet, p, giant) = cell.Value;
            foreach (var (frame, kind) in new[] { (0, "idle"), (1, "attacking") })
            {
                var box = FrameBox(p, frame, giant);
                var meta = Meta(("monster", mid), ("byte27", pic), ("sheet", sheet), ("picture", p), ("giant", giant));
                if (borrowed is not null)
                    meta["borrowed"] = borrowed;
                w.Save(Crop(sheetImg[sheet], box.X0, box.Y0, box.X1, box.Y1), $"monsters/{kind}/{mid:00}-{Slug(name)}.png",
                    false, meta);
            }
            var entry = Meta(("name", name), ("byte27", pic), ("sheet", sheet), ("picture", p), ("giant", giant));
            if (borrowed is not null)
                entry["borrowed"] = borrowed;
            pictured[mid.ToString()] = entry;
        }

        // Portraits, filed by the remaster's race id through rule-tables.json core_race; III's 16 is the elemental.
        var coreRace = ctx.ReadGameData("phantasie3", "rule-tables.json")?["core_race"] as JsonObject;
        if (coreRace is null)
            ctx.Warn("phantasie3/rule-tables.json (core_race) not found: party portraits other than the elemental are not cut");
        for (var race = 0; race < 18; race++)
        {
            var p = RacePicture[race];
            string stem;
            if (race == 16)
                stem = "elemental";
            else if (coreRace?[race.ToString()]?.GetValue<string>() is { } core)
                stem = $"{Array.IndexOf(CoreRaces, core):00}-{Slug(core)}";
            else
                continue;
            foreach (var (frame, kind) in new[] { (0, "idle"), (1, "attacking") })
            {
                int x = ((p % 10) + frame) * 32, y = (p / 10) * 48;
                w.Save(Crop(sheetImg["heros"], x, y, x + 32, y + 48), $"portraits/{kind}/{stem}.png", false,
                    Meta(("p3_race", race), ("picture", p)));
            }
        }

        // The grave (heros sheet x 128, y 96): sprite 16 of 0x1b2b2. portrait.grave.
        w.Save(Crop(sheetImg["heros"], 128, 96, 160, 144), "portraits/grave.png", false, Meta(("sprite", 16), ("sheet", "heros")));

        // Shapes (.csh), each in its file's palette.
        ctx.Progress("shapes");
        (string Name, int First)[] cshSlots = [("anatomy1", 16), ("anatomy2", 31), ("dng", 45), ("scroll", 54)];
        // The body figure is drawn in the main screen's palette = anatomy1.csh's (0x24010).
        var bodyPal = ReadCmp(files.Read("Graphics/anatomy1.csh")).Palette;
        var dngPal = ReadCmp(files.Read("Graphics/dng.csh")).Palette;
        foreach (var (name, first) in cshSlots)
        {
            var src = $"Graphics/{name}.csh";
            var cmp = ReadCmp(files.Read(src));
            palettes[name] = Hexes(cmp.Palette);
            var full = ScreenImage(cmp.Screen, cmp.Palette);
            w.Save(full, $"sheets/{name}.png", false, Meta(("source", src)));
            foreach (var (k, x, y, w0, h) in CshShapes(cmp.Screen))
            {
                var width = name == "dng" ? 8 : w0; // 0x1e838 blits only the first byte of each plane word
                var slot = first + k;
                w.Save(Crop(full, x, y, x + width, y + h), $"shapes/{name}/{slot:00}.png", false,
                    Meta(("source", src), ("shape", k), ("slot", slot)));
                if (name.StartsWith("anatomy", StringComparison.Ordinal) && slot is >= BodyFirstSlot and <= BodyLastSlot)
                    w.Save(IndexedCrop(cmp.Screen, (x, y, x + width, y + h), bodyPal, transparent0: true), $"body/{slot}.png", true,
                        Meta(("source", src), ("shape", k), ("slot", slot),
                            ("note", "body-area figure piece (0x9694), main-screen palette, colour 0 transparent")));
                if (name == "anatomy2" && FlashSlots.Contains(slot))
                {
                    w.Save(IndexedCrop(cmp.Screen, (x, y, x + width, y + h), dngPal, transparent0: true), $"flash/{slot}.png", true,
                        Meta(("source", "Graphics/anatomy2.csh"), ("shape", k), ("slot", slot),
                            ("note", "flash sparkle frame (0x1b814), dungeon palette 0x244de, colour 0 transparent")));
                    w.Save(IndexedCrop(cmp.Screen, (x, y, x + width, y + h), combatPal, transparent0: true),
                        $"flash/combat/{slot}.png", true,
                        Meta(("source", "Graphics/anatomy2.csh"), ("shape", k), ("slot", slot),
                            ("note", "flash sparkle frame (0x1b814) in combat, heros.cmp palette (0x19906), colour 0 transparent")));
                }
                if (name == "scroll" && slot is >= ScrollFirstSlot and <= ScrollLastSlot)
                    w.Save(Crop(full, x, y, x + 8, y + h), $"scroll/{slot}.png", false,
                        Meta(("source", "Graphics/scroll.csh"), ("shape", k), ("slot", slot),
                            ("note", "text-scroll frame piece (0x1e2c2), left 8 px (0x1e838)")));
            }
        }

        // Dialog buttons: 0x8012 cuts slots 1..10 at (64 i, 0) and (64 i, 19), 64x18.
        var btn = sheets["dlgbttns"];
        for (var i = 0; i < Buttons.Length; i++)
            foreach (var (state, y) in new[] { (0, 0), (1, 19) })
                w.Save(IndexedCrop(btn.Screen, (64 * i, y, 64 * i + 64, y + 18), btn.Palette), $"buttons/{Buttons[i]}-{state}.png", true,
                    Meta(("source", "Graphics/dlgbttns.cmp"), ("slot", 2 * i + 1 + state),
                        ("note", "dialog button (0x8012); state 1 = the second row, shown while chosen [~]")));

        // Dungeon map glyphs (slots 45..53, 8x5): the dng shapes again.
        for (var slot = 45; slot < 54; slot++)
            w.Save(w.Written[$"shapes/dng/{slot:00}.png"], $"dungeon/glyph-{slot}.png", false, Meta(("slot", slot)));

        // Overworld tiles.
        ctx.Progress("overworld");
        var set = files.Read("Graphics/phantasy.set");
        var setPal = Palette(set.AsSpan(3, 32));
        palettes["map"] = Hexes(setPal);
        int rows = set[1], cols = set[2];
        var amigaWorld = set.AsSpan(35, rows * cols).ToArray();
        var tiles = set.AsSpan(35 + rows * cols, TileCount * 128).ToArray();
        var tileIms = Enumerable.Range(0, TileCount).Select(i => TileImage(tiles, i, setPal)).ToArray();
        for (var i = 0; i < TileCount; i++)
            w.Save(tileIms[i], $"overworld/tiles/{i:000}.png", true, Meta(("tile", i)));
        w.Save(TileImage(tiles, PartyTile, setPal, transparent0: true), "overworld/party.png", true,
            Meta(("tile", PartyTile), ("note", "0x378a: OR-ed over the cell's tile, colour 0 transparent")));

        RgbaImage Render(ReadOnlySpan<byte> grid, int gw, int gh)
        {
            var im = new RgbaImage(gw * 16, gh * 16);
            for (var y = 0; y < gh; y++)
                for (var x = 0; x < gw; x++)
                    Paste(im, tileIms[Math.Min((int)grid[y * gw + x], TileCount - 1)], x * 16, y * 16);
            return im;
        }

        // Previews (not used by the pack): the Amiga's own Scandor, and the DOS maps the remaster plays, in Amiga tiles.
        w.Save(Render(amigaWorld, cols, rows), "previews/scandor-amiga.png", true, Meta(("note", "phantasy.set's own map")));
        var diffs = new JsonArray();
        var dos = ctx.OptionalFiles("p3-dos-1988");
        if (dos is not null && dos.Has("WORLD.DAT") && dos.Has("PLNS.DAT"))
        {
            var world = dos.Read("WORLD.DAT");
            w.Save(Render(world, 50, 75), "previews/scandor-dos.png", true, Meta(("note", "DOS WORLD.DAT in Amiga tiles")));
            for (var i = 0; i < 3750; i++)
                if (world[i] != amigaWorld[i])
                    diffs.Add(new JsonArray(i % 50, i / 50, (int)world[i], (int)amigaWorld[i]));
            var planes = dos.Read("PLNS.DAT");
            w.Save(Render(planes.AsSpan(0, 81), 9, 9), "previews/plane-light.png", true, Meta(("note", "DOS PLNS.DAT")));
            w.Save(Render(planes.AsSpan(81, 81), 9, 9), "previews/plane-dark.png", true, Meta(("note", "DOS PLNS.DAT")));
            w.Save(Render(planes.AsSpan(162, 496), 31, 16), "previews/netherworld.png", true, Meta(("note", "DOS PLNS.DAT")));
        }
        else
        {
            ctx.Warn("Phantasie III DOS (WORLD.DAT, PLNS.DAT) not given: previews/scandor-dos.png and the Scandor " +
                     "DOS-vs-Amiga cell list are left out; the plane previews are drawn from the Amiga's D/pln1, D/pln2, " +
                     "D/pln4 (the same bytes). The previews are not used by the pack.");
            (string File, int Len, int W, int H, string Out)[] amigaPlanes =
            [
                ("D/pln1", 81, 9, 9, "plane-light"), ("D/pln2", 81, 9, 9, "plane-dark"), ("D/pln4", 496, 31, 16, "netherworld"),
            ];
            foreach (var (file, len, pw, ph, outName) in amigaPlanes)
                w.Save(Render(files.Read(file).AsSpan(0, len), pw, ph), $"previews/{outName}.png", true,
                    Meta(("note", $"Amiga {file} (= the DOS PLNS.DAT part)")));
        }

        var indexJson = new JsonObject
        {
            ["_source"] = "tools/scripts/extract_amiga_p3_graphics.py; notes: reverse-engineering/amiga/phantasie3/FORMAT.md",
            ["palettes"] = palettes,
            ["monsters_pictured"] = pictured,
            ["monsters_without_picture"] = unpictured,
            ["scandor_dos_vs_amiga"] = new JsonObject
            {
                ["_note"] = "[x, y, DOS tile, Amiga tile]; the remaster draws the DOS grid", ["cells"] = diffs,
            },
            ["files"] = w.Index,
        };
        ctx.WriteJsonFile(Root + "extraction-index.json", indexJson, indent: 1, ensureAscii: true);

        if (ctx.ReadShippedBytes("phantasie3", "graphics/amiga.json") is { } pack)
            ctx.WriteFile(Root + "pack.json", pack);
        else
            ctx.Warn("phantasie3/graphics/amiga.json not shipped: pack.json is not written");

        // Raw files the pack's audio reads verbatim (title_sequence.tune, the reward tune, sound_effects).
        foreach (var raw in new[] { "D/m0", "D/m1", "PhantasieIII" })
            ctx.WriteFile(Unpacked + raw, files.Read(raw));

        // The game's own lines of the string tables (10-importer.md 7.6). With the Amiga's tables, the remaster's
        // wording for the lines it lacks (the common shared / I fallback and III's own, as a DOS-only III).
        ImportedStrings.Write(ctx);
        if (tables)
            ImportedStrings.WriteFallback(ctx, ImportedStrings.CommonFallbackGame, P3Dos.P3DosPipeline.Game);
    }
}
