using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P3Dos;
using OpenPhantasie.Importer.Profiles;
using static OpenPhantasie.Importer.Pipelines.P3Dos.P3Extract;

namespace OpenPhantasie.Importer.Pipelines.P3Amiga;

/// <summary>
/// Phantasie III's game tables from the Amiga v1.0 disk alone (s208), in the shapes <see cref="P3Extract"/> gives for
/// DOS III, so the same <see cref="P3Build"/> turns them into GameData. Layouts, addresses and every Amiga ≠ DOS cell:
/// <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> section "Game tables for a standalone import".
/// <para><b>Not offered to players.</b> No pipeline calls this: <c>p3-amiga-1.0</c> still imports only the art. Where
/// the Amiga's numbers or content differ from DOS (HP above level 15, training gold above 15, the terrain table's
/// overrun, the place tiles' terrain, the Scroll III pages, the text) the user has not decided what an Amiga-only
/// import uses, and some outputs do not load as they are (<c>hp_dice</c> has 21 levels on the Amiga, the loader wants
/// 22). Values the Amiga does not have are <c>null</c>, never DOS's.</para>
/// <para>Addresses are offsets in <see cref="AmigaHunks.LoadFlat"/>'s image (CODE at 0, DATA at 0x21890), each read
/// from the code that indexes it (FORMAT.md lists the code sites).</para>
/// </summary>
public sealed class P3AmigaTables
{
    public const string Label = "Phantasie III, Amiga v1.0 SSI 1987 (reverse-engineering/amiga/phantasie3/FORMAT.md, " +
                                "section 'Game tables for a standalone import')";

    private readonly FileSet _files;
    private readonly byte[] _img;

    public P3AmigaTables(FileSet files)
    {
        _files = files;
        (_img, _) = AmigaHunks.LoadFlat(files.Read("PhantasieIII"));
    }

    // ---- the program image ---------------------------------------------------------------------------------------

    private int B(int a) => _img[a];
    private int S8(int a) => (sbyte)_img[a];
    private int U16(int a) => BinaryPrimitives.ReadUInt16BigEndian(_img.AsSpan(a));
    private long U32(int a) => BinaryPrimitives.ReadUInt32BigEndian(_img.AsSpan(a));

    /// <summary>Entry <paramref name="i"/> of a table of string pointers (4 B each, relocated) at <paramref name="table"/>.</summary>
    private string Str(int table, int i)
    {
        var p = (int)U32(table + 4 * i);
        var end = Array.IndexOf(_img, (byte)0, p);
        if (p <= 0 || end < 0)
            throw new InvalidDataException($"PhantasieIII: bad string pointer at {P3Exe.Hx(table + 4 * i)}");
        return Encoding.Latin1.GetString(_img, p, end - p);
    }

    // String-pointer tables (DATA), each the base the code indexes with 4 * n (FORMAT.md, "Names").
    private const int CombatActions = 0x22A38, RankLabels = 0x22A2C, ItemNames = 0x22A78, SpellNames = 0x22D4C;
    private const int Origins = 0x22E34, Races = 0x22E4C, Classes = 0x22E94, ClassShort = 0x22EAC, Social = 0x22EC4;
    private const int BodyStates = 0x22EDC, Status = 0x22EEC, StatusShort = 0x22F00, Towns = 0x22F14;
    private const int Places = 0x22F8C, Inns = 0x22FBC, Terrain = 0x22FE0, Skills = 0x2302C;
    private const int TerrainNameCount = 19;

    // Byte / word tables (DATA).
    private const int Points = 0x227A0, Req = 0x22805, Value = 0x2286A, TownPrice = 0x229D4;
    private const int ClassMap = 0x23F12, RaceMap = 0x23F18;
    private const int HpDice = 0x221DC, HpLevels = 21, MagicCeiling = 0x2225A, GoldCurve = 0x222DC, XpCurve = 0x222FA;
    private const int SpellRows = 0x2261E, HitLocation = 0x239B4, AirSpells = 0x23F48, AirSpellCount = 31;
    private const int AreaLevels = 0x220F6, TerrainOffsets = 0x2210F, DungeonLetters = 0x23B3C, EntranceX = 0x23A38;
    private const int ScoreWeights = 0x23A50;

