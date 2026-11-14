using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P3Dos;

/// <summary>
/// The Phantasie III DOS extract stage: ports of <c>extract_dos_p3_tables.py</c>, <c>_dungeons</c>, <c>_rules</c>,
/// <c>_scrolls</c>, <c>_towns</c> and <c>_world</c>. Each method returns the object the script wrote to
/// <c>reverse-engineering/dos/phantasie3/extracted/&lt;name&gt;.json</c> (same keys and values), which
/// <see cref="P3Build"/> turns into GameData. Not ported: the scripts' <c>diff-vs-p1.json</c> (an RE report against
/// I's extracts) and their Amiga file comparison (a print).
/// </summary>
public sealed class P3Extract
{
    private readonly FileSet _files;

    public P3Extract(FileSet files)
    {
        _files = files;
        Exe = new P3Exe(files.Read("MAIN.EXE"));
        Strings = Exe.StringTable();
    }

    public P3Exe Exe { get; }
    public IReadOnlyList<string> Strings { get; }

    // ---- extract_dos_p3_tables.py -------------------------------------------------------------------------------

    public const int ItemBase = 150;
    public const int SpellBase = 331;
    private const int Points = 0x113C, Req = 0x11A2, Value = 0x0F92, TownPrice = 0x10FC, TownPriceCount = 18;
    private const int ClassMap = 0x48, RaceMap = 0x4E;

    private static readonly (string Key, int From, int To)[] Groups =
    [
        ("combat_actions", 0, 19), ("status_short", 19, 24), ("class_short", 24, 30), ("rank_labels", 30, 33),
        ("places", 34, 45), ("inns", 46, 54), ("terrain", 54, 76), ("skills", 76, 85), ("races", 85, 103),
        ("classes", 103, 109), ("towns", 112, 126), ("origins", 126, 135), ("status", 135, 140),
        ("social_classes", 140, 146), ("body_states", 146, 150),
    ];

    internal static string ItemKind(int i) => i switch
    {
        0 => "none",
        <= 20 => "shield",
        <= 40 => "armor",
        <= 60 => "bow",
        <= 100 => "weapon",
        <= 110 => "healing_potion",
        <= 120 => "magic_potion",
        <= 140 => "scroll",
        _ => "treasure",
    };

    public JsonObject StringsJson() => new()
    {
        ["_source"] = "MAIN.EXE root far-pointer string table DS:0x1308 [C]; index = position",
        ["strings"] = Arr(Strings.Select((s, i) => (JsonNode)new JsonObject { ["index"] = i, ["text"] = s })),
    };

    public JsonObject Names()
    {
        var names = new JsonObject
        {
            ["_source"] = "MAIN.EXE root far-pointer string table DS:0x1308 [C]; index = position",
            ["string_count"] = Strings.Count,
        };
        foreach (var (key, a, b) in Groups)
            names[key] = Arr(Enumerable.Range(a, b - a).Select(i =>
                (JsonNode)new JsonObject { ["index"] = i - a, ["string_index"] = i, ["name"] = Strings[i] }));
        names["transfer_class_map_I_to_III"] = Ints(Enumerable.Range(0, 6).Select(i => Exe.DsByte(ClassMap + i)));
        names["transfer_race_map_I_to_III"] = Ints(Enumerable.Range(0, 16).Select(i => Exe.DsByte(RaceMap + i)));
        names["_maps"] = "DS:0x48 / DS:0x4e [C] (ovl1 0x245 / 0x27b), indexed by DOS I class (THIEF..WIZARD) / race " +
                         "(HUMAN..MINOTAUR, 15 = UNDEAD)";
        return names;
    }

    public JsonObject Items()
    {
        var items = new JsonArray();
        for (var i = 0; i < 181; i++)
        {
            var it = new JsonObject
            {
                ["id"] = i, ["name"] = Strings[ItemBase + i], ["kind"] = ItemKind(i), ["value"] = Exe.DsU16(Value + 2 * i),
            };
            if (i <= 100)
            {
                it["points"] = Exe.DsByte(Points + i);
                it["requirement"] = Exe.DsByte(Req + i);
            }
            items.Add(it);
        }
        return new JsonObject
        {
            ["_source"] = "names: string table 150+id [C]; points DS:0x113c+id, requirement DS:0x11a2+id " +
                          "(ids 0..100) [C bytes]; which attribute sum the requirement is compared with " +
                          "(STR shields/armour, STR+DEX bows, 2·STR+DEX melee, as the manual) [C] (ovl2 0x39b5, s79); " +
                          "value DS:0xf92+2·id [C bytes]; armory build (root 0x0fb8..0x102e) computes " +
                          "value · town_price_index[FAR:0xdd98] / 100 as a long for each stocked id [~]; FAR:0xdd98 " +
                          "= current town number, 1 = Pendragon (s81) [C]. The table holds 18 u16 (towns 0..17, the " +
                          "rows of INITARMS.DAT; entry 0 unused) [C] bytes, extent [~] (s82: 14 Sciattica = 123, " +
                          "15-17 = 100; the next bytes are not prices)",
            ["town_price_index"] = Arr(Enumerable.Range(0, TownPriceCount).Select(t => (JsonNode)new JsonObject
            {
                ["town"] = t, ["name"] = t != 0 ? Strings[111 + t] : null, ["index"] = Exe.DsU16(TownPrice + 2 * t),
            })),
            ["items"] = items,
        };
    }

