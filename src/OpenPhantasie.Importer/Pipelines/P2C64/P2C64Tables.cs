using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1C64;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P2C64;

/// <summary>
/// Phantasie II's tables from the C64 v1.2 disks alone (s218), so a C64-only import is a playable II. Layouts:
/// <c>reverse-engineering/c64/phantasie2/FORMAT.md</c> sections 2-3. Each C64 table is put into the shape of the Atari
/// ST's extract (<see cref="P2StExtract"/>) and goes through the ST's builders (<see cref="P2GameDataBuilder"/>), so the
/// GameData keeps the ST's schema. As the Apple import (DIFFERENCES-II D2-046..D2-050): the C64's text, and the ST's
/// value in every cell where a C64 number differs (the C64-only ones the same way, FORMAT.md 7), from the shipped
/// remaster data <c>phantasie2/c64-import.json</c> (made by <c>tools/scripts/build_c64_p2_import.py</c>): its
/// <c>st_cells</c>, the ST's monster sprite sizes, the manual's spell names.
/// <para>Played copies: the towns, regions and elementals come from side 2's <c>PATTERN2</c>, the new-game template
/// (FORMAT.md 2.1; never written by play), so a played copy imports as a fresh one; nothing play rewrites is read.</para>
/// <para>When the ST's or the Apple's tables are imported too (this run or earlier), they win and this writes nothing.</para>
/// </summary>
internal static class P2C64Tables
{
    public const string Game = "phantasie2";
    public const string CellsFile = "c64-import.json";
    private const string Platform = "c64";

    /// <summary>Starts every <c>_source</c> this writes.</summary>
    public const string Source = "Phantasie II, C64 v1.2 (reverse-engineering/c64/phantasie2/FORMAT.md)";

    private static readonly CellPatches StCells =
        new("c64", "st", "the C64 copy's data", "the C64's", "the Atari ST's", "the Atari ST value");

    private const int Monsters = 80, MnRecord = 100, ItemNames = 180, CharRecord = 170, ElementalFirst = 21, TownBody = 250;

    /// <summary>
    /// Side 2's <c>PATTERN2</c> (19,566 B, loads at $2000): CHAR22 at 0, town t (1-10) at 4260 + 252 (t - 1), region k of
    /// <see cref="P2StSource.Regions"/> at 6780 + 752 k (750 B each; FORMAT.md 2.1).
    /// </summary>
    private const int PatternSize = 19564, PatternBase = 0x2000, PatternTowns = 4260, PatternRegions = 6780, Slot = 252, RegionSlot = 752;
    private const int RegionBytes = 750, Grid = 520, Features = 520, TextAt = 595, Page = 40;

    /// <summary>The C64's ten towns (the ST's records 0-9; STARVILLE and OLYMPIA are not on the C64).</summary>
    private const int Towns = 10;