    private static readonly (string Name, int Base)[] SkillTables =
    [
        ("attack", 0x2234E), ("parry", 0x223A8), ("swim", 0x22401), ("listen", 0x2245B), ("find_trap", 0x224B5),
        ("disarm_trap", 0x2250F), ("find_item", 0x22569), ("pick_lock", 0x225C3),
    ];

    private static readonly int[] SpellRowCount = [1, 1, 3, 2, 2, 4];

    // ---- names --------------------------------------------------------------------------------------------------

    private JsonArray Group(int table, int first, int count) => Arr(Enumerable.Range(0, count).Select(i =>
        (JsonNode)new JsonObject { ["index"] = i, ["pointer"] = P3Exe.Hx(table + 4 * (first + i)), ["name"] = Str(table, first + i) }));

    public string TownName(int n) => Str(Towns, n);
    public string ItemName(int i) => Str(ItemNames, i);

    public JsonObject Names()
    {
        // origins: DOS's group starts with towns 15-17 (P3Build skips 3); the Amiga's table 0x22e34 holds only the six
        // origin names, indexed by the record value as DOS's string 129 + n. The three town names come from the town table.
        var origins = new JsonArray();
        for (var n = 15; n <= 17; n++)
            origins.Add(new JsonObject { ["index"] = n - 15, ["pointer"] = P3Exe.Hx(Towns + 4 * n), ["name"] = TownName(n) });
        foreach (var o in Group(Origins, 0, 6))
        {
            o!["index"] = (int)o["index"]! + 3;
            origins.Add(o.DeepClone());
        }
        return new JsonObject
        {
            ["_source"] = "PhantasieIII string-pointer tables in DATA, one per group (FORMAT.md 'Game tables', Names) [C]",
            ["combat_actions"] = Group(CombatActions, 0, 16),
            ["status_short"] = Group(StatusShort, 0, 5),
            ["class_short"] = Group(ClassShort, 0, 6),
            ["rank_labels"] = Group(RankLabels, 0, 3),
            ["places"] = Group(Places, 1, 11),
            ["inns"] = Group(Inns, 1, 8),
            ["terrain"] = Group(Terrain, 0, TerrainNameCount),
            ["skills"] = Group(Skills, 0, 9),
            ["races"] = Group(Races, 0, 18),
            ["classes"] = Group(Classes, 0, 6),
            ["towns"] = Group(Towns, 1, 14),
            ["origins"] = origins,
            ["status"] = Group(Status, 0, 5),
            ["social_classes"] = Group(Social, 0, 6),
            ["body_states"] = Group(BodyStates, 0, 4),
            ["transfer_class_map_I_to_III"] = Ints(Enumerable.Range(0, 6).Select(i => B(ClassMap + i))),
            ["transfer_race_map_I_to_III"] = Ints(Enumerable.Range(0, 16).Select(i => B(RaceMap + i))),
            ["_maps"] = "0x23f12 / 0x23f18 [C] (0x206b6 / 0x206f4), indexed by DOS I class / race as DOS's DS:0x48 / 0x4e",
        };
    }

    // ---- items, spells, monsters --------------------------------------------------------------------------------

    public JsonObject Items()
    {
        var items = new JsonArray();
        for (var i = 0; i < 181; i++)
        {
            var it = new JsonObject { ["id"] = i, ["name"] = ItemName(i), ["kind"] = ItemKind(i), ["value"] = U16(Value + 2 * i) };
            if (i <= 100)
            {
                it["points"] = B(Points + i);
                it["requirement"] = B(Req + i);
            }
            items.Add(it);
        }
        return new JsonObject
        {
            ["_source"] = "names 0x22a78 + 4 id; points 0x227a0 + id, requirement 0x22805 + id (0x8c74 / 0x8c9a); value " +
                          "0x2286a + 2 id (u16), times town_price_index 0x229d4 + 2 town / 100 (0x8bc0) [C]",
            ["town_price_index"] = Arr(Enumerable.Range(0, 18).Select(t => (JsonNode)new JsonObject
            {
                ["town"] = t, ["name"] = t != 0 ? TownName(t) : null, ["index"] = U16(TownPrice + 2 * t),
            })),
            ["items"] = items,
        };
    }