    public JsonObject Spells() => new()
    {
        ["_source"] = "string table 331+n [C]; #53 is an empty string (Vision removed)",
        ["spells"] = Arr(Enumerable.Range(1, 57).Select(n =>
            (JsonNode)new JsonObject { ["number"] = n, ["name"] = Strings[SpellBase + n] })),
    };

    private static readonly (int Offset, string Name, string Type, string Note)[] MonFields =
    [
        (0, "name", "char[14]", "[C] NUL-padded"),
        (14, "group_max", "u8", "[~] 1 for every boss (Nikademus, Lord Wood, the three Beasts), 2 for dragons/giants"),
        (15, "attack_power_div10", "u8", "[~] = DOS I attack_power / 10 (rounded) for 33 of 35 shared monsters"),
        (16, "damage", "u8", "[~] DOS I damage, often +1"),
        (17, "armor", "u8", "[~] equals DOS I armor for all 35 shared monsters"),
        (18, "defense", "s8", "[~] equals DOS I defense (33/35)"),
        (19, "level", "u8", "[~] equals DOS I level (35/35)"),
        (20, "special_chance", "u8", "[~] equals DOS I special_chance (32/35)"),
        (21, "specials", "u8[3]", "[~] same values as DOS I specials where shared"),
        (24, "combat_rating", "u8", "[~] equals DOS I combat_rating (35/35)"),
        (25, "hp_base", "u8", "[~] equals DOS I hp_base (35/35)"),
        (26, "reaction_modifier", "s8", "[~] equals DOS I reaction_modifier (34/35)"),
        (27, "byte_27", "u8", "[?] (sprite index / picture id?)"),
        (28, "next_group_chance", "u8", "[~] DOS I next_group_chance (16/35 equal, rest close)"),
        (29, "companions", "u8[2]", "[~] monster ids (self-references like I's)"),
        (31, "treasure_items", "u8[2]", "[~] III item ids"),
        (33, "byte_33", "u8", "[?] 1 on undead and demons (Skeleton, Zombie, Ghoul, Wight, Wraith, Vampire, devils, demons), also Constrictor"),
        (34, "gold", "u16be", "[~] equals DOS I gold (34/35); BIG-ENDIAN like the Amiga file it shares"),
        (36, "xp", "u16be", "[~] close to DOS I xp (Skeleton 77 = 77, Viper 731 vs 732); big-endian"),
    ];

    public JsonObject Monsters() => MonstersFrom(_files.Read("CREATURE.DAT"),
        "CREATURE.DAT, 80 x 38 B + 32 B pad (0x60); identical to Amiga monsinfo.dat");

    /// <summary>The 80 x 38-B monster records (DOS <c>CREATURE.DAT</c> = Amiga <c>monsinfo.dat</c>, byte for byte).</summary>
    internal static JsonObject MonstersFrom(byte[] raw, string source)
    {
        var list = new JsonArray();
        for (var i = 0; i < 80; i++)
        {
            var r = raw.AsSpan(i * 38, 38);
            var nul = r[..14].IndexOf((byte)0);
            var name = P3Exe.PyRStrip(Encoding.Latin1.GetString(nul < 0 ? r[..14] : r[..nul]));
            list.Add(new JsonObject
            {
                ["index"] = i, ["name"] = name,
                ["group_max"] = (int)r[14], ["attack_power_div10"] = (int)r[15], ["damage"] = (int)r[16], ["armor"] = (int)r[17],
                ["defense"] = (int)(sbyte)r[18], ["level"] = (int)r[19], ["special_chance"] = (int)r[20],
                ["specials"] = Ints([r[21], r[22], r[23]]),
                ["combat_rating"] = (int)r[24], ["hp_base"] = (int)r[25], ["reaction_modifier"] = (int)(sbyte)r[26], ["byte_27"] = (int)r[27],
                ["next_group_chance"] = (int)r[28], ["companions"] = Ints([r[29], r[30]]),
                ["treasure_items"] = Ints([r[31], r[32]]), ["byte_33"] = (int)r[33],
                ["gold"] = (int)r[34] << 8 | r[35], ["xp"] = (int)r[36] << 8 | r[37], ["raw_hex"] = P3Exe.Hex(r),
            });
        }
        return new JsonObject
        {
            ["_source"] = source,
            ["_fields"] = Arr(MonFields.Select(f => (JsonNode)new JsonObject
                { ["offset"] = f.Offset, ["name"] = f.Name, ["type"] = f.Type, ["note"] = f.Note })),
            ["_pad_hex"] = P3Exe.Hex(raw.AsSpan(80 * 38)),
            ["monsters"] = list,
        };
    }

