using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// The table extracts of Phantasie II (Atari ST v1.0): ports of <c>extract_st_phantasie2.py</c>,
/// <c>extract_st_p2_maps.py</c>, <c>extract_st_p2_dungeons.py</c>, <c>extract_st_p2_scrolls.py</c> and
/// <c>extract_st_p2_towns.py</c>. Each returns the part of the script's <c>extracted/</c> JSON that
/// <see cref="P2GameDataBuilder"/> reads (debug fields, renders and the comparisons with Phantasie I's DOS / Amiga files
/// are left out: <c>build_p2_gamedata.py</c> drops them).
/// </summary>
internal static class P2StExtract
{
    // ---- PHANT.PRG: names, items, monsters (extract_st_phantasie2.py) ------------------------------------------

    private const int ItemValues = 0x180;
    private const int ItemPtrs = 0x2EC;
    private const int NamePtrs = 0x5C4;
    public const int MonstersOff = 0x1C76;
    public const int MonsterStride = 58;
    public const int MonsterCount = 80;

    private static readonly (int Off, string Fmt, string Name)[] MonsterFields =
    [
        (0x14, "h", "armor"), (0x16, "h", "damage"), (0x18, "h", "level"), (0x1A, "I", "xp"),
        (0x1E, "h", "special_chance"), (0x20, "3B", "specials"), (0x23, "B", "pad_23"), (0x24, "h", "hp_base"),
        (0x26, "h", "combat_rating"), (0x28, "2B", "companions"), (0x2A, "h", "reaction_modifier"),
        (0x2C, "h", "next_group_chance"), (0x2E, "h", "defense"), (0x30, "I", "gold"), (0x34, "B", "group_size_factor"),
        (0x35, "B", "byte_34"), (0x36, "2B", "treasure_items"), (0x38, "h", "attack_power"),
    ];

    /// <summary>extracted/names.json: classes, races, status, spells (slot, number, name), items.</summary>
    public static JsonObject Names(StPrg p)
    {
        var d = p.DataStart;
        var names = Enumerable.Range(0, 85).Select(i => p.CStr(p.U32(d + NamePtrs + 4 * i))).ToList();
        var items = new JsonArray();
        for (var i = 0; i < 181; i++)
            items.Add(J.O(("index", i), ("name", p.CStr(p.U32(d + ItemPtrs + 4 * i))),
                ("value_factor", p.U16(d + ItemValues + 2 * i))));
        var spells = J.A(names.Skip(25).Select((n, i) => (JsonNode?)J.O(("slot", i), ("number", i + 1), ("name", n))));
        return J.O(("classes", J.Strs(names.Take(6))), ("races", J.Strs(names.Skip(6).Take(16))),
            ("status", J.Strs(names.Skip(22).Take(3))), ("spells", spells), ("items", items));
    }

    /// <summary>extracted/monsters.json: 80 records (index, name, the fields, raw_hex).</summary>
    public static JsonArray Monsters(StPrg p)
    {
        var img = p.Image;
        var list = new JsonArray();
        for (var i = 0; i < MonsterCount; i++)
        {
            var raw = img.AsSpan(p.DataStart + MonstersOff + i * MonsterStride, MonsterStride);
            var m = J.O(("index", i), ("name", Py.Latin1(Py.UntilNul(raw[..20]))));
            foreach (var (off, fmt, name) in MonsterFields)
                m[name] = fmt switch
                {
                    "h" => BinaryPrimitives.ReadInt16BigEndian(raw[off..]),
                    "I" => (long)BinaryPrimitives.ReadUInt32BigEndian(raw[off..]),
                    "B" => (int)raw[off],
                    "2B" => J.Ints([raw[off], raw[off + 1]]),
                    "3B" => J.Ints([raw[off], raw[off + 1], raw[off + 2]]),
                    _ => throw new InvalidOperationException(fmt),
                };
            m["raw_hex"] = Py.Hex(raw);
            list.Add(m);
        }
        return list;
    }

    // ---- MAPS.INT + OUTn.DAT (extract_st_p2_maps.py) -----------------------------------------------------------

    private const int W = 26, H = 20, FeatOff = 520, FeatN = 15, TextOff = 595, TextPages = 10, Page = 40;

