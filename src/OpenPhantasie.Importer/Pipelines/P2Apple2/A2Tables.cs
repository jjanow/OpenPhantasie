using System.Globalization;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// Port of <c>tools/scripts/extract_a2_p2_tables.py</c>: the Apple II tables in the extract's own shape
/// (<c>reverse-engineering/apple2/phantasie2/extracted/*.json</c>, ST field names, Apple-only fields under <c>a2</c>).
/// The GameData is built from these by <see cref="A2GameData"/>.
/// </summary>
internal static class A2Tables
{
    public const string Source = "Phantasie II, Apple II v1.0 2/5/86 (4am crack; reverse-engineering/apple2/phantasie2/FORMAT.md)";
    public static readonly int[] Regions = [1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31];
    public static readonly string[] ClassesOrder = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"];
    private static readonly string[] Attrs = ["STR", "INT", "DEX", "CON", "CHA"];
    private static readonly string[] Skills = ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen", "_42_toss", "pick_lock",
        "_unused_46", "swim"];

    public static readonly string[] MonsterFields = ["name", "armor", "damage", "level", "xp", "special_chance", "special_0", "special_1", "special_2",
        "hp_base", "combat_rating", "companion_0", "companion_1", "a2_scale", "a2_hcolor",
        "reaction_modifier", "next_group_chance", "defense", "gold", "group_size_factor",
        "a2_group_height", "treasure_0", "treasure_1", "attack_power", "a2_shape"];

    private static int PyInt(byte[] b) => int.Parse(System.Text.Encoding.ASCII.GetString(b).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    /// <summary>The 25 CR-ended fields of each of the 80 monsters (MN20..MN29).</summary>
    public static List<List<byte[]>> MonsterRecords(A2Source src)
    {
        var recs = new List<List<byte[]>>();
        for (var f = 0; f < 10; f++)
        {
            var fields = A2Text.SplitCr(A2Text.Low7(src.A($"MN2{f}")));
            for (var h = 0; h < 8; h++)
                recs.Add(fields.GetRange(h * 25, 25));
        }
        return recs;
    }

    public static JsonObject Monsters(A2Source src)
    {
        var list = new JsonArray();
        var recs = MonsterRecords(src);
        for (var idx = 0; idx < recs.Count; idx++)
        {
            var rec = recs[idx];
            var v = new Dictionary<string, int>();
            for (var k = 1; k < 25; k++)
                v[MonsterFields[k]] = PyInt(rec[k]);
            list.Add(J.O(
                ("id", $"p2:{idx}"), ("index", idx), ("name", A2Text.CrString(rec[0])),
                ("armor", v["armor"]), ("damage", v["damage"]), ("level", v["level"]), ("xp", v["xp"]),
                ("special_chance", v["special_chance"]),
                ("specials", J.Ints([v["special_0"], v["special_1"], v["special_2"]])),
                ("hp_base", v["hp_base"]), ("combat_rating", v["combat_rating"]),
                ("companions", J.Ints([v["companion_0"], v["companion_1"]])),
                ("reaction_modifier", v["reaction_modifier"]), ("next_group_chance", v["next_group_chance"]),
                ("defense", v["defense"]), ("gold", v["gold"]), ("group_size_factor", v["group_size_factor"]),
                ("treasure_items", J.Ints([v["treasure_0"], v["treasure_1"]])), ("attack_power", v["attack_power"]),
                ("a2", J.O(("shape", v["a2_shape"]), ("scale", v["a2_scale"]), ("hcolor", v["a2_hcolor"]),
                    ("group_height", v["a2_group_height"])))));
        }
        return J.O(
            ("_source", $"{Source}: MN20..MN29, 8 records x 25 CR-ended fields (OUTDOORS2 line 910)"),
            ("_fields", "ST names where the field means the same (GameData/phantasie2/monsters.json). a2.shape = SHAPES " +
                        "shape number of pose A (pose B = shape + 1), a2.scale = SCALE, a2.hcolor = HCOLOR and the " +
                        "group's x offset (DUNGON2 1540-1550), a2.group_height = the rank's height in pixels " +
                        "(OUTDOORS2 1480-1500; the ST keeps the sprite height for this). reaction_modifier < -10 marks " +
                        "Pluto's minion -(10 + n) (OUTDOORS2 916)."),
            ("monsters", list));
    }

    public static List<string> ItemNames(A2Source src) =>
        A2Text.SplitCr(A2Text.Low7(src.A("ITEM"))).Take(180).Select(n => A2Text.CrString(n)).ToList();

    public static JsonObject Items(A2Source src)
    {
        var names = ItemNames(src);
        var tinfo = src.A("TINFO");
        return J.O(
            ("_source", $"{Source}: ITEM (names, ids 1..180, read by TOWN2 line 230) and TINFO +89+id (value factor; " +
                        "price = INT(f^2 / 5 * town factor + 1), TOWN2 1117)"),
            ("_note", "Id 0 (NOTHING) has no name or factor on the Apple; kind / points / min_str are index formulas " +
                      "shared with the ST build (GameData/phantasie2/items.json), not disk data."),
            ("items", J.A(Enumerable.Range(1, 180).Select(i => (JsonNode?)J.O(("id", $"p2:{i}"), ("index", i),
                ("name", names[i - 1]), ("value_factor", (int)tinfo[89 + i]))))));
    }

    private static List<string> DataList(string line) =>
        line.Split("DATA", 2)[1].Split(',').Select(A2Text.Strip).ToList();

    public static JsonObject Names(A2Source src, JsonObject monsters)
    {
        var town = A2Text.Program(src.A("TOWN2"));
        var outd = A2Text.Program(src.A("OUTDOORS2"));
        var classes = DataList(town[260]);
        var races = DataList(town[270]).Where(r => r.Length > 0).ToList();
        var status = outd[290].Split(':').Where(p => p.Contains('"')).Select(p => p.Split('"')[1]).ToList();
        var terrain = DataList(outd[120]);
        var kinds = DataList(outd[280]);
        JsonArray Idx(IEnumerable<string> xs) => J.A(xs.Select((n, i) => (JsonNode?)J.O(("index", i), ("name", n))));
        return J.O(
            ("_source", $"{Source}: TOWN2 lines 260 / 270 (DATA), OUTDOORS2 lines 120 / 280 / 290, ITEM, MN2x"),
            ("_note", "The Apple II has no spell names: spells are listed and cast by number 1..60 (TOWN2 3060, 6882)."),
            ("classes", Idx(classes)),
            ("races", Idx(races)),
            ("status", Idx(status)),
            ("items", J.A(ItemNames(src).Select((n, i) => (JsonNode?)J.O(("index", i + 1), ("name", n))))),
            ("monsters", J.A(monsters["monsters"]!.AsArray().Select(m => (JsonNode?)J.O(("index", J.Clone(m!["index"])), ("name", J.Clone(m["name"])))))),
            ("spells", new JsonArray()),
            ("a2", J.O(("terrain_names", J.Strs(terrain)), ("treasure_kinds", J.Strs(kinds)))));
    }

    private static readonly (string Key, int Offset)[] TinfoLayout =
    [
        ("hp_factors", 0), ("value_factors", 89), ("race_attribute_modifiers", 275), ("class_attribute_modifiers", 350),
        ("spell_levels", 380), ("mp_ceiling", 740), ("skill_coefficients", 830),
    ];

    public static JsonObject CreationTables(A2Source src, JsonObject names)
    {
        var t = src.A("TINFO");
        var l = TinfoLayout.ToDictionary(x => x.Key, x => x.Offset);
        var races = names["races"]!.AsArray().Select(r => (string)r!["name"]!).Take(15).ToList();
        JsonArray Bytes(int off, int n) => J.Ints(Enumerable.Range(off, n).Select(i => (int)t[i]));
        JsonObject By(int off, int n)
        {
            var o = new JsonObject();
            for (var i = 0; i < ClassesOrder.Length; i++)
                o[ClassesOrder[i]] = Bytes(off + i * n, n);
            return o;
        }
        JsonObject Attr(int off)
        {
            var o = new JsonObject();
            for (var k = 0; k < 5; k++)
                o[Attrs[k]] = (int)t[off + k];
            return o;
        }
        var offsets = new JsonObject();
        foreach (var (k, v) in TinfoLayout)
            offsets[k] = v;
        var skill = new JsonObject();
        for (var i = 0; i < ClassesOrder.Length; i++)
            skill[ClassesOrder[i]] = J.A(Enumerable.Range(0, 15).Select(lv => (JsonNode?)Bytes(l["skill_coefficients"] + i * 150 + lv * 10, 10)));
        var cls = new JsonObject();
        for (var i = 0; i < ClassesOrder.Length; i++)
            cls[ClassesOrder[i]] = Attr(l["class_attribute_modifiers"] + i * 5);
        var race = new JsonObject();
        for (var i = 0; i < races.Count; i++)
            race[races[i]] = J.O(("modifiers", Attr(l["race_attribute_modifiers"] + i * 5)));
        return J.O(
            ("_schema", "a2 raw creation tables (cells named as in GameData/phantasie2/creation-tables.json)"),
            ("_source", $"{Source}: TINFO, 1730 B loaded at $08FC (TOWN2 830); offsets in a2_offsets"),
            ("a2_offsets", offsets),
            ("hp_factors", J.O(("layout", "class * 15 + level"), ("by_class", By(l["hp_factors"], 15)))),
            ("mp_ceiling", J.O(("layout", "class * 15 + level"), ("by_class", By(l["mp_ceiling"], 15)))),
            ("spell_levels", J.O(("layout", "class * 60 + spell slot; 0 = never"), ("by_class", By(l["spell_levels"], 60)))),
            ("skill_coefficients", J.O(("layout", "class * 150 + level * 10 + skill (u8; the ST stores u16)"),
                ("skill_order", J.Strs(Skills)), ("by_class_level", skill))),
            ("class_attribute_modifiers", J.O(("by_class", cls))),
            ("race_attribute_modifiers", J.O(("by_race", race))),
            ("a2_unused_bytes", J.O(("tinfo_270_274", Bytes(270, 5)))));
    }

    public static JsonObject Towns(A2Source src)
    {
        var tf = src.A("TF2");
        var towns = new JsonArray();
        for (var n = 0; n < 10; n++)
        {
            var r = tf.AsSpan(n * 250, 250);
            var name = r[230..250].ToArray().Where(b => b != 0x15).ToArray();
            towns.Add(J.O(
                ("id", n + 1), ("index", n), ("name", A2Text.CrString(name)),
                ("price_index", (int)r[226]),
                ("stock", J.Ints(new[] { 0 }.Concat(r[..180].ToArray().Select(b => (int)b)))),
                ("restock_items", J.Ints(r[180..225].ToArray().Select(b => (int)b))),
                ("exit_region", (int)r[227]), ("exit_x", 25 - r[229]), ("exit_y", (int)r[228]),
                ("a2", J.O(("restock_slot_45", (int)r[225]), ("name_raw", Py.Hex(r[230..250]))))));
        }
        return J.O(
            ("_source", $"{Source}: TF2 (10 x 250 B, the new-game template BACKUP 500-550 copies to TWN21..TWN210)"),
            ("_layout", J.O(("record_stride", 250), ("stock", "bytes 0..179 = item ids 1..180 (LA+1+id-1)"),
                ("restock_items", "180..224 (TOWN2 860 reads LA+181+RND*L*3, L <= 15 -> slots 0..44)"),
                ("slot_45", "225, never read"), ("price_index", "226 (TOWN2 825: /100)"),
                ("exit", "227 region, 228 row y, 229 column 25 - x (TOWN2 6020 -> SA+1191..1193)"),
                ("name", "230..249, padded with 0x15"))),
            ("towns", towns));
    }

    public static JsonObject Overworld(A2Source src, bool forGameData = false)
    {
        var regions = new JsonObject();
        foreach (var r in Regions)
        {
            var o = src.A($"OUTT{r}");
            var tiles = new int[20, 26];
            var rows = new JsonArray();
            for (var y = 0; y < 20; y++)
            {
                for (var x = 0; x < 26; x++)
                    tiles[y, x] = o[(25 - x) * 20 + y];
                rows.Add(J.Ints(Enumerable.Range(0, 26).Select(x => tiles[y, x])));
            }
            var feats = new List<JsonObject>();
            for (var i = 0; i < 15; i++)
            {
                var b = o.AsSpan(520 + i * 5, 5);
                feats.Add(J.O(("index", i), ("x", 25 - b[1]), ("y", (int)b[0]), ("arg", (int)b[2]), ("dest_x", (int)b[3]), ("dest_y", (int)b[4])));
            }
            var pages = new JsonArray();
            for (var p = 0; p < (o.Length - 595) / 40; p++)
            {
                var first = A2Text.Low7(o.AsSpan(595 + 40 * p, Math.Min(40, o.Length - 595 - 40 * p)));
                if (!A2Text.BlankSlot(first))
                    pages.Add(J.O(("index", p), ("text", A2Text.CrString(o.AsSpan(595 + 40 * p)))));
            }
            foreach (var f in feats)
            {
                int x = (int)f["x"]!, y = (int)f["y"]!, arg = (int)f["arg"]!;
                if (x is >= 0 and < 26 && y is >= 0 and < 20 && tiles[y, x] % 120 % 10 == 6)
                {
                    var start = 595 + 40 * arg;
                    var text = start < o.Length ? o.AsSpan(start) : [];
                    f["a2_text"] = A2Text.CrString(text);
                    if (forGameData)
                        f["a2_lines"] = J.Strs(A2Text.ScreenText(text, stopAtCr: true).Select(A2Text.Strip));
                }
            }
            regions[$"OUT{r}.DAT"] = J.O(("region_id", r), ("size", o.Length), ("grid_width", 26), ("grid_height", 20),
                ("tiles", rows), ("features", J.A(feats)), ("a2_text_pages", pages), ("a2_played", o.Length == 756));
        }
        return J.O(
            ("_source", $"{Source}: OUTT<n> (768 B as shipped; OUTDOORS2 450). " +
                        "OUTDOORS2 7700-7710 STOREs the region back (756 B) on leaving it; the only bytes play changes are " +
                        "the fog flags (line 5030), so a stored-back file (a2_played) has the true tiles / features / text but " +
                        "maybe not the starting fog. OUT2 ships stored back (the Memorial Set's unplayed disk, s166: OUT1 / OUT2 " +
                        "open, the rest fogged, as the ST); a played copy is told by the profile's pristine sha1s. In the " +
                        "reference dump only OUT2, OUT26 and OUT30 are as shipped"),
            ("_note", "OUTDOORS2 reads cell (CX, CY) at LA + CY*20 + CX (CX 0..19, CY 0..25): the ST's (25 - x) * 20 + y " +
                      "with CX = y, CY = 25 - x, so tiles here are ST-oriented [y][x] rows. The Apple draws CX across " +
                      "and CY down (140 + 7*CX, 2 + 6*CY), i.e. the ST map turned a quarter. Fog = tile + 120. Features " +
                      "520..594 (CX, CY, arg, dest...). Text: one CR-ended string from 595 + 40*arg (CALL 800), " +
                      "a2_text_pages lists each non-empty start slot as displayed."),
            ("regions", regions));
    }

    public const int LevelBytes = 1740;
    public const int MsgBase = 1700;

    private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);

    public static JsonObject Dungeons(A2Source src, bool forGameData = false)
    {
        var levels = new JsonObject();
        for (var n = 1; n <= 8; n++)
        {
            var d = src.B($"DNG{n}");
            var low = A2Text.Low7(d);
            var msgs = new JsonObject();
            var stream = new JsonArray();
            var coveredTo = 0;
            for (var mid = 1; mid < (d.Length - MsgBase) / 40; mid++)
            {
                var start = MsgBase + 40 * mid;
                var slot = low.AsSpan(start, Math.Min(40, d.Length - start));
                if (start < coveredTo || A2Text.BlankSlot(slot))
                    continue;
                var end = start + low.AsSpan(start).IndexOf((byte)0x0D);
                coveredTo = end + 1;
                var lines = A2Text.ScreenText(end + 1 > start ? d.AsSpan(start, end + 1 - start) : [], stopAtCr: true);
                msgs[mid.ToString(CultureInfo.InvariantCulture)] = J.O(("lines", J.Strs(lines)), ("a2_slots", FloorDiv(end - start, 40) + 1));
                foreach (var ln in lines)
                    stream.Add(ln);
            }
            levels[$"DNG{n}"] = J.O(("id", $"p2:{n}"), ("size", d.Length), ("raw_hex", Py.Hex(d.AsSpan(0, LevelBytes))),
                ("messages", msgs), ("a2_text_stream", stream));
            if (forGameData)
                levels[$"DNG{n}"]!["raw_full_hex"] = Py.Hex(d.AsSpan(0, Math.Min(2048, d.Length)));
        }
        return J.O(
            ("_source", $"{Source}: DNG1..DNG8 (6140 B, side B). Bytes 0..1739 = the Phantasie I level format (grid " +
                        "33 x 38 at LA + 33*CY + CX, events at 1270 = 3320 - LA, scripts at 1450 = 3500 - LA, level " +
                        "byte 1255), the rest = 40-B message slots (DUNGON2 8650, printed by CALL 800 until CR)"),
            ("_note", "Messages are listed by slot id; a message that runs past 40 chars continues in the next slot " +
                      "(the ST cuts the same text into 41-B records). raw_hex holds only the level part."),
            ("levels", levels));
    }

    public static JsonObject Scrolls(A2Source src)
    {
        var pages = new SortedDictionary<int, List<string>>();
        foreach (var f in src.FilesA)
            if (f.Path.StartsWith("SCROLLL", StringComparison.Ordinal))
                pages[int.Parse(f.Path[7..], CultureInfo.InvariantCulture)] = A2Text.TextPage(f.Data);
        var chains = new JsonArray();
        for (var item = 121; item < 141; item++)
        {
            var k = item - 120;
            if (!pages.ContainsKey(k))
                continue;
            var chain = new List<int> { k };
            while (pages[chain[^1]][22].StartsWith('!') && pages.ContainsKey(chain[^1] + 1))
                chain.Add(chain[^1] + 1);
            chains.Add(J.O(("item_id", item), ("files", J.Strs(chain.Select(c => $"SCROLLL{c}"))),
                ("lines", J.Strs(chain.SelectMany(c => pages[c])))));
        }
        var all = new JsonObject();
        foreach (var (k, v) in pages)
            all[$"SCROLLL{k}"] = J.Strs(v);
        return J.O(
            ("_source", $"{Source}: SCROLLL1..20 (side A, 1000 B text-screen dumps loaded at $400; TOWN2 8000-8020: item " +
                        "i shows SCROLLL(i - 120) and the next file while row 22 column 0 is '!')"),
            ("scrolls", chains),
            ("a2_pluto_speech", J.O(("file", "SCROLLL0"), ("side", "B"), ("shown", "DUNGON2 4300: after a win over monster 79"),
                ("lines", J.Strs(A2Text.TextPage(src.B("SCROLLL0")))))),
            ("a2_pages", all));
    }

    private static readonly string[] KeyFilesA = ["PHANTASIE II", "TOWN2", "OUTDOORS2", "ITEM", "TINFO", "TF2", "SHAPES", "ICE DRAGON", "INFO"];
    private static readonly string[] KeyFilesB = ["DUNGON2", "DNG1", "DNG8", "SCROLLL0"];

    /// <param name="forGameData">Adds what the GameData build needs beyond the extract (text-cell screen lines, the
    /// level files' first 2048 bytes); false = exactly the Python extract.</param>
    public static Dictionary<string, JsonObject> Build(A2Source src, bool forGameData = false)
    {
        for (var i = 0; i < 10; i++)
            if (!src.A($"MN2{i}").AsSpan().SequenceEqual(src.B($"MN2{i}")))
                throw new InvalidDataException($"MN2{i} differs between the sides");
        var monsters = Monsters(src);
        var names = Names(src, monsters);
        JsonObject Sha(Func<string, byte[]> read, string[] keys)
        {
            var o = new JsonObject();
            foreach (var k in keys)
                o[k] = Hashing.Sha1(read(k));
            return o;
        }
        return new Dictionary<string, JsonObject>
        {
            ["monsters.json"] = monsters,
            ["items.json"] = Items(src),
            ["names.json"] = names,
            ["creation-tables.json"] = CreationTables(src, names),
            ["towns.json"] = Towns(src),
            ["overworld.json"] = Overworld(src, forGameData),
            ["dungeons.json"] = Dungeons(src, forGameData),
            ["scrolls.json"] = Scrolls(src),
            ["key-files.json"] = J.O(
                ("_note", "sha1 of the read-only files the extract reads (importer detection, 10-importer.md 3.2). " +
                          "SSI.INIT2 is left out: Passport patched it in the crack (4am-notes T0E,S07)."),
                ("side_a", Sha(src.A, KeyFilesA)),
                ("side_b", Sha(src.B, KeyFilesB))),
        };
    }
}