    // ---- extract_dos_p3_dungeons.py -----------------------------------------------------------------------------

    private const int DngCount = 11, DngSize = 0x1080, DngW = 24, DngH = 30, DngPtr = 0x593, DngTextBase = 0x800;

    /// <summary>Dungeon n loads record letter - 'A', letter from DS:0x1c84 + 2*dungeon (FORMAT.md section 9.5, s82).</summary>
    public const string Letters = "JHEDACBKIFG";

    private static (int Slots, JsonArray Texts) DungeonTexts(ReadOnlySpan<byte> rec)
    {
        var ptrs = new List<int>();
        var o = DngPtr;
        var end = DngSize;
        while (o + 1 < end)
        {
            var p = rec[o] << 8 | rec[o + 1];
            if (p != 0)
            {
                if (p < DngTextBase || p >= DngTextBase + DngSize)
                    throw new InvalidDataException($"DNGS.DAT: pointer {p:#x} at {o:#x}");
                end = Math.Min(end, p - DngTextBase);
            }
            ptrs.Add(p);
            o += 2;
        }
        var keep = (end - DngPtr) / 2;
        if (keep < ptrs.Count)
            ptrs.RemoveRange(keep, ptrs.Count - keep);
        var texts = new JsonArray();
        for (var i = 0; i < ptrs.Count; i++)
        {
            if (ptrs[i] == 0)
                continue;
            var at = ptrs[i] - DngTextBase;
            var stop = rec[at..].IndexOf((byte)0);
            if (stop < 0)
                throw new InvalidDataException($"DNGS.DAT: unterminated text at {at:#x}");
            texts.Add(new JsonObject
            {
                ["index"] = i, ["offset"] = P3Exe.Hx(at), ["text"] = Encoding.Latin1.GetString(rec.Slice(at, stop)),
            });
        }
        return (ptrs.Count, texts);
    }

    public JsonObject Dungeons()
    {
        var raw = _files.Read("DNGS.DAT");
        if (raw.Length != DngCount * DngSize)
            throw new InvalidDataException($"DNGS.DAT: {raw.Length} bytes, expected {DngCount * DngSize}");
        return DungeonsFrom(r => raw.AsSpan(r * DngSize, DngSize).ToArray(), Letters, n => Strings[33 + n],
            "DNGS.DAT 11 x 4224 B (FORMAT.md section 9.5; dungeon n = record LETTERS[n-1] - 'A'); field tags in tools/scripts/extract_dos_p3_dungeons.py");
    }