    // Step rules from PHANT.PRG 0x60c6..0x651e (FORMAT.md section 9.3) [C].
    internal static readonly (string, string)[] ClassRules =
    [
        ("tile 100", "blocked (no move, no time)"),
        ("tile % 10 == 6", "location text (feature arg -> pages)"),
        ("tile % 10 == 7", "fixed encounter, monster = feature arg + 1 (1-based)"),
        ("tile % 10 == 8", "plain move, no encounter roll"),
        ("class 5", "dungeon entrance (state 13; feature arg = dungeon, dest = start cell)"),
        ("class 7, class 11", "if not yet this trip: 9 % per step (rand0_99 <= 8) fight, monster (1-based) = " +
                              "rand0_99 / 34 + 12 * (tile % 10) + 1; never the 6 % roll. [Mist / Dark Voids]"),
        ("class 8, tile % 10 != 0", "state 8 = 0xfac2(0) ' L A V A :': every non-dead member takes " +
                                    "1 + rand0_99 * (tile % 10) / 10 damage; no encounter. [Molten Lava]"),
        ("class 8, tile 80", "plain move, no encounter roll"),
        ("tile 40", "town (feature arg - 1 = town index)"),
        ("tile 41", "wilderness inn (state 9)"),
        ("class 10", "water: state 10 = 0xfac2(1) swimming"),
        ("otherwise", "6 % random encounter (rand0_99 < 6, 0xfe38)"),
        ("step cost", "class <= 3: (class + 1) * 75; class >= 4: 60 (0x612c)"),
    ];

    /// <summary>extracted/maps/overworld.json (the fields the GameData build keeps).</summary>
    public static JsonObject Overworld(P2StSource src)
    {
        var regions = new JsonObject();
        foreach (var r in P2StSource.Regions)
        {
            var o = src.Region(r);
            var tiles = new JsonArray();
            for (var y = 0; y < H; y++)
                tiles.Add(J.Ints(Enumerable.Range(0, W).Select(x => (int)o[(25 - x) * 20 + y])));
            var feats = new JsonArray();
            for (var i = 0; i < FeatN; i++)
            {
                var b = o.AsSpan(FeatOff + 5 * i, 5);
                feats.Add(J.O(("index", i), ("x", 25 - b[1]), ("y", (int)b[0]), ("arg", (int)b[2]), ("dest_x", (int)b[3]),
                    ("dest_y", (int)b[4])));
            }
            var pages = new JsonArray();
            for (var i = 0; i < TextPages; i++)
            {
                var text = Py.Strip(Py.Latin1(Py.UntilNul(o.AsSpan(TextOff + i * Page, Page))));
                if (text.Length == 0)
                    continue;
                var shown = new StringBuilder();
                foreach (var c in text)
                    shown.Append(c is >= ' ' and <= '~' ? c : '·');
                pages.Add(J.O(("index", i), ("text", shown.ToString())));
            }
            regions[$"OUT{r}.DAT"] = J.O(("region_id", r), ("size", o.Length), ("grid_width", W), ("grid_height", H),
                ("tiles", tiles), ("features", feats), ("text_pages", pages));
        }
        var rules = new JsonObject();
        foreach (var (k, v) in ClassRules)
            rules[k] = v;
        return J.O(
            ("_source", "Phantasie II ST: disk1/MAPS.INT (initial grids) + disk2/OUTn.DAT bytes 520..2999 (static)"),
            ("_note", "26x20 grid stored column-major as (25 - x) * 20 + y; tiles here are [y][x] rows like Phantasie " +
                      "I's overworld.json (DOS y * 26 + x). Feature x/y are converted from the stored (y, 25 - x). " +
                      "Fog encodes as tile + 120. FORMAT.md section 9."),
            ("grid_width", W), ("grid_height", H), ("tile_rules", rules), ("regions", regions));
    }

    // ---- DNG1-8 + MESS1-8 (extract_st_p2_dungeons.py + extract_dungeon_events.py) ------------------------------

    private const int TextBase = 0xE6;
    private const int MaxId = 114;
    private const int Rec = 41;
    private const int DngGrid = 33 * 38;