    public JsonObject Spells() => new()
    {
        ["_source"] = "0x22d4c + 4 n [C]; #53 holds a placeholder name (DOS: an empty string)",
        ["spells"] = Arr(Enumerable.Range(1, 57).Select(n =>
            (JsonNode)new JsonObject { ["number"] = n, ["name"] = Str(SpellNames, n) })),
    };

    public JsonObject Monsters() =>
        MonstersFrom(_files.Read("monsinfo.dat"), "monsinfo.dat, 80 x 38 B + 32 B pad; byte for byte DOS CREATURE.DAT");

    // ---- dungeons -----------------------------------------------------------------------------------------------

    /// <summary>Dungeon n reads <c>D/dng&lt;letter&gt;.dat</c>, letter = byte 0x23b3c + 2n + 1 (0x1bbe6) [C].</summary>
    public string Letters => new(Enumerable.Range(1, 11).Select(n => (char)U16(DungeonLetters + 2 * n)).ToArray());

    public JsonObject Dungeons()
    {
        byte[] Record(int r)
        {
            var file = $"D/dng{(char)('a' + r)}.dat";
            var raw = _files.Read(file);
            if (raw.Length != 4352 || raw.AsSpan(4224).ContainsAnyExcept((byte)0))
                throw new InvalidDataException($"{file}: expected 4224 B of record and 128 B of zero padding");
            return raw[..4224];
        }
        return DungeonsFrom(Record, Letters, n => Str(Places, n),
            "D/dng<a..k>.dat (4352 B = the 4224-B record + 128 B of zeros), letter of dungeon n from 0x23b3c (0x1bbe6); " +
            "names 0x22f8c + 4 n (0x80cc) [C]");
    }

    // ---- scrolls ------------------------------------------------------------------------------------------------

    private const int ScrollSize = 0x41A, ScrollWidth = 38;

    /// <summary>Text files in record order: <c>D/s1</c>..<c>s20</c> without <c>s6</c> (Scroll IV's picture, a packed screen).</summary>
    private static readonly int[] ScrollFiles = [1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];

    /// <summary>
    /// 0x1f6dc: n = item - 120; n 2..4 become 2 with three pages; n 9 has three pages; n 6 is the picture <c>D/s6</c>;
    /// page k reads <c>D/s&lt;n + k&gt;</c> (1050 B into 0x241f0) [C]. Returned as record numbers (file order).
    /// </summary>
    private static int[] ItemRecords(int item)
    {
        var n = item - 120;
        if (n == 6)
            return [];
        var pages = 1;
        if (n is >= 2 and <= 4)
        {
            n = 2;
            pages = 3;
        }
        if (n == 9)
            pages = 3;
        return Enumerable.Range(n, pages).Select(f => Array.IndexOf(ScrollFiles, f)).ToArray();
    }

    public JsonObject Scrolls()
    {
        var recs = new JsonArray();
        for (var k = 0; k < ScrollFiles.Length; k++)
        {
            var raw = _files.Read($"D/s{ScrollFiles[k]}");
            if (raw.Length < ScrollSize)
                throw new InvalidDataException($"D/s{ScrollFiles[k]}: {raw.Length} bytes, expected at least {ScrollSize}");
            var rec = raw.AsSpan(0, ScrollSize);
            var nul = rec.IndexOf((byte)0);
            var text = Encoding.Latin1.GetString(nul < 0 ? rec : rec[..nul]);
            recs.Add(new JsonObject
            {
                ["record"] = k, ["file"] = $"D/s{ScrollFiles[k]}", ["length"] = text.Length, ["text"] = text,
                ["lines"] = Strs(Slices(text, ScrollWidth)),
            });
        }
        var items = new JsonArray();
        for (var i = 121; i < 141; i++)
            items.Add(new JsonObject { ["item"] = i, ["name"] = ItemName(i), ["records"] = Ints(ItemRecords(i)) });
        return new JsonObject
        {
            ["_source"] = "D/s1..s20 without s6: one 1050-B record each (plain text, padded to 1152 B), record k = the k-th " +
                          "file; item -> pages = 0x1f6dc [C]; 38-character lines [~]",
            ["_notes"] = Strs([
                "Item 126 (Scroll IV) shows the picture D/s6 (a packed screen), not text.",
                "Items 122, 123 and 124 all show s2, s3, s4 (records 1-3); item 129 shows s9-s11 (records 7-9).",
            ]),
            ["records"] = recs,
            ["items"] = items,
        };
    }