    /// <summary>
    /// The 11 dungeon records of 4224 B: <paramref name="record"/>(r) = record r (letter 'A' + r); dungeon n reads
    /// letter <paramref name="letters"/>[n - 1]; <paramref name="name"/>(n) = dungeon n's name.
    /// </summary>
    internal static JsonObject DungeonsFrom(Func<int, byte[]> record, string letters, Func<int, string> name, string source)
    {
        var list = new JsonArray();
        for (var n = 0; n < DngCount; n++)
        {
            var recordIndex = letters[n] - 'A';
            var bytes = record(recordIndex);
            if (bytes.Length != DngSize)
                throw new InvalidDataException($"dungeon record {letters[n]}: {bytes.Length} bytes, expected {DngSize}");
            ReadOnlySpan<byte> rec = bytes;
            var cells = new JsonArray();
            for (var y = 0; y < DngH; y++)
                cells.Add(Ints(rec.Slice(y * DngW, DngW).ToArray().Select(b => (int)b)));
            var squares = new JsonArray();
            for (var k = 0; k < 35; k++)
            {
                var b = rec.Slice(0x2E4 + 5 * k, 5);
                squares.Add(new JsonObject
                {
                    ["cell"] = 0xDD + k, ["message"] = b[0] & 0x7F, ["once"] = (b[0] & 0x80) != 0, ["choices"] = (int)b[1],
                    ["programs"] = Ints([b[2], b[3], b[4]]),
                });
            }
            var exits = new JsonArray();
            for (var k = 0; k < 3; k++)
                exits.Add(new JsonObject
                {
                    ["cell"] = 0xD7 + k, ["destination"] = (int)rec[0x2DB + 3 * k], ["x"] = (int)rec[0x2DC + 3 * k],
                    ["y"] = (int)rec[0x2DD + 3 * k],
                });
            var (slots, txt) = DungeonTexts(rec);
            list.Add(new JsonObject
            {
                ["dungeon"] = n + 1, ["record"] = recordIndex, ["letter"] = letters[n].ToString(), ["name"] = name(n + 1),
                ["level"] = (int)rec[0x2D0], ["width"] = DngW, ["height"] = DngH, ["cells"] = cells,
                ["forced_fights"] = Ints(rec.Slice(0x2D1, 10).ToArray().Select(b => (int)b)),
                ["exits"] = exits,
                ["event_squares"] = squares,
                ["event_program_hex"] = P3Exe.Hex(rec[0x393..0x593]),
                ["text_slots"] = slots, ["texts"] = txt,
                ["raw_hex"] = P3Exe.Hex(rec),
            });
        }
        return new JsonObject
        {
            ["_source"] = source,
            ["_cells"] = new JsonObject
            {
                ["0"] = "floor", ["1"] = "wall [~]", ["7"] = "secret door [~]",
                ["8-17"] = "random-encounter square: fight when FAR:0xdd8c > rand() % 100, monster 12L + (cell - 8) + " +
                           "rand() % 3 (ovl4 0xeb5)",
                ["0x80-0x89"] = "forced fight, monster forced_fights[cell - 0x80]",
                ["0xb9 / 0xc8"] = "doors; 0xba-0xc6 / 0xc9-0xd5 doors with monsters behind (monster cell - 186 / " +
                                  "cell - 201, plus 12L)",
                ["0xc7 / 0xd6"] = "special doors (event 0x2a) [?]",
                ["0xd7-0xd9"] = "exits (table 'exits')",
                [">= 0xdd"] = "event squares (table 'event_squares')",
            },
            ["dungeons"] = list,
        };
    }

    // ---- extract_dos_p3_rules.py --------------------------------------------------------------------------------

    public static readonly string[] Classes = ["Fighter", "Thief", "Priest", "Ranger", "Monk", "Wizard"];

    public static readonly (string Name, int Base)[] SkillTables =
    [
        ("attack", 0xC53), ("parry", 0xCAD), ("swim", 0xD07), ("listen", 0xD61), ("find_trap", 0xDBB),
        ("disarm_trap", 0xE15), ("find_item", 0xE6F), ("pick_lock", 0xEC9),
    ];

    private static readonly int[] SpellRowCount = [1, 1, 3, 2, 2, 4]; // by Classes