    private static readonly string[] Classes = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"];
    private static readonly string[] Races =
        ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
         "GOBLIN", "LIZ.MAN", "MINOTAUR"];
    private static readonly string[] Stats = ["STR", "INT", "DEX", "CON", "CHA"];
    private static readonly string[] SkillOrder =
        ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen", "_42_toss", "pick_lock", "_unused_46", "swim"];

    /// <summary>Write II's tables from the C64; false when the ST's or the Apple's tables (this run or earlier) win.</summary>
    public static bool Write(ImportContext ctx, P2C64Source src)
    {
        if (ctx.ReadImportedGameData(Game, "game.json") is JsonObject existing && (string?)existing["platform"] != Platform)
        {
            ctx.Warn("Phantasie II's game data comes from another copy imported as well (Atari ST or Apple II); the C64 " +
                     "copy adds nothing to it.");
            return false;
        }
        var built = Build(ctx, src, out var warnings);
        foreach (var (file, node) in built)
            ctx.WriteGameData(Game, file, node, indent: 1);
        foreach (var w in warnings)
            ctx.Warn(w);
        return true;
    }

    /// <summary>The C64 file's bytes after its 2-byte PRG load address.</summary>
    private static byte[] Body(byte[] prg) => prg.Length < 2 ? throw new InvalidDataException("a PRG file without a load address") : prg[2..];

    private static int Load(byte[] prg) => prg[0] | prg[1] << 8;

    public static Dictionary<string, JsonObject> Build(ImportContext ctx, P2C64Source src, out List<string> warnings)
    {
        warnings = [];
        var shipped = ctx.ReadShipped(Game, CellsFile) as JsonObject
                      ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile} missing");
        var patternPrg = src.Side2("PATTERN2");
        var pattern = Body(patternPrg);
        if (Load(patternPrg) != PatternBase || pattern.Length != PatternSize)
            throw new InvalidDataException($"PATTERN2: expected {PatternSize} bytes at ${PatternBase:X4}");
        var tinfo = Body(src.Side1("TINFO"));
        if (tinfo.Length < 830 + 6 * 150)
            throw new InvalidDataException($"TINFO: {tinfo.Length} bytes, too short");

        var monsters = MonsterRecords(src.Side1("MN"));
        var names = Names(ctx, src.Side1("TOWN2"), src.Side1("ITEM"), tinfo, shipped);
        var overworld = Overworld(pattern);
        var dungeons = P2StExtract.Dungeons(n => Body(src.Side2($"DNG {n}")), n => Body(src.Side2($"PMES {n}")));
        var messages = P2StExtract.Messages(n => Body(src.Side2($"PMES {n}")));
        foreach (var area in messages["areas"]!.AsArray())
            area!["source"] = $"PMES {(int)area["id"]!}";

        var b = new P2GameDataBuilder(ctx);
        var built = new Dictionary<string, JsonObject>
        {
            ["monsters.json"] = MonstersJson(monsters, shipped),
            ["monster-traits.json"] = P2GameDataBuilder.MonsterTraits(monsters),
            ["items.json"] = Retag(b.Items(names["items"]!.AsArray()),
                $"{Source}: ITEM (names 1-180, its empty line 46 skipped; FORMAT.md 3.2) and TINFO 89 + id (value factors) " +
                "[C]; id 0: TOWN2's NOTHING, its value I's"),
            ["spells.json"] = Retag(b.Spells(names), $"{Source}: the C64 has no spell names (spells are cast by number, " +
                                                    "FORMAT.md 3.10); names from the manual's spell list (D2-050, c64-import.json)"),
            ["names.json"] = Retag(P2GameDataBuilder.Names(names, monsters),
                $"{Source}: TOWN2 DATA (classes, races, status), ITEM, MN; spells from the manual's list"),
            ["overworld.json"] = Retag(P2GameDataBuilder.Overworld(overworld),
                $"{Source}: PATTERN2 (the new-game template) offsets 6780 + 752 k, the 17 regions' 750 bytes (the ST's " +
                "OUTn.DAT 0-749; text pages in screen codes) [C]"),
            ["dungeons.json"] = Retag(P2GameDataBuilder.Dungeons(dungeons, messages),
                $"{Source}: DNG 1..DNG 8 (1962 B, the ST's DNGn level layout; side 2) [C]"),
            ["messages.json"] = Retag(P2GameDataBuilder.Messages(messages),
                $"{Source}: PMES 1..PMES 8 (plain ASCII in the ST's MESSn layout; side 2) [C]"),
            ["scrolls.json"] = Retag(P2GameDataBuilder.Scrolls(Scrolls(src.Side1("SCROLLS"), names)),
                $"{Source}: SCROLLS (REL, 40-byte records; the rows inside each page's frame; FORMAT.md 3.9) [C]"),
            ["towns.json"] = Retag(P2GameDataBuilder.Towns(TownsExtract(pattern, overworld)),
                $"{Source}: PATTERN2 (the new-game template) offsets 4260 + 252 (t - 1), 10 x 250 B [C]"),
            ["creation-tables.json"] = Retag(b.Creation(Creation(tinfo), manualCopy: true),
                $"{Source}: TINFO (1731 B at $9920, I's / the Apple's layout; FORMAT.md 3.3) [C]"),
            ["elementals.json"] = Retag(P2GameDataBuilder.Elementals(Elementals(pattern)),
                $"{Source}: PATTERN2's CHAR22 records 21-23 (170 bytes each; FORMAT.md 3.5) [C]"),
            ["wilderness-encounters.json"] = P2GameDataBuilder.WildernessEncounters(),
        };
        built["monsters.json"]["_fields"] = (string)built["monsters.json"]["_fields"]! +
            " C64 (s218): MN fields 13 (sprite blocks - 1), 14 and 24 are the C64's own [?] and not kept; raw_hex (the ST's " +
            "58-byte record) is not on the C64. sprite_width / sprite_height are the ST's (c64-import.json); every number where " +
            "the C64 differs is the ST's (st_cells: D2-047, FORMAT.md 3.1).";

        // D2-046..049 (user s159) and the C64-only numbers (FORMAT.md 7): the ST's value in every cell where the C64's differs.
        StCells.Apply(shipped["st_cells"]?.AsArray() ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile}: no st_cells"),
            built, warnings);
        built["game.json"] = Manifest(built, ctx.Profile.Id);
        return built;
    }

    private static JsonObject Retag(JsonObject o, string source)
    {
        o["_source"] = source;
        return o;
    }

    // ---- monsters -----------------------------------------------------------------------------------------------

    /// <summary>
    /// MN's 80 records (REL, 100 bytes; 25 CR-ended fields, FORMAT.md 3.1) in the ST extract's shape: field 0 the name,
    /// 1-12 and 15-23 the ST's fields in the ST's order (20 = byte_34); 13, 14 and 24 are the C64's own and not kept.
    /// </summary>
    internal static JsonArray MonsterRecords(byte[] mn)
    {
        if (mn.Length < Monsters * MnRecord)
            throw new InvalidDataException($"MN: expected at least {Monsters * MnRecord} bytes, got {mn.Length}");
        var list = new JsonArray();
        for (var i = 0; i < Monsters; i++)
        {
            var f = Encoding.Latin1.GetString(mn, i * MnRecord, MnRecord).Split('\r');
            if (f.Length < 25)
                throw new InvalidDataException($"MN: record {i + 1} has {f.Length - 1} fields");
            int F(int k) => int.TryParse(f[k], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)
                ? v : throw new InvalidDataException($"MN: record {i + 1} field {k} '{f[k]}' is not a number");
            list.Add(J.O(
                ("index", i), ("name", f[0]),
                ("armor", F(1)), ("damage", F(2)), ("level", F(3)), ("xp", (long)F(4)), ("special_chance", F(5)),
                ("specials", J.Ints([F(6), F(7), F(8)])), ("hp_base", F(9)), ("combat_rating", F(10)),
                ("companions", J.Ints([F(11), F(12)])), ("reaction_modifier", F(15)), ("next_group_chance", F(16)),
                ("defense", F(17)), ("gold", (long)F(18)), ("group_size_factor", F(19)), ("byte_34", F(20)),
                ("treasure_items", J.Ints([F(21), F(22)])), ("attack_power", F(23))));
        }
        return list;
    }

    /// <summary>The ST build's monsters.json (<see cref="P2GameDataBuilder.Monsters"/>) with the ST's sprite sizes.</summary>
    private static JsonObject MonstersJson(JsonArray src, JsonObject shipped)
    {
        var sizes = shipped["sprite_sizes"]?.AsArray() ?? throw new InvalidOperationException($"{CellsFile}: no sprite_sizes");
        if (sizes.Count != Monsters)
            throw new InvalidOperationException($"{CellsFile}: {sizes.Count} sprite sizes, expected {Monsters}");
        // The ST builder checks each sprite's name against the monster's: the C64's names are the sprites' here.
        var gfx = J.O(("monsters", J.A(src.Select((m, i) => (JsonNode?)J.O(("index", i), ("name", J.Clone(m!["name"])),
            ("width", J.Clone(sizes[i]![0])), ("height", J.Clone(sizes[i]![1])))))));
        var o = P2GameDataBuilder.Monsters(src, gfx);
        o["_source"] = $"{Source}: MN (REL, 80 records of 25 CR-ended fields; side 1's, which OUTDOORS2 reads) [C]; sprite " +
                       "sizes the ST's (c64-import.json)";
        return o;
    }

    // ---- names and items ----------------------------------------------------------------------------------------

    /// <summary>
    /// The names in the ST extract's shape (<see cref="P2StExtract.Names"/>): classes, races (16, UNDEAD last) and the
    /// status words from TOWN2's DATA list (THIEF .. WIZARD, HUMAN .. UNDEAD, " ", DEAD, OKAY, SLEEP; FORMAT.md 3.10), the
    /// manual's spell names (c64-import.json), the items: id 0 TOWN2's NOTHING (its value I's, as the Apple import),
    /// ids 1-180 ITEM's lines (the empty line 46 skipped, FORMAT.md 3.2) with their TINFO 89 + id values.
    /// </summary>
    private static JsonObject Names(ImportContext ctx, byte[] town2, byte[] item, byte[] tinfo, JsonObject shipped)
    {
        var data = P1C64Tables.DataList(town2, "THIEF,").Select(d => d.Text).ToList();
        if (data.Count < 26 || data[21] != "UNDEAD" || data[22] != " ")
            throw new InvalidDataException("TOWN2: the class / race / status DATA line was not found");
        if (town2.AsSpan().IndexOf("NOTHING"u8) < 0)
            throw new InvalidDataException("TOWN2: item 0's name (NOTHING) not found");
        var p1 = ctx.ReadShipped("phantasie1", "items.json")?["items"]?.AsArray()
                 ?? throw new InvalidOperationException("shipped phantasie1/items.json missing");
        var items = new JsonArray
        {
            J.O(("index", 0), ("name", "NOTHING"), ("value_factor", J.Clone(p1.First(x => (int)x!["index"]! == 0)!["value_factor"]))),
        };
        var lines = Encoding.Latin1.GetString(item).Split('\r').Where(l => l.Length > 0).Take(ItemNames).ToList();
        if (lines.Count != ItemNames)
            throw new InvalidDataException($"ITEM: {lines.Count} names, expected {ItemNames}");
        for (var id = 1; id <= ItemNames; id++)
            items.Add(J.O(("index", id), ("name", lines[id - 1]), ("value_factor", (int)tinfo[89 + id])));
        var spellNames = (shipped["spell_names"]?.AsArray() ?? throw new InvalidOperationException($"{CellsFile}: no spell_names"))
            .ToDictionary(s => (int)s!["slot"]!, s => (string)s!["name"]!);
        var spells = J.A(Enumerable.Range(0, 60).Select(s => (JsonNode?)J.O(("slot", s), ("number", s + 1),
            ("name", spellNames.TryGetValue(s, out var n) ? n : ""))));
        return J.O(("classes", J.Strs(data.Take(6))), ("races", J.Strs(data.Skip(6).Take(16))),
            ("status", J.Strs(data.Skip(23).Take(3))), ("spells", spells), ("items", items));
    }

    // ---- the overworld ------------------------------------------------------------------------------------------

    /// <summary>
    /// The 17 regions in the ST extract's shape (<see cref="P2StExtract.Overworld"/>): PATTERN2's 750 bytes per region
    /// are the ST's OUTn.DAT 0-749 (grid at (25 - x) * 20 + y, fog = tile + 120; 15 feature records; text pages from
    /// 595), the pages in screen codes (A = 1) made ASCII. The C64 holds pages 0-2 and 35 bytes of page 3.
    /// </summary>
    private static JsonObject Overworld(byte[] pattern)
    {
        var regions = new JsonObject();
        for (var k = 0; k < P2StSource.Regions.Length; k++)
        {
            var r = P2StSource.Regions[k];
            var o = pattern.AsSpan(PatternRegions + RegionSlot * k, RegionBytes).ToArray();
            var tiles = new JsonArray();
            for (var y = 0; y < 20; y++)
                tiles.Add(J.Ints(Enumerable.Range(0, 26).Select(x => (int)o[(25 - x) * 20 + y])));
            var feats = new JsonArray();
            for (var i = 0; i < 15; i++)
            {
                var f = o.AsSpan(Features + 5 * i, 5);
                feats.Add(J.O(("index", i), ("x", 25 - f[1]), ("y", (int)f[0]), ("arg", (int)f[2]), ("dest_x", (int)f[3]),
                    ("dest_y", (int)f[4])));
            }
            var pages = new JsonArray();
            for (var p = 0; TextAt + p * Page < RegionBytes; p++)
            {
                var raw = o.AsSpan(TextAt + p * Page, Math.Min(Page, RegionBytes - TextAt - p * Page));
                var text = Py.Strip(Py.Latin1(Py.UntilNul(P1C64Tables.ScreenPage(raw))));
                if (text.Length > 0)
                    pages.Add(J.O(("index", p), ("text", text)));
            }
            regions[$"OUT{r}.DAT"] = J.O(("region_id", r), ("size", RegionBytes), ("grid_width", 26), ("grid_height", 20),
                ("tiles", tiles), ("features", feats), ("text_pages", pages));
        }
        var rules = new JsonObject();
        foreach (var (key, v) in P2StExtract.ClassRules)
            rules[key] = v;
        return J.O(
            ("_source", "PATTERN2 offsets 6780 + 752 k (the 17 regions, OUTT<n>'s mastered bytes)"),
            ("_note", "The ST's layout and orientation (FORMAT.md 3.6): 26x20 grid stored column-major as (25 - x) * 20 + y; " +
                      "tiles here are [y][x] rows. Fog encodes as tile + 120. Text pages from 595 in screen codes (A = 1), " +
                      "pages 0-3 (the ST's leftover pages 4-9 are not on the C64)."),
            ("grid_width", 26), ("grid_height", 20), ("tile_rules", rules), ("regions", regions));
    }

    // ---- towns --------------------------------------------------------------------------------------------------

    /// <summary>
    /// The ten towns in the ST extract's shape (<see cref="P2StExtract.Towns"/>): the C64's 250-byte record (I's / the
    /// Apple's: stock of ids 1-180, the 45-item restock pool, the price index at 226, the exit region / row y / 25 - x
    /// at 227-229, the name at 230, 0x15-padded; FORMAT.md 3.4).
    /// </summary>
    private static JsonObject TownsExtract(byte[] pattern, JsonObject overworld)
    {
        var townCells = new Dictionary<int, JsonArray>();
        foreach (var (_, node) in overworld["regions"]!.AsObject())
        {
            var reg = node!.AsObject();
            var feats = new Dictionary<(int, int), int>();
            foreach (var f in reg["features"]!.AsArray())
                feats[((int)f!["x"]!, (int)f["y"]!)] = (int)f["arg"]!;
            var tiles = reg["tiles"]!.AsArray();
            for (var x = 0; x < 26; x++)
                for (var y = 0; y < 20; y++)
                    if (P2StSource.Unfog((int)tiles[y]![x]!) == 40 && feats.TryGetValue((x, y), out var arg) && arg != 0)
                    {
                        if (!townCells.TryGetValue(arg - 1, out var list))
                            townCells[arg - 1] = list = [];
                        list.Add(J.O(("region", J.Clone(reg["region_id"])), ("x", x), ("y", y)));
                    }
        }
        var towns = new JsonArray();
        for (var i = 0; i < Towns; i++)
        {
            var a = pattern.AsSpan(PatternTowns + Slot * i, TownBody);
            towns.Add(J.O(
                ("id", i + 1), ("index", i),
                ("name", Py.Strip(P1C64Tables.Printed(a[230..250]))),
                ("price_index", (int)a[226]),
                ("stock", J.Ints(new[] { 0 }.Concat(a[..180].ToArray().Select(v => (int)v)))),
                ("restock_items", J.Ints(a[180..225].ToArray().Select(v => (int)v))),
                ("exit_region", (int)a[227]),
                ("exit_x", 25 - a[229]),
                ("exit_y", (int)a[228]),
                ("map_town_cells", townCells.TryGetValue(i, out var cells) ? cells : new JsonArray())));
        }
        return J.O(
            ("_layout", J.O(("record_stride", TownBody), ("town_count", Towns), ("stock", "bytes 0..179 = item ids 1..180"),
                ("restock_items", "180..224"), ("slot_225", "unread"), ("price_index", "226"),
                ("exit", "227 region, 228 row y, 229 column 25 - x"), ("name", "230..249, padded with 0x15"))),
            ("_notes", J.Strs([
                "PATTERN2's ten towns (the new-game template; TWN21..TWN210 are the played copies) [C]; exit_x / exit_y in the " +
                "ST's map convention. The ST's unused STARVILLE / OLYMPIA records are not on the C64.",
                "map_town_cells: cells with tile 40 whose feature arg - 1 == this town index.",
                "D2-048 (user s159): the SCROLL D / E stock of IPSWICH and FERRON are the ST's; FERRON's one MAGIC 5 too " +
                "(the C64-only numbers, s217).",
                "Restock: the C64's rule is not read [?]; the remaster plays the ST rule (D2-045).",
            ])),
            ("towns", towns));
    }

    // ---- creation tables ----------------------------------------------------------------------------------------

    /// <summary>
    /// TINFO in the ST extract's creation-table shape (<see cref="P2StExtract.CreationTables"/>): I's / the Apple's
    /// layout (FORMAT.md 3.3): HP factors 0, race modifiers 275, class modifiers 350, spell levels 380, MP ceiling 740,
    /// skill coefficients 830 (one byte each; the ST stores u16).
    /// </summary>
    private static JsonObject Creation(byte[] t)
    {
        JsonObject ByClass(int off, int n) => J.O(Classes.Select((c, i) => (c, (JsonNode?)J.Ints(t.AsSpan(off + i * n, n).ToArray().Select(b => (int)b)))).ToArray());
        JsonObject Attr(int off) => J.O(Stats.Select((s, k) => (s, (JsonNode?)(int)t[off + k])).ToArray());
        var sk = J.O(Classes.Select((c, i) => (c, (JsonNode?)J.A(Enumerable.Range(0, 15).Select(lv => (JsonNode?)J.Ints(
            Enumerable.Range(0, 10).Select(k => (int)t[830 + i * 150 + lv * 10 + k])))))).ToArray());
        var rm = J.O(Races.Select((r, i) => (r, (JsonNode?)J.O(
            ("modifiers", Attr(275 + i * 5)),
            ("ceilings", J.O(Stats.Select((s, k) => (s, (JsonNode?)(t[275 + i * 5 + k] + 8))).ToArray()))))).ToArray());
        return J.O(
            ("_schema", "phantasie.creation-tables/v1 (shape of GameData/phantasie1/creation-tables.json, raw values)"),
            ("hp_factors", J.O(("offset", "0"), ("layout", "class * 15 + level"), ("by_class", ByClass(0, 15)))),
            ("mp_ceiling", J.O(("offset", "740"), ("layout", "class * 15 + level"), ("by_class", ByClass(740, 15)))),
            ("skill_coefficients", J.O(("offset", "830"), ("layout", "class * 150 + level * 10 + skill (u8)"),
                ("skill_order", J.Strs(SkillOrder)), ("by_class_level", sk))),
            ("class_attribute_modifiers", J.O(("offset", "350"), ("by_class", J.O(Classes.Select((c, i) => (c, (JsonNode?)Attr(350 + i * 5))).ToArray())))),
            ("race_attribute_modifiers", J.O(("offset", "275"),
                ("note", "15 races x 5 bytes. ceiling = modifier + 8 as in Phantasie I [~] for II"), ("by_race", rm))),
            ("spell_levels", J.O(("offset", "380"), ("layout", "class * 60 + spell slot (u8); 0 = never"), ("by_class", ByClass(380, 60)))));
    }

    // ---- elementals ---------------------------------------------------------------------------------------------

    /// <summary>
    /// PATTERN2's CHAR22 records 21-23 in the ST extract's shape (<see cref="P2StExtract.Elementals"/>; I's 170-byte
    /// record, FORMAT.md 3.5): INT, MP ceiling / max / current, HP, status, attack, parry, level, creation number, the
    /// points, race, class, the 60 spell flags, the 12-byte name (backspaces applied). The creation numbers, race and MP
    /// ceiling where the C64's differ take the ST's (st_cells).
    /// </summary>
    private static JsonArray Elementals(byte[] pattern)
    {
        var list = new JsonArray();
        for (var i = 0; i < 3; i++)
        {
            var p = pattern.AsSpan((ElementalFirst + i) * CharRecord, CharRecord).ToArray();
            var known = Enumerable.Range(0, 60).Where(k => p[61 + k] == 1).ToList();
            list.Add(J.O(
                ("name", Py.Strip(P1C64Tables.Printed(p.AsSpan(131, 12)))),
                ("intelligence", (int)p[2]), ("mp_ceiling", (int)p[9]), ("magic_max", (int)p[10]), ("magic_cur", (int)p[11]),
                ("hp_max", (int)p[12]), ("hp_cur", (int)p[13]), ("status", (int)p[18]), ("attack", (int)p[20]),
                ("parry", (int)p[22]), ("level", (int)p[54]), ("guild_number", (int)p[59]), ("birth_race", (int)p[161]),
                ("class", (int)p[151]), ("weapon_damage_b", (int)p[162]), ("shield_soak", (int)p[163]),
                ("armor_soak", (int)p[164]), ("race", (int)p[161]), ("known_spells", J.Ints(known))));
        }
        return list;
    }

    // ---- scrolls ------------------------------------------------------------------------------------------------

    /// <summary>PETSCII of the lower / upper-case set to ASCII (as <c>P1C64Tables</c>): 0xC1..0xDA capitals, 0x41..0x5A small.</summary>
    private static char Petscii(byte b) => b switch
    {
        >= 0xC1 and <= 0xDA => (char)(b - 0x80),
        >= 0x41 and <= 0x5A => (char)(b + 0x20),
        >= 0x20 and < 0x41 or 0x5B or 0x5D => (char)b,
        _ => throw new InvalidDataException($"SCROLLS: PETSCII 0x{b:x2} outside the text"),
    };

    /// <summary>
    /// SCROLLS' 20 pages (REL, 40-byte records; FORMAT.md 3.9): records 1-4 the index (page k = records idx[k] ..
    /// idx[k+1] - 1, k = 1-20 = items 121-140); each page's rows inside its <c>+---+</c> / <c>! … !</c> frame, the
    /// frame's blank first and last rows dropped (as I's C64 import). Page 17 (item 137) is empty, as on the ST.
    /// </summary>
    internal static List<List<string>> ScrollPages(byte[] data)
    {
        const int rec = 40;
        var idx = new List<int>();
        for (var n = 1; n <= 4; n++)
            foreach (var tok in Encoding.ASCII.GetString(data, (n - 1) * rec, rec).Split('\r'))
                if (int.TryParse(tok.Trim('\0', ' '), NumberStyles.None, CultureInfo.InvariantCulture, out var v))
                    idx.Add(v);
        if (idx.Count != 21 || data.Length < (idx[^1] - 1) * rec)
            throw new InvalidDataException($"SCROLLS: index of {idx.Count} entries (expected 21)");
        var pages = new List<List<string>>();
        for (var k = 0; k < 20; k++)
        {
            var rows = new List<string>();
            for (var n = idx[k]; n < idx[k + 1]; n++)
            {
                var r = data.AsSpan((n - 1) * rec, rec);
                var cr = r.IndexOf((byte)'\r');
                var row = cr < 0 ? r : r[..cr];
                var first = row.IndexOf((byte)'!');
                if (first < 0)
                    continue; // the +---+ borders
                // SCROLL C's row 114 lacks its closing '!' (the C64's data): the row runs to its CR.
                var last = row.LastIndexOf((byte)'!');
                var end = last > first ? last : row.Length;
                rows.Add(Py.RStrip(string.Concat(row[(first + 1)..end].ToArray().Select(Petscii))));
            }
            while (rows.Count > 0 && rows[0].Length == 0)
                rows.RemoveAt(0);
            while (rows.Count > 0 && rows[^1].Length == 0)
                rows.RemoveAt(rows.Count - 1);
            pages.Add(rows);
        }
        return pages;
    }

    /// <summary>
    /// The lettered scrolls in the ST extract's shape (<see cref="P2StExtract.Scrolls"/>): the ST's item → record map
    /// (item i in 121..140 with a name other than SCROLL: first record (i > 137 ? i - 1 : i) - 121; SCROLL A shows 3
    /// records, the 132 and 134 scrolls 2 and 3), the ST's record r being the C64's page r + 1 (r ≤ 15) or r + 2 (the
    /// C64 keeps the empty page 17 that the ST drops).
    /// </summary>
    private static JsonObject Scrolls(byte[] data, JsonObject names)
    {
        var pages = ScrollPages(data);
        var nameOf = names["items"]!.AsArray().ToDictionary(it => (int)it!["index"]!, it => (string)it!["name"]!);
        var scrolls = new JsonArray();
        var id = 0;
        for (var item = 121; item <= 140; item++)
        {
            var name = nameOf.GetValueOrDefault(item);
            if (string.IsNullOrEmpty(name) || Py.Strip(name) == "SCROLL")
                continue;
            var first = (item > 137 ? item - 1 : item) - 121;
            var extra = item switch { 121 => 2, 132 => 1, 134 => 2, _ => 0 };
            var recs = Enumerable.Range(first, extra + 1).ToList();
            var lines = new List<string>();
            foreach (var r in recs)
            {
                var page = pages[r <= 15 ? r : r + 1];
                if (page.Count == 0)
                    throw new InvalidDataException($"SCROLLS: {name}'s page {(r <= 15 ? r + 1 : r + 2)} is empty");
                if (lines.Count > 0)
                    lines.Add("");
                lines.AddRange(page);
            }
            scrolls.Add(J.O(("id", ++id), ("item_id", item), ("name", name), ("records", J.Ints(recs)),
                ("lines", J.Strs(lines)), ("text", string.Join("\n", lines.Where(l => Py.Strip(l).Length > 0)))));
        }
        return J.O(
            ("_source", "SCROLLS"),
            ("_layout", "PETSCII (lower / upper-case set) pages inside a +---+ / ! ... ! frame; records 1-4 the index " +
                        "(reverse-engineering/c64/phantasie2/FORMAT.md 3.9). lines = the rows inside the frame (a blank " +
                        "line between the pages of one scroll). records = the ST's record numbers (C64 page = record + 1, " +
                        "from record 16 record + 2)."),
            ("_item_map", "As the ST (PHANT.PRG 0xe1e8): item i in 121..140 -> first record (i > 137 ? i - 1 : i) - 121; " +
                          "SCROLL A (121) shows 3 records, the 132 and 134 scrolls 2 and 3. The C64 shows page i - 120 and " +
                          "the pages after it (the same text) [C]."),
            ("scrolls", scrolls));
    }

    // ---- manifest -----------------------------------------------------------------------------------------------

    private static JsonObject Manifest(IReadOnlyDictionary<string, JsonObject> built, string profile)
    {
        int Count(string file, string key) => built[file][key] switch
        {
            JsonArray a => a.Count,
            JsonObject o => o.Count,
            _ => throw new InvalidOperationException(key),
        };
        return J.O(
            ("_schema", "phantasie.game/v1"),
            ("_source", $"Built by the importer ({profile}) from {Source}"),
            ("_note", "II's tables from the C64 copy: its text, the Atari ST's numbers (c64-import.json). No C64 graphics " +
                      "pack yet (FORMAT.md section 6)."),
            ("game", "p2"),
            ("platform", Platform),
            ("profile", profile),
            ("counts", J.O(
                ("monsters", Count("monsters.json", "monsters")),
                ("items", Count("items.json", "items")),
                ("spells", Count("spells.json", "spells")),
                ("classes", Count("names.json", "classes")),
                ("races", Count("names.json", "races")),
                ("overworld_regions", Count("overworld.json", "regions")),
                ("dungeon_levels", Count("dungeons.json", "levels")),
                ("message_areas", Count("messages.json", "areas")),
                ("scrolls", Count("scrolls.json", "scrolls")),
                ("towns", Count("towns.json", "towns")),
                ("elementals", Count("elementals.json", "elementals")))),
            ("spell_cost_rule", null),
            ("_spell_cost_rule", "As the ST build: (slot % 4) + 1 for every spell, filled in spells.json."));
    }
}