    // ---- towns --------------------------------------------------------------------------------------------------

    public JsonObject TownsTable()
    {
        const int rows = 18, row = 181;
        var raw = _files.Read("inititem.dat");
        if (raw.Length != 3328 || raw.AsSpan(rows * row).ContainsAnyExcept((byte)0x3A))
            throw new InvalidDataException("inititem.dat layout changed");
        var towns = new JsonArray();
        for (var n = 0; n < rows; n++)
        {
            var r = raw.AsSpan(n * row, row);
            var stock = new int[181];
            for (var i = 1; i < 181; i++)
                stock[i] = r[i];
            if (r[0] != stock.Count(v => v != 0))
                throw new InvalidDataException($"inititem.dat town {n}: kinds byte {r[0]} != stocked ids");
            towns.Add(new JsonObject
            {
                ["town"] = n, ["name"] = n != 0 ? TownName(n) : null, ["price_index"] = U16(TownPrice + 2 * n),
                ["kinds"] = (int)r[0], ["stock"] = Ints(stock),
                ["stocked"] = Arr(Enumerable.Range(0, 181).Where(i => stock[i] != 0).Select(i => (JsonNode)new JsonObject
                    { ["id"] = i, ["name"] = ItemName(i), ["count"] = stock[i] })),
            });
        }
        return new JsonObject
        {
            ["_source"] = "inititem.dat rows 0..17 x 181 B (byte for byte DOS INITARMS.DAT); price index 0x229d4 + 2 town; " +
                          "names 0x22f14 + 4 town [C]",
            ["_layout"] = "stock[id] for item id 0..180 (byte 0 = 'kinds', the number of stocked ids). Row 0 is unused.",
            ["towns"] = towns,
        };
    }

    // ---- rule tables --------------------------------------------------------------------------------------------