    public JsonObject RuleTables()
    {
        var x = Exe;
        JsonArray U16(int a, int n) => Ints(Enumerable.Range(0, n).Select(i => x.DsU16(a + 2 * i)));
        JsonObject ByClass(Func<int, JsonNode> row)
        {
            var o = new JsonObject();
            for (var k = 0; k < Classes.Length; k++)
                o[Classes[k]] = row(k);
            return o;
        }

        var hp = ByClass(k => Ints(Enumerable.Range(0, 22).Select(lv => x.DsByte(0xA78 + 22 * k + lv))));
        var mp = ByClass(k => Ints(Enumerable.Range(1, 15).Select(lv => x.DsByte(0xAFB + 15 * k + lv))));
        var skills = new JsonObject();
        foreach (var (name, b) in SkillTables)
            skills[name] = ByClass(k => Ints(Enumerable.Range(1, 15).Select(lv => x.DsByte(b + 15 * k + lv))));

        var rows = new JsonObject();
        var levels = new JsonObject();
        var a = 0x1208;
        for (var k = 0; k < Classes.Length; k++)
        {
            var rs = new JsonArray();
            var lv = new int[57];
            for (var r = 0; r < SpellRowCount[k]; r++)
            {
                var row = Enumerable.Range(0, 15).Select(i => x.DsByte(a + i)).ToArray();
                rs.Add(Ints(row));
                a += 15;
                for (var i = 0; i < row.Length; i++)
                {
                    if (row[i] == 0)
                        continue;
                    if (lv[row[i] - 1] != 0)
                        throw new InvalidDataException($"MAIN.EXE: {Classes[k]}: spell {row[i]} twice");
                    lv[row[i] - 1] = i + 1;
                }
            }
            rows[Classes[k]] = rs;
            levels[Classes[k]] = Ints(lv);
        }

        var air = new List<int>();
        for (var p = 0x1BB2; x.DsU16(p) != 0; p += 2)
            air.Add(x.DsU16(p));
        var hit = Arr(Enumerable.Range(0, 5).Select(r => (JsonNode)U16(0x12CC + 12 * r, 6)));
        var areaLevels = Ints(Enumerable.Range(0, 25).Select(i => x.DsS8(0x9FE + i)));
        var terrainOffsets = Ints(Enumerable.Range(0, 26).Select(i => x.DsByte(0xA17 + i)));
        var letters = Enumerable.Range(1, 11).Select(n => ((char)x.DsU16(0x1C84 + 2 * n)).ToString()).ToList();
        var entranceX = new JsonObject();
        foreach (var ch in letters.Distinct().OrderBy(s => s, StringComparer.Ordinal))
            entranceX[ch] = x.DsU16(0x1BB6 + 2 * ch[0]);
        var bases = new JsonObject();
        foreach (var (n, b) in SkillTables)
            bases[n] = P3Exe.Hx(b);

        return new JsonObject
        {
            ["_source"] = "MAIN.EXE DGROUP tables; addresses and tags in tools/scripts/extract_dos_p3_rules.py",
            ["classes"] = Strs(Classes),
            ["hp_dice"] = new JsonObject
            {
                ["_address"] = "DS:0xa78 + 22*class + (level - 1)",
                ["_note"] = "levels 1..22; dice of rnd(3) at training, rnd(2) + 1 at creation; twice each L1-15 value = " +
                            "the manual's CON-10 HP chart",
                ["by_class"] = hp,
            },
            ["training_xp_curve"] = new JsonObject
            {
                ["_address"] = "DS:0xb88 u32, index = level (0..21)",
                ["values"] = Arr(Enumerable.Range(0, 22).Select(i => (JsonNode)JsonValue.Create((long)x.DsU32(0xB88 + 4 * i)))),
            },
            ["training_gold_curve"] = new JsonObject
            {
                ["_address"] = "DS:0xb6c u16, index = level; 16..20 overrun into the XP curve", ["values"] = U16(0xB6C, 21),
            },
            ["magic_ceiling"] = new JsonObject { ["_address"] = "DS:0xafb + 15*class + level (1..15)", ["by_class"] = mp },
            ["skill_tables"] = new JsonObject
            {
                ["_address"] = "DS:base + 15*class + level (1..15)", ["_bases"] = bases, ["by_skill"] = skills,
            },
            ["spell_rows"] = new JsonObject
            {
                ["_address"] = "DS:0x1208, 13 rows x 15 levels; class grouping [~] (manual match)", ["by_class"] = rows,
            },
            ["spell_levels"] = new JsonObject
            {
                ["_note"] = "derived from spell_rows: level at which spell #n (index n - 1) becomes learnable, 0 = never",
                ["by_class"] = levels,
            },
            ["score_weights"] = new JsonObject { ["_address"] = "DS:0x1c50 u16 x 8", ["values"] = U16(0x1C50, 8) },
            ["hit_location"] = new JsonObject
            {
                ["_address"] = "DS:0x12cc u16, 5 rows x 6 cumulative percent",
                ["_columns"] = Strs(["head", "torso", "right_leg", "right_arm", "left_leg", "left_arm"]),
                ["_rows"] = Strs(["bow hit on a monster", "spells [~]", "melee hit; every monster hit on a character",
                    "Aim blow", "any hit on a 'tall' monster"]),
                ["rows"] = hit,
            },
            ["air_elemental_spells"] = new JsonObject { ["_address"] = "DS:0x1bb2 u16, 0-terminated", ["values"] = Ints(air) },
            ["wilderness_area_levels"] = new JsonObject
            {
                ["_address"] = "DS:0x9fe s8 x 25, index x / 10 + 5 * (y / 15) (root 0x1198)", ["values"] = areaLevels,
            },
            ["wilderness_terrain_offsets"] = new JsonObject
            {
                ["_address"] = "DS:0xa17 u8, index terrain + rnd(4) (root 0xf143 kind 3); 26 entries cover terrain 0..21",
                ["values"] = terrainOffsets,
            },
            ["dungeon_letters"] = new JsonObject
            {
                ["_address"] = "DS:0x1c84 + 2 * dungeon (dungeons 1..11)", ["values"] = Strs(letters),
            },
            ["dungeon_entrance_x"] = new JsonObject
            {
                ["_address"] = "DS:0x1bb6 + 2 * letter code (ovl4 0x836); y = 0", ["by_letter"] = entranceX,
            },
        };
    }