    /// <summary>OV2 0x2418 (extract_dungeon_events.load_slot): (prefix, 40 chars) or null for an empty slot.</summary>
    private static (int Prefix, byte[] Buf)? LoadSlot(byte[] plain, int id)
    {
        var i = 2 * (id - 1);
        if (i < 0 || i + 4 > TextBase + 2)
            return null;
        int w = BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(i));
        int nxt = BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(i + 2));
        if (w == nxt)
            return null;
        var rec = new byte[41];
        var p = TextBase + w;
        if (p < plain.Length)
            plain.AsSpan(p, Math.Min(41, plain.Length - p)).CopyTo(rec);
        return (rec[0], rec[1..41]);
    }

    /// <summary>
    /// extract_dungeon_events.message(): the text lines and menu options as OV2 0x2a95 shows them. The s153
    /// <c>option_cells</c> field (added to the script for I's Amiga requesters) is not written: II's committed
    /// dungeons.json predates it and the II Client does not read it.
    /// </summary>
    private static JsonObject? Message(byte[] plain, int id)
    {
        if (LoadSlot(plain, id) is not { } first)
            return null;
        var (length, buf) = first;
        var opts = 1;
        var cur = id;
        var text = new List<byte>();
        var di = 0;
        while (di < length)
        {
            text.Add(buf[di % 40]);
            di++;
            if (di % 40 == 0)
            {
                cur++;
                if (LoadSlot(plain, cur) is { } nxt)
                {
                    if (di == 40)
                        opts = nxt.Prefix;
                    buf = nxt.Buf;
                }
            }
        }
        if (length < 40)
            opts = 3;
        var options = new JsonArray();
        if (opts is >= 2 and <= 20)
        {
            var width = opts <= 7 ? 11 : 3;
            for (var k = 0; k < opts - 1; k++)
            {
                var label = new byte[width];
                for (var c = 0; c < width; c++)
                {
                    label[c] = buf[di % 40];
                    di++;
                    if (di % 40 == 0)
                    {
                        cur++;
                        if (LoadSlot(plain, cur) is { } nxt)
                            buf = nxt.Buf;
                    }
                }
                options.Add(Py.Strip(Py.Latin1(label)));
            }
        }
        var raw = Py.Latin1(text.ToArray());
        var lines = new JsonArray();
        for (var i = 0; i < raw.Length; i += 40)
            lines.Add(Py.RStrip(raw.Substring(i, Math.Min(40, raw.Length - i))));
        return J.O(("lines", lines), ("options", options));
    }

    /// <summary>extract_dungeon_events.continuation_of().</summary>
    private static int? ContinuationOf(byte[] plain, int id)
    {
        if (LoadSlot(plain, id) is not { } rec || rec.Prefix > 20)
            return null;
        for (var s = id - 1; s >= 1; s--)
        {
            if (LoadSlot(plain, s) is not { } prev)
                return null;
            if (prev.Prefix > 20)
                return prev.Prefix > 40 * (id - s) ? s : null;
        }
        return null;
    }

    /// <summary>extracted/dungeons/dungeons.json levels (id, size, raw_hex, embedded_text, messages).</summary>
    public static JsonObject Dungeons(P2StSource src)
    {
        var levels = new JsonObject();
        foreach (var n in P2StSource.Dungeons)
        {
            var raw = src.Read($"DNG{n}");
            var plain = src.Read($"MESS{n}");
            var msgs = new JsonObject();
            for (var mid = 1; mid <= MaxId; mid++)
            {
                if (Message(plain, mid) is not { } m)
                    continue;
                if (ContinuationOf(plain, mid) is { } c)
                    m["continuation_of"] = c;
                msgs[mid.ToString()] = m;
            }
            levels[$"DNG{n}"] = J.O(("id", $"p2:{n}"), ("size", raw.Length), ("raw_hex", Py.Hex(raw)),
                ("embedded_text", EmbeddedText(raw.AsSpan(DngGrid))), ("messages", msgs));
        }
        return J.O(("_source", "Phantasie II ST disk2/DNG1..DNG8 + MESS1..MESS8 (static data; identical in the TOSEC set)"),
            ("levels", levels));
    }

    /// <summary>ASCII runs of 6+ printable bytes after the grid that hold a space (re <c>[ -~]{6,}</c>).</summary>
    private static JsonArray EmbeddedText(ReadOnlySpan<byte> tail)
    {
        var list = new JsonArray();
        var i = 0;
        while (i < tail.Length)
        {
            if (tail[i] is < 0x20 or > 0x7E)
            {
                i++;
                continue;
            }
            var j = i;
            while (j < tail.Length && tail[j] is >= 0x20 and <= 0x7E)
                j++;
            var run = tail[i..j];
            if (run.Length >= 6 && run.Contains((byte)' '))
                list.Add(J.O(("offset", $"0x{i:x4}"), ("text", Py.Latin1(run))));
            i = j;
        }
        return list;
    }

    /// <summary>extracted/dungeons/messages.json (the fields the GameData build keeps).</summary>
    public static JsonObject Messages(P2StSource src)
    {
        var areas = new JsonArray();
        foreach (var n in P2StSource.Dungeons)
        {
            var plain = src.Read($"MESS{n}");
            var slots = new JsonArray();
            for (var sid = 0; sid <= MaxId; sid++)
            {
                int w = BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(2 * sid));
                int nxt = BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(2 * sid + 2));
                var e = J.O(("id", sid + 1), ("offset", w), ("empty", w == nxt));
                if (w != nxt)
                {
                    var p = TextBase + w;
                    var r = p < plain.Length ? plain.AsSpan(p, Math.Min(Rec, plain.Length - p)) : [];
                    e["file_offset"] = p;
                    e["prefix"] = r.Length > 0 ? (int)r[0] : null;
                    e["text"] = Py.RStrip(Py.Latin1(r.Length > 0 ? r[1..] : r));
                }
                slots.Add(e);
            }
            var recs = new JsonArray();
            for (var pos = TextBase; pos + Rec <= plain.Length; pos += Rec)
                recs.Add(J.O(("offset", pos), ("record_index", (pos - TextBase) / Rec), ("prefix", (int)plain[pos]),
                    ("text", Py.RStrip(Py.Latin1(plain.AsSpan(pos + 1, Rec - 1))))));
            areas.Add(J.O(("id", n), ("source", $"MESS{n}"), ("size", plain.Length), ("slots", slots), ("records", recs)));
        }
        return J.O(
            ("_source", "Phantasie II ST disk2/MESS1..MESS8"),
            ("_cipher", "none (plain text on the ST)"),
            ("_layout", "u16 LE slot table (115 words, ids 1..115) then 41-B records (prefix + 40 chars) from 0xe6; " +
                        "slot id -> record at 0xe6 + word; empty when word == next word (PHANT.PRG 0x1159e) [C]"),
            ("areas", areas));
    }

    // ---- SCROLLS.DTX (extract_st_p2_scrolls.py) ----------------------------------------------------------------

    /// <summary>extracted/scrolls/scrolls.json (the lettered scrolls).</summary>
    public static JsonObject Scrolls(P2StSource src, JsonObject names)
    {
        const int recSize = 840, line = 40, linesPer = 21;
        var data = src.Read("SCROLLS.DTX");
        var nameOf = new Dictionary<int, string>();
        foreach (var it in names["items"]!.AsArray())
            nameOf[(int)it!["index"]!] = (string)it["name"]!;
        var full = data.Length / recSize;
        var records = new List<List<string>>();
        for (var n = 0; n < full + (data.Length % recSize != 0 ? 1 : 0); n++)
        {
            var chunk = data.AsSpan(n * recSize, Math.Min(recSize, data.Length - n * recSize));
            var lines = new List<string>();
            for (var k = 0; k < linesPer && k * line < chunk.Length; k++)
                lines.Add(Py.RStrip(Py.Latin1(Py.UntilNul(chunk.Slice(k * line, Math.Min(line, chunk.Length - k * line))))));
            records.Add(lines);
        }
        var scrolls = new JsonArray();
        var id = 0;
        for (var item = 121; item <= 140; item++)
        {
            var name = nameOf.GetValueOrDefault(item);
            if (string.IsNullOrEmpty(name) || Py.Strip(name) == "SCROLL")
                continue;
            var i = item > 137 ? item - 1 : item;
            var first = i - 121;
            var extra = item switch { 121 => 2, 132 => 1, 134 => 2, _ => 0 };
            var recs = Enumerable.Range(first, extra + 1).ToList();
            var textLines = recs.SelectMany(r => records[r]).ToList();
            scrolls.Add(J.O(("id", ++id), ("item_id", item), ("name", name), ("records", J.Ints(recs)),
                ("lines", J.Strs(textLines)), ("text", string.Join("\n", textLines.Where(l => Py.Strip(l).Length > 0)))));
        }
        return J.O(
            ("_source", "Phantasie II ST disk2/SCROLLS.DTX (16384 B, plain text)"),
            ("_layout", "840-B records = 21 lines x 40 chars (PHANT.PRG 0x13608 / 0x292a) [C]"),
            ("_item_map", "PHANT.PRG 0xe1e8: item i in 121..140 -> first record (i > 137 ? i - 1 : i) - 121; " +
                          "SCROLL A (121) shows 3 records, the 132 and 134 scrolls 2 and 3 [C]"),
            ("scrolls", scrolls));
    }

    // ---- TWNS.INT (extract_st_p2_towns.py) ---------------------------------------------------------------------

    private const int CreationEnd = 2446, TownOff = 2446, TownStride = 292, TownCount = 12;
    private const int GuildOff = 5950, GuildStride = 318, GuildCount = 25;
    private static readonly string[] Classes = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"];
    private static readonly string[] Races =
        ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC",
         "GOBLIN", "LIZ.MAN", "MINOTAUR"];
    private static readonly string[] Stats = ["STR", "INT", "DEX", "CON", "CHA"];
    private static readonly string[] SkillOrder =
        ["attack", "parry", "find_item", "spot_trap", "disarm_trap", "listen", "_42_toss", "pick_lock", "_unused_46", "swim"];
    private static readonly (int Off, string Key)[] ElemWords =
    [
        (2, "intelligence"), (14, "mp_ceiling"), (16, "magic_max"), (18, "magic_cur"), (20, "hp_max"), (22, "hp_cur"),
        (24, "status"), (30, "attack"), (32, "parry"), (88, "level"), (92, "guild_number"), (216, "birth_race"),
        (220, "class"), (240, "weapon_damage_b"), (242, "shield_soak"), (244, "armor_soak"), (256, "race"),
    ];

    private static byte[] TwnsInt(P2StSource src)
    {
        var t = src.Read("TWNS.INT");
        if (t.Length != 13900)
            throw new InvalidDataException($"TWNS.INT: {t.Length} bytes, expected 13900");
        return t;
    }

    /// <summary>extracted/towns/creation-tables.json (raw ST values; the keys the GameData build keeps).</summary>
    public static JsonObject CreationTables(P2StSource src)
    {
        var d = TwnsInt(src);
        JsonObject ByClass(int off, int n) => Obj(Classes.Select((c, i) => (c, (JsonNode?)J.Ints(d.AsSpan(off + i * n, n).ToArray().Select(b => (int)b)))));
        var sk = Obj(Classes.Select((c, i) => (c, (JsonNode?)J.A(Enumerable.Range(0, 15).Select(lv => (JsonNode?)J.Ints(
            Enumerable.Range(0, 10).Select(k => (int)BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(0x21C + i * 0x12C + lv * 0x14 + 2 * k)))))))));
        var cm = Obj(Classes.Select((c, i) => (c, (JsonNode?)Obj(Stats.Select((s, k) => (s, (JsonNode?)(int)d[0x924 + i * 5 + k]))))));
        var rm = Obj(Races.Select((r, i) => (r, (JsonNode?)J.O(
            ("modifiers", Obj(Stats.Select((s, k) => (s, (JsonNode?)(int)d[0x942 + i * 5 + k])))),
            ("ceilings", Obj(Stats.Select((s, k) => (s, (JsonNode?)(d[0x942 + i * 5 + k] + 8)))))))));
        return J.O(
            ("_schema", "phantasie.creation-tables/v1 (shape of GameData/phantasie1/creation-tables.json, raw values)"),
            ("hp_factors", J.O(("offset", "0x0000"), ("layout", "class * 15 + level"), ("by_class", ByClass(0, 15)))),
            ("mp_ceiling", J.O(("offset", "0x01c2"), ("layout", "class * 15 + level"), ("by_class", ByClass(0x1C2, 15)))),
            ("skill_coefficients", J.O(("offset", "0x021c"), ("layout", "class * 0x12c + level * 0x14 + skill * 2 (u16 BE)"),
                ("skill_order", J.Strs(SkillOrder)), ("by_class_level", sk))),
            ("class_attribute_modifiers", J.O(("offset", "0x0924"), ("by_class", cm))),
            ("race_attribute_modifiers", J.O(("offset", "0x0942"),
                ("note", "15 races x 5 bytes + 1 pad byte (76-byte block; DOS 75). ceiling = modifier + 8 as in Phantasie I [~] for II"),
                ("pad_byte", (int)d[0x942 + 75]), ("by_race", rm))),
            ("spell_levels", J.O(("offset", "0x005a"), ("layout", "class * 60 + spell slot (u8); 0 = never"),
                ("by_class", ByClass(0x5A, 60)))));
    }

    private static JsonObject Obj(IEnumerable<(string, JsonNode?)> props) => J.O(props.ToArray());

    /// <summary>extracted/towns/towns.json (_layout, _notes and the town fields the GameData build keeps).</summary>
    public static JsonObject Towns(P2StSource src)
    {
        var t = TwnsInt(src);
        // region_cells(): {(x, y): (tile, feature arg)} per region, scanned x-major as the script's dict.
        var townCells = new Dictionary<int, JsonArray>();
        var exitTiles = new Dictionary<(int, int, int), int>();
        foreach (var r in P2StSource.Regions)
        {
            var o = src.Region(r);
            var feats = new Dictionary<(int, int), int>();
            for (var i = 0; i < 15; i++)
                feats[(25 - o[520 + 5 * i + 1], o[520 + 5 * i])] = o[520 + 5 * i + 2];
            for (var x = 0; x < 26; x++)
                for (var y = 0; y < 20; y++)
                {
                    var tile = P2StSource.Unfog(o[(25 - x) * 20 + y]);
                    exitTiles[(r, x, y)] = tile;
                    if (tile == 40 && feats.TryGetValue((x, y), out var arg) && arg != 0)
                    {
                        if (!townCells.TryGetValue(arg - 1, out var list))
                            townCells[arg - 1] = list = [];
                        list.Add(J.O(("region", r), ("x", x), ("y", y)));
                    }
                }
        }
        var towns = new JsonArray();
        for (var i = 0; i < TownCount; i++)
        {
            var rec = t.AsSpan(TownOff + i * TownStride, TownStride).ToArray();
            int U16(int off) => BinaryPrimitives.ReadUInt16BigEndian(rec.AsSpan(off));
            towns.Add(J.O(
                ("id", i + 1), ("index", i),
                ("name", Py.Strip(Py.Latin1(Py.UntilNul(rec.AsSpan(280, 10))))),
                ("price_index", U16(272)),
                ("stock", J.Ints(rec[..181].ToArray().Select(b => (int)b))),
                ("restock_items", J.Ints(Enumerable.Range(0, 45).Select(k => U16(182 + 2 * k)))),
                ("exit_region", U16(274)),
                ("exit_x", 25 - U16(278)),
                ("exit_y", U16(276)),
                ("map_town_cells", townCells.TryGetValue(i, out var cells) ? cells : new JsonArray())));
        }
        return J.O(
            ("_layout", J.O(("record_stride", TownStride), ("town_count", TownCount), ("stock_slots", 181),
                ("restock_items_offset", "0xb6 (u16 BE x 45)"), ("price_index_offset", "0x110"),
                ("exit_region_offset", "0x112"), ("exit_row_offset", "0x114"), ("exit_col_offset", "0x116"),
                ("name_offset", 280))),
            ("_notes", J.Strs([
                "Field meanings carried over from DOS I (save-file-format.md section Town state) with the +1 shift [~].",
                "exit_x/exit_y: the record holds (row y, column 25 - x), the ST map convention [C by data]: 8 of the 10 " +
                "map towns' exit cells are their own tile-40 cell.",
                "map_town_cells: cells with tile 40 whose feature arg - 1 == this town index. Town numbers are 1-based " +
                "(PHANT.PRG 0x26cc: record n - 1, n = 99 -> record 11) [C]; GUILD @254 holds the number (1 = PIPPACOTT).",
                "STARVILLE and OLYMPIA are byte-identical to Phantasie I's records and no map cell leads to them.",
            ])),
            ("towns", towns));
    }

    /// <summary>extracted/towns/elementals.json: the Guild template's three elementals (slots 22..24).</summary>
    public static JsonArray Elementals(P2StSource src)
    {
        var t = TwnsInt(src);
        var guild = t.AsSpan(GuildOff, GuildCount * GuildStride);
        if (guild[..(22 * GuildStride)].ContainsAnyExcept((byte)0))
            throw new InvalidDataException("TWNS.INT: the 22 Guild character slots are not empty");
        var list = new JsonArray();
        for (var i = 22; i < 25; i++)
        {
            var rec = guild.Slice(i * GuildStride, GuildStride);
            var e = J.O(("name", Py.Latin1(Py.UntilNul(rec.Slice(270, 12)))));
            foreach (var (off, key) in ElemWords)
                e[key] = (int)BinaryPrimitives.ReadUInt16BigEndian(rec[off..]);
            var known = new List<int>();
            for (var k = 0; k < 60; k++)
                if (BinaryPrimitives.ReadUInt16BigEndian(rec[(0x60 + 2 * k)..]) == 1)
                    known.Add(k);
            e["known_spells"] = J.Ints(known);
            list.Add(e);
        }
        return list;
    }
}