    public JsonObject RuleTables()
    {
        var cls = P3Extract.Classes;
        JsonObject ByClass(Func<int, JsonNode> row)
        {
            var o = new JsonObject();
            for (var k = 0; k < cls.Length; k++)
                o[cls[k]] = row(k);
            return o;
        }
        JsonArray U16s(int a, int n) => Ints(Enumerable.Range(0, n).Select(i => U16(a + 2 * i)));

        // hp: 21 per class, class * 21 + level - 1, the level clamped at 21 (0xe39c / 0xe3d4) [C].
        var hp = ByClass(k => Ints(Enumerable.Range(0, HpLevels).Select(lv => B(HpDice + HpLevels * k + lv))));
        // magic ceiling: class * 15 + level - 1, level clamped at 15 (0x11d60) [C].
        var mp = ByClass(k => Ints(Enumerable.Range(0, 15).Select(lv => B(MagicCeiling + 15 * k + lv))));
        // skills: base + class * 15 + level - 1 (0xe6d0 ... 0xe866) [C].
        var skills = new JsonObject();
        var bases = new JsonObject();
        foreach (var (name, b) in SkillTables)
        {
            skills[name] = ByClass(k => Ints(Enumerable.Range(0, 15).Select(lv => B(b + 15 * k + lv))));
            bases[name] = P3Exe.Hx(b);
        }

        var rows = new JsonObject();
        var levels = new JsonObject();
        var a = SpellRows;
        for (var k = 0; k < cls.Length; k++)
        {
            var rs = new JsonArray();
            var lv = new int[57];
            for (var r = 0; r < SpellRowCount[k]; r++)
            {
                var row = Enumerable.Range(0, 15).Select(i => B(a + i)).ToArray();
                rs.Add(Ints(row));
                a += 15;
                for (var i = 0; i < row.Length; i++)
                {
                    if (row[i] == 0)
                        continue;
                    if (lv[row[i] - 1] != 0)
                        throw new InvalidDataException($"PhantasieIII: {cls[k]}: spell {row[i]} twice");
                    lv[row[i] - 1] = i + 1;
                }
            }
            rows[cls[k]] = rs;
            levels[cls[k]] = Ints(lv);
        }

        // XP curve: (level - 1) * 4, levels 1..21 (0xdfde); gold: (level - 1) * 2 (0xde92), levels 1..15 its own,
        // 16..20 run into the XP curve's words. Index 0 (DOS's level 0) does not exist on the Amiga: null.
        var xp = new JsonArray { null };
        for (var lv = 1; lv <= 21; lv++)
            xp.Add(JsonValue.Create(U32(XpCurve + 4 * (lv - 1))));
        var gold = new JsonArray { null };
        for (var lv = 1; lv <= 20; lv++)
            gold.Add(U16(GoldCurve + 2 * (lv - 1)));

        // Terrain offsets: 0x1a9d6 reads 0x2210f + terrain + rnd(4) - 1 (rnd 1..4, 0xaa7e), i.e. DOS's index t + rnd(4)
        // into a table one byte lower: entry 0 (never read) is the area-level table's last byte.
        var terrainOffsets = Ints(Enumerable.Range(0, 26).Select(i => B(TerrainOffsets - 1 + i)));
        var letters = Letters;
        var entrance = new JsonObject();
        foreach (var ch in letters.Distinct().OrderBy(c => c))
            entrance[ch.ToString()] = U16(EntranceX + 2 * (ch - 'A'));

        return new JsonObject
        {
            ["_source"] = "PhantasieIII DATA tables; addresses and code sites in FORMAT.md 'Game tables for a standalone import'",
            ["classes"] = Strs(cls),
            ["hp_dice"] = new JsonObject
            {
                ["_address"] = "0x221dc + 21*class + (level - 1), level clamped at 21 (0xe3d4)",
                ["_note"] = "levels 1..21 (no level 22 on the Amiga); L16-21 keep rising where DOS stays at L15's value",
                ["by_class"] = hp,
            },
            ["training_xp_curve"] = new JsonObject
            {
                ["_address"] = "0x222fa + 4*(level - 1), u32; index 0 (no Amiga entry) is null", ["values"] = xp,
            },
            ["training_gold_curve"] = new JsonObject
            {
                ["_address"] = "0x222dc + 2*(level - 1), u16; 16..20 overrun into the XP curve (big-endian words); index 0 null",
                ["values"] = gold,
            },
            ["magic_ceiling"] = new JsonObject { ["_address"] = "0x2225a + 15*class + (level - 1)", ["by_class"] = mp },
            ["skill_tables"] = new JsonObject
            {
                ["_address"] = "base + 15*class + (level - 1)", ["_bases"] = bases, ["by_skill"] = skills,
            },
            ["spell_rows"] = new JsonObject { ["_address"] = "0x2261e, 13 rows x 15 levels (0x10b8e)", ["by_class"] = rows },
            ["spell_levels"] = new JsonObject
            {
                ["_note"] = "derived from spell_rows: level at which spell #n (index n - 1) becomes learnable, 0 = never",
                ["by_class"] = levels,
            },
            ["score_weights"] = new JsonObject { ["_address"] = "0x23a50 u16 x 8 (0x1e9e4)", ["values"] = U16s(ScoreWeights, 8) },
            ["hit_location"] = new JsonObject
            {
                ["_address"] = "0x239b4 u16, 5 rows x 6 cumulative percent (0x1421c)",
                ["_columns"] = Strs(["head", "torso", "right_leg", "right_arm", "left_leg", "left_arm"]),
                ["_rows"] = Strs(["bow hit on a monster", "spells [~]", "melee hit; every monster hit on a character",
                    "Aim blow", "any hit on a 'tall' monster"]),
                ["rows"] = Arr(Enumerable.Range(0, 5).Select(r => (JsonNode)U16s(HitLocation + 12 * r, 6))),
            },
            ["air_elemental_spells"] = new JsonObject
            {
                ["_address"] = "0x23f48 u16 x 31 (the copy loop 0x8f4c runs 31 times)", ["values"] = U16s(AirSpells, AirSpellCount),
            },
            ["wilderness_area_levels"] = new JsonObject
            {
                ["_address"] = "0x220f6 s8 x 25, index x / 10 + 5 * (y / 15) (0x1aa40)",
                ["values"] = Ints(Enumerable.Range(0, 25).Select(i => S8(AreaLevels + i))),
            },
            ["wilderness_terrain_offsets"] = new JsonObject
            {
                ["_address"] = "0x2210e + terrain + rnd(4) (0x1a9d6 reads 0x2210f + t + rnd(4) - 1); entries 16+ are the next table",
                ["values"] = terrainOffsets,
            },
            ["dungeon_letters"] = new JsonObject
            {
                ["_address"] = "0x23b3c + 2 * dungeon (dungeons 1..11, 0x1bbe6)", ["values"] = Strs(letters.Select(c => c.ToString())),
            },
            ["dungeon_entrance_x"] = new JsonObject
            {
                ["_address"] = "0x23a38 + 2 * (letter - 'A') (0x1bd44); y = 0", ["by_letter"] = entrance,
            },
        };
    }