    // ---- extract_dos_p3_scrolls.py ------------------------------------------------------------------------------

    private const int ScrollRecords = 19, ScrollSize = 0x41A, ScrollWidth = 38;

    /// <summary>planes.md section 7 [C].</summary>
    private static int[] ItemRecords(int item)
    {
        if (item == 126)
            return [];
        if (item == 123)
            return [4];
        var first = item - 121 - (item > 126 ? 1 : 0);
        return item is 122 or 124 or 129 ? [first, first + 1, first + 2] : [first];
    }

    /// <summary>Python's <c>[text[i:i + w].rstrip() for i in range(0, len(text), w)]</c>.</summary>
    public static List<string> Slices(string text, int width) =>
        Enumerable.Range(0, (text.Length + width - 1) / width)
            .Select(i => P3Exe.PyRStrip(text.Substring(i * width, Math.Min(width, text.Length - i * width)))).ToList();

    public JsonObject Scrolls()
    {
        var raw = _files.Read("SCROLLS.DAT");
        if (raw.Length != ScrollRecords * ScrollSize)
            throw new InvalidDataException($"SCROLLS.DAT: {raw.Length} bytes, expected {ScrollRecords * ScrollSize}");
        var recs = new JsonArray();
        for (var k = 0; k < ScrollRecords; k++)
        {
            var rec = raw.AsSpan(k * ScrollSize, ScrollSize);
            var nul = rec.IndexOf((byte)0);
            var text = Encoding.Latin1.GetString(nul < 0 ? rec : rec[..nul]);
            recs.Add(new JsonObject
            {
                ["record"] = k, ["length"] = text.Length, ["text"] = text, ["lines"] = Strs(Slices(text, ScrollWidth)),
            });
        }
        var items = new JsonArray();
        for (var i = 121; i < 141; i++)
        {
            var r = ItemRecords(i);
            if (r.Any(v => v is < 0 or >= ScrollRecords))
                throw new InvalidDataException($"scroll item {i}: record out of range");
            items.Add(new JsonObject { ["item"] = i, ["name"] = Strings[ItemBase + i], ["records"] = Ints(r) });
        }
        return new JsonObject
        {
            ["_source"] = "SCROLLS.DAT 19 x 1050 B NUL-terminated [C] (FORMAT.md section 9.4); item -> records = read_scroll " +
                          "root 0xeb [C] (planes.md section 7); 38-character lines [~]",
            ["_notes"] = Strs([
                "Item 126 (Scroll IV) shows the map picture (SHAPES.CGA; caption: string useitem.scroll_picture_caption), not text.",
                "Items 122 / 124 / 129 show three consecutive records (pages). Item 130 shows record 8, whose " +
                "text is cut at byte 869 (planes.md section 7); the duplicate III / VII items are partial copies.",
            ]),
            ["records"] = recs,
            ["items"] = items,
        };
    }

    // ---- extract_dos_p3_towns.py --------------------------------------------------------------------------------

    private const int TownRows = 18, TownRow = 181;

    public JsonObject Towns()
    {
        var raw = _files.Read("INITARMS.DAT");
        if (raw.Length != 3328 || raw.AsSpan(TownRows * TownRow).ContainsAnyExcept((byte)0x3A))
            throw new InvalidDataException("INITARMS layout changed");
        var towns = new JsonArray();
        for (var n = 0; n < TownRows; n++)
        {
            var row = raw.AsSpan(n * TownRow, TownRow);
            var stock = new int[181];
            for (var i = 1; i < 181; i++)
                stock[i] = row[i];
            var kinds = row[0];
            if (kinds != stock.Count(v => v != 0))
                throw new InvalidDataException($"INITARMS.DAT town {n}: kinds byte {kinds} != stocked ids");
            towns.Add(new JsonObject
            {
                ["town"] = n, ["name"] = n != 0 ? Strings[111 + n] : null,
                ["price_index"] = Exe.DsU16(TownPrice + 2 * n),
                ["kinds"] = (int)kinds, ["stock"] = Ints(stock),
                ["stocked"] = Arr(Enumerable.Range(0, 181).Where(i => stock[i] != 0).Select(i => (JsonNode)new JsonObject
                    { ["id"] = i, ["name"] = Strings[ItemBase + i], ["count"] = stock[i] })),
            });
        }
        return new JsonObject
        {
            ["_source"] = "INITARMS.DAT rows 0..17 x 181 B [C] (FORMAT.md section 9.3); price index DS:0x10fc + 2 * town [C] " +
                          "bytes (extent [~]); names = master string 111 + town [C]",
            ["_layout"] = "stock[id] for item id 0..180 (stock[0] is always 0; the file's byte 0 is 'kinds', the number of " +
                          "ids with a non-zero count, checked here). Row 0 is unused; 15..17 (the plane towns) stock nothing. " +
                          "Counts of 200 / 99 / 77 on potions and scrolls = plenty. Restocking between visits [?].",
            ["towns"] = towns,
        };
    }

    // ---- extract_dos_p3_world.py --------------------------------------------------------------------------------

    private static readonly string[] PlaceTypes = ["", "town", "dungeon", "inn", "map_edge"];

    private static readonly (int Id, string Key, string Src, int Off, int W, int H, (int X, int Y)? Arrive)[] Planes =
    [
        (0, "scandor", "WORLD.DAT", 0x000, 50, 75, null),
        (1, "light", "PLNS.DAT", 0x000, 9, 9, (4, 1)),
        (2, "dark", "PLNS.DAT", 0x051, 9, 9, (4, 1)),
        (4, "netherworld", "PLNS.DAT", 0x0A2, 31, 16, (7, 3)),
    ];

    /// <summary>planes.md section 4 (read by hand, not by the scan): the Light / Dark places.</summary>
    private static readonly (string Plane, int X, int Y, string Type, int Number)[] HandPlaces =
    [
        ("light", 4, 1, "town", 15), ("light", 6, 7, "dungeon", 10), ("dark", 4, 1, "town", 16), ("dark", 2, 7, "dungeon", 11),
    ];

    private static readonly (int First, int Last, int Terrain)[] TerrainRanges =
    [
        (0, 25, 10), (26, 38, 19), (39, 46, 20), (47, 54, 21), (61, 67, 14), (68, 74, 11), (90, 114, 3),
        (115, 154, 2), (155, 187, 9), (188, 205, 0), (206, 209, 6), (214, 221, 6), (222, 222, 13), (223, 224, 6),
        (225, 241, 1), (242, 255, 15),
    ];

    private static readonly (int First, int Last, int Terrain)[] NetherOverrides =
        [(61, 77, 17), (78, 82, 16), (83, 87, 18), (88, 241, 8)];

    /// <summary>(x, y, type, number) from the immediate operands of place_at_party (ovl3 0xa22..0x1300), in code order.</summary>
    private static List<JsonObject> ScanPlaces(byte[] ovl3)
    {
        int? x = null, y = null, typ = null;
        var list = new List<JsonObject>();
        var i = 0xA22;
        while (i < 0x1300)
        {
            bool At(byte a, byte b, byte c, byte d) => ovl3[i] == a && ovl3[i + 1] == b && ovl3[i + 2] == c && ovl3[i + 3] == d;
            if (At(0x83, 0x3E, 0x82, 0x48)) { x = ovl3[i + 4]; i += 5; }
            else if (At(0x83, 0x3E, 0x84, 0x48)) { y = ovl3[i + 4]; i += 5; }
            else if (At(0xC7, 0x06, 0xBC, 0x0C)) { typ = ovl3[i + 4]; i += 6; }
            else if (At(0xC7, 0x06, 0x84, 0xDD))
            {
                list.Add(new JsonObject
                {
                    ["x"] = x, ["y"] = y, ["type"] = PlaceTypes[typ ?? throw new InvalidDataException("place without type")],
                    ["number"] = (int)ovl3[i + 4], ["code"] = $"ovl3 {P3Exe.Hx(i)}",
                });
                i += 6;
            }
            else
            {
                i++;
            }
        }
        return list;
    }