    // ---- world --------------------------------------------------------------------------------------------------

    private static readonly string[] PlaceTypes = ["", "town", "dungeon", "inn", "map_edge"];

    /// <summary>
    /// The place test 0x9c6a (DOS ovl3 0xa22): each fixed square is <c>cmpi.w #x,party_x; bne; cmpi.w #y,party_y;
    /// bne; move.w #type,0x240be; move.w #number,0x240c0</c>, in code order. The Netherworld's two come first
    /// (0x9cda, under its plane test); the Light / Dark squares compute the number and are read by hand (0x9d5a).
    /// </summary>
    private List<JsonObject> ScanPlaces()
    {
        bool Op(int i, ushort op, int addr) => U16(i) == op && (int)U32(i + 4) == addr;
        var list = new List<JsonObject>();
        for (var i = 0x9C6A; i < 0xA346;)
        {
            if (Op(i, 0x0C79, 0x240C4) && _img[i + 8] == 0x66 && Op(i + 10, 0x0C79, 0x240C6) && _img[i + 18] == 0x66
                && U16(i + 20) == 0x33FC && (int)U32(i + 24) == 0x240BE && U16(i + 28) == 0x33FC && (int)U32(i + 32) == 0x240C0)
            {
                var type = PlaceTypes[U16(i + 22)];
                var number = U16(i + 30);
                list.Add(new JsonObject
                {
                    ["plane"] = i < 0x9D36 ? "netherworld" : "scandor",
                    ["x"] = U16(i + 2), ["y"] = U16(i + 12), ["type"] = type, ["number"] = number,
                    ["code"] = $"PhantasieIII {P3Exe.Hx(i)}",
                });
                i += 36;
            }
            else
            {
                i += 2;
            }
        }
        return list;
    }

    /// <summary>0x9d5a: on Light / Dark (plane p = 1 / 2) (4, 1) is town 14 + p; (6, 7) on Light and (2, 7) on Dark are
    /// dungeon 9 + p [C].</summary>
    private static readonly (string Plane, int X, int Y, string Type, int Number)[] HandPlaces =
    [
        ("light", 4, 1, "town", 15), ("light", 6, 7, "dungeon", 10), ("dark", 4, 1, "town", 16), ("dark", 2, 7, "dungeon", 11),
    ];

    /// <summary>
    /// 0xa4a6, terrain of a tile: a run of range tests, each later one overriding the earlier (code order). Tiles none
    /// of them covers (26-60, 75-89; DOS: towns 19, dungeons 20, inns 21, else 4) leave the local unset (it is never
    /// initialised): null here.
    /// </summary>
    private static readonly (int First, int Last, int Terrain)[] TerrainRules =
    [
        (1, 1, 10), (0, 0, 10), (2, 25, 10), (68, 74, 11), (61, 67, 14), (90, 114, 3), (115, 154, 2), (155, 187, 9),
        (188, 205, 0), (206, 209, 6), (214, 224, 6), (225, 241, 1), (222, 222, 13), (242, 255, 15),
    ];

    /// <summary>0xa5fa: on the Netherworld (plane bit 2) these override, in this order [C].</summary>
    private static readonly (int First, int Last, int Terrain)[] NetherRules = [(61, 77, 17), (78, 82, 16), (83, 87, 18), (88, 241, 8)];

    private static JsonArray Ranges(IEnumerable<(int First, int Last, int Terrain)> rules)
    {
        var t = new int?[256];
        foreach (var (f, l, v) in rules)
            for (var i = f; i <= l; i++)
                t[i] = v;
        var list = new JsonArray();
        for (var i = 0; i < 256;)
        {
            if (t[i] is not { } v)
            {
                i++;
                continue;
            }
            var j = i;
            while (j + 1 < 256 && t[j + 1] == v)
                j++;
            list.Add(new JsonObject { ["first"] = i, ["last"] = j, ["terrain"] = v });
            i = j + 1;
        }
        return list;
    }

    public JsonObject World()
    {
        // Graphics/phantasy.set: 3 bytes (0xfa, rows 75, columns 50), 32-B palette, the 50 x 75 map (0x27d8) [C].
        var set = _files.Read("Graphics/phantasy.set");
        if (set.Length < 35 + 3750 || set[1] != 75 || set[2] != 50)
            throw new InvalidDataException("Graphics/phantasy.set: unexpected header");
        var sources = new (int Id, string Key, string Src, byte[] Data, int Off, int W, int H, (int X, int Y)? Arrive)[]
        {
            (0, "scandor", "Graphics/phantasy.set", set, 35, 50, 75, null),
            // 0x1eb32: D/PLN<plane> ('0' + plane): Light / Dark 81 B, party (4, 1); the Netherworld 496 B, party (7, 3).
            (1, "light", "D/pln1", _files.Read("D/pln1"), 0, 9, 9, (4, 1)),
            (2, "dark", "D/pln2", _files.Read("D/pln2"), 0, 9, 9, (4, 1)),
            (4, "netherworld", "D/pln4", _files.Read("D/pln4"), 0, 31, 16, (7, 3)),
        };
        var maps = new JsonArray();
        var byKey = new Dictionary<string, byte[][]>();
        foreach (var (pid, key, src, file, off, w, h, arrive) in sources)
        {
            if (file.Length < off + w * h)
                throw new InvalidDataException($"{src}: too short for {key}");
            var rows = Enumerable.Range(0, h).Select(yy => file[(off + yy * w)..(off + (yy + 1) * w)]).ToArray();
            byKey[key] = rows;
            maps.Add(new JsonObject
            {
                ["plane"] = pid, ["key"] = key, ["source"] = $"{src} +{P3Exe.Hx(off)}", ["width"] = w, ["height"] = h,
                ["arrival"] = arrive is { } ar ? new JsonObject { ["x"] = ar.X, ["y"] = ar.Y } : null,
                ["tiles"] = Arr(rows.Select(r => (JsonNode)Ints(r.Select(b => (int)b)))),
            });
        }

        var places = ScanPlaces();
        foreach (var (plane, hx, hy, type, number) in HandPlaces)
            places.Add(new JsonObject
            {
                ["plane"] = plane, ["x"] = hx, ["y"] = hy, ["type"] = type, ["number"] = number, ["code"] = "PhantasieIII 0x9d5a (by hand)",
            });
        foreach (var p in places)
        {
            var plane = (string)p["plane"]!;
            var type = (string)p["type"]!;
            var number = (int)p["number"]!;
            var tile = (int)byKey[plane][(int)p["y"]!][(int)p["x"]!];
            // DOS's check (every place on a place tile) does not hold: Pendragon (24, 37) is tile 25 on the Amiga's map
            // (DOS 29), one of the 54 cells that differ (FORMAT.md section 4). The tile is recorded as it is.
            p["tile"] = tile;
            p["name"] = type switch { "town" => TownName(number), "dungeon" => Str(Places, number), _ => Str(Inns, number) };
        }
        int Count(string t) => places.Count(p => (string)p["type"]! == t);
        if (Count("town") != 17 || Count("dungeon") != 11 || Count("inn") != 8)
            throw new InvalidDataException("PhantasieIII place scan: unexpected place counts");

        return new JsonObject
        {
            ["_source"] = "Graphics/phantasy.set (Scandor, +0x23) and D/pln1, pln2, pln4 (planes) tile bytes [C]; places from the " +
                          "place test 0x9c6a [C] (Light / Dark by hand, 0x9d5a); terrain of a tile 0xa4a6 [C]. tiles[y][x].",
            ["planes"] = maps,
            // 0x7602 / 0x2010e: a new game and 'Start this turn in Pendragon' put the party at (24, 37) [C].
            ["start"] = new JsonObject { ["plane"] = 0, ["x"] = 24, ["y"] = 37 },
            ["places"] = Arr(places),
            ["terrain"] = new JsonObject
            {
                ["_note"] = "terrain index t: name = 0x22fe0 + 4 t (19 names, 0xa728); ranges = 0xa4a6's tests resolved to " +
                            "one value per tile. Tiles 26-60 and 75-89 get no value (an uninitialised local): other = null " +
                            "[?]. Tiles >= 242 are blocked (0xa5d8). No random encounters on 10, 8, 15 (0x7c24).",
                ["names"] = Strs(Enumerable.Range(0, TerrainNameCount).Select(i => Str(Terrain, i))),
                ["ranges"] = Ranges(TerrainRules),
                ["other"] = null,
                ["netherworld_overrides"] = Ranges(NetherRules),
                ["blocked_from"] = 242,
                ["no_encounter"] = Ints([10, 8, 15]),
            },
        };
    }