    public JsonObject World()
    {
        var ovl3 = Exe.Overlay(3);
        var maps = new JsonArray();
        var byKey = new Dictionary<string, byte[][]>();
        foreach (var (pid, key, src, off, w, h, arrive) in Planes)
        {
            var file = _files.Read(src);
            if (file.Length < off + w * h)
                throw new InvalidDataException($"{src}: too short for {key}");
            var rows = Enumerable.Range(0, h).Select(yy => file[(off + yy * w)..(off + (yy + 1) * w)]).ToArray();
            byKey[key] = rows;
            maps.Add(new JsonObject
            {
                ["plane"] = pid, ["key"] = key, ["source"] = $"{src} +{P3Exe.Hx(off)}", ["width"] = w, ["height"] = h,
                ["arrival"] = arrive is { } a ? new JsonObject { ["x"] = a.X, ["y"] = a.Y } : null,
                ["tiles"] = Arr(rows.Select(r => (JsonNode)Ints(r.Select(b => (int)b)))),
            });
        }

        var scanned = ScanPlaces(ovl3);
        var edges = scanned.Where(p => (string)p["type"]! == "map_edge").ToList();
        var places = new List<JsonObject>();
        foreach (var p in scanned.Where(p => (string)p["type"]! != "map_edge"))
        {
            var type = (string)p["type"]!;
            var number = (int)p["number"]!;
            var plane = (type, number) is ("town", 17) or ("dungeon", 9) ? "netherworld" : "scandor";
            var o = new JsonObject { ["plane"] = plane };
            foreach (var (k, v) in p)
                o[k] = v?.DeepClone();
            places.Add(o);
        }
        foreach (var (plane, hx, hy, type, number) in HandPlaces)
            places.Add(new JsonObject
            {
                ["plane"] = plane, ["x"] = hx, ["y"] = hy, ["type"] = type, ["number"] = number,
                ["code"] = "planes.md §4 (by hand)",
            });

        // Every square against its tile (FORMAT section 9.1-9.2): Scandor towns 26-38, dungeons 39-46, inns 47-54;
        // plane places on the place tiles 55-60.
        foreach (var p in places)
        {
            var plane = (string)p["plane"]!;
            var type = (string)p["type"]!;
            var number = (int)p["number"]!;
            var tile = (int)byKey[plane][(int)p["y"]!][(int)p["x"]!];
            p["tile"] = tile;
            var ok = plane == "scandor"
                ? type switch { "town" => tile is >= 26 and <= 38, "dungeon" => tile is >= 39 and <= 46, _ => tile is >= 47 and <= 54 }
                : tile is >= 55 and <= 60;
            if (!ok)
                throw new InvalidDataException($"place {p.ToJsonString()} is not on a place tile");
            p["name"] = type switch
            {
                "town" => Strings[111 + number], "dungeon" => Strings[33 + number], _ => Strings[45 + number],
            };
        }
        int Count(string t) => places.Count(p => (string)p["type"]! == t);
        if (Count("town") != 17 || Count("dungeon") != 11 || Count("inn") != 8)
            throw new InvalidDataException("ovl3 place scan: unexpected place counts");

        return new JsonObject
        {
            ["_source"] = "WORLD.DAT (Scandor) and PLNS.DAT (planes) tile bytes [C]; places scanned from ovl3 place_at_party " +
                          "0xa22 [C] (Light / Dark squares by hand, planes.md section 4); terrain ranges of ovl3 0x375 " +
                          "(planes.md section 3) [C]. tiles[y][x], x west -> east, y north -> south.",
            ["planes"] = maps,
            ["start"] = new JsonObject { ["plane"] = 0, ["x"] = 24, ["y"] = 37 },
            ["places"] = Arr(places),
            ["map_edge"] = new JsonObject
            {
                ["_note"] = "x = 0, y = 0, the last row or column is blocked: type 4, number 12 (Scandor) / 15 (planes) [C]",
                ["scanned"] = Arr(edges),
            },
            ["terrain"] = new JsonObject
            {
                ["_note"] = "terrain index t: name = master string 54 + t; the first matching range wins; any other tile = " +
                            "4 (no name). Netherworld overrides first. Tiles >= 242 are blocked (River Styx; 'Ocean' on " +
                            "Scandor). No random encounters on t 10 (Water), 8 (Lava), 15 (Styx); Water swims, Lava hurts " +
                            "(damage size [?]).",
                ["names"] = Strs(Enumerable.Range(0, 22).Select(i => Strings[54 + i])),
                ["ranges"] = Arr(TerrainRanges.Select(r => (JsonNode)new JsonObject
                    { ["first"] = r.First, ["last"] = r.Last, ["terrain"] = r.Terrain })),
                ["other"] = 4,
                ["netherworld_overrides"] = Arr(NetherOverrides.Select(r => (JsonNode)new JsonObject
                    { ["first"] = r.First, ["last"] = r.Last, ["terrain"] = r.Terrain })),
                ["blocked_from"] = 242,
                ["no_encounter"] = Ints([10, 8, 15]),
            },
        };
    }

    // ---- JSON helpers ------------------------------------------------------------------------------------------

    internal static JsonArray Arr(IEnumerable<JsonNode?> items) => new(items.ToArray());

    internal static JsonArray Ints(IEnumerable<int> items) => new(items.Select(i => (JsonNode)i).ToArray());

    internal static JsonArray Strs(IEnumerable<string> items) => new(items.Select(s => (JsonNode)s).ToArray());
}