    // ---- GameData -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The ten class (b) / (c) files <see cref="P3DosPipeline"/> writes, built by <see cref="P3Build"/> from the
    /// Amiga's tables (provenance relabelled). Not called by any pipeline (see the class summary).
    /// </summary>
    public static List<(string File, JsonObject Node)> Build(ImportContext context)
    {
        var x = new P3AmigaTables(context.Files);
        var names = x.Names();
        var monsters = x.Monsters();
        var rt = x.RuleTables();
        var p1Spells = context.ReadShipped("phantasie1", "spells.json")?["spells"]?.AsArray()
                       ?? throw new InvalidOperationException("shipped phantasie1/spells.json is missing");
        var p1Creation = context.ReadShipped("phantasie1", "creation-tables.json")?.AsObject()
                         ?? throw new InvalidOperationException("shipped phantasie1/creation-tables.json is missing");
        JsonObject Shipped(string file) => context.ReadShipped(P3DosPipeline.Game, file)?.AsObject()
                                           ?? throw new InvalidOperationException($"shipped phantasie3/{file} is missing");
        var items = P3Build.Items(x.Items());
        var spells = P3Build.Spells(x.Spells(), p1Spells);
        var built = new List<(string File, JsonObject Node)>
        {
            ("monsters.json", P3Build.Monsters(monsters)),
            ("items.json", items),
            ("spells.json", spells),
            ("names.json", P3Build.Names(names, monsters["monsters"]!.AsArray(), items["items"]!.AsArray(),
                spells["spells"]!.AsArray())),
            ("overworld.json", P3Build.Overworld(x.World())),
            ("dungeons.json", P3Build.Dungeons(x.Dungeons())),
            ("scrolls.json", P3Build.Scrolls(x.Scrolls())),
            ("towns.json", P3Build.Towns(x.TownsTable(), Shipped("towns.json"))),
            ("elementals.json", P3Build.Elementals(rt)),
            ("rule-tables.json", P3Build.RuleTables(rt, names, p1Creation, Shipped("rule-tables.json"))),
        };
        foreach (var (_, node) in built)
            Relabel(node);
        return built;
    }

    /// <summary>P3Build prefixes provenance with DOS III's label; these files come from the Amiga.</summary>
    private static void Relabel(JsonNode? n)
    {
        switch (n)
        {
            case JsonObject o:
                foreach (var key in o.Select(kv => kv.Key).ToList())
                {
                    if (key.StartsWith('_') && o[key] is JsonValue v && v.TryGetValue<string>(out var s)
                        && s.StartsWith(P3Build.Source, StringComparison.Ordinal))
                        o[key] = Label + s[P3Build.Source.Length..];
                    else
                        Relabel(o[key]);
                }
                break;
            case JsonArray a:
                foreach (var e in a)
                    Relabel(e);
                break;
        }
    }
}
