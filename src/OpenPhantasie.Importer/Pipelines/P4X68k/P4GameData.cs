using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json.Nodes;
using static OpenPhantasie.Importer.Pipelines.P4X68k.P4Util;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// IV's GameData tables (port of <c>tools/scripts/build_p4_gamedata.py</c>). Numbers and Japanese text come from the
/// player's files; English comes from the shipped id-keyed <c>strings/en.json</c> (<see cref="P4Text.En(string)"/>);
/// our own notes and manual-decided values are this code's constants, except <c>rule-tables.json</c>, whose many
/// rules and notes of our own come from the shipped file (the overlay, 10-importer.md 2.7) with every value read
/// from the player's files set over it (<see cref="RuleTables"/>).
/// </summary>
internal sealed class P4GameData
{
    private const string Game = "p4";
    private const string Source = "Phantasie IV, X68000 Star Craft 1991 Ver.1.02 (reverse-engineering/x68000/phantasie4/, FORMAT.md)";

    // The class and skill names are P4.X's own English (_class, _skill; the Python's CLASSES / SKILLS equal them).
    private string[] Classes => _s.Names["class"];
    private string[] Skills => _s.Names["skill"];
    // Body areas and wound levels: our English (P4.X's _body / _hurt hold Japanese wound messages).
    private static readonly string[] Body = ["Head", "Torso", "Right Arm", "Left Arm", "Right Leg", "Left Leg"];
    private static readonly string[] Wounds = ["None", "Light", "Serious", "Lost"];
    private static readonly int[] MoutRecord =
        [0, 0, 1, 2, 3, 4, 5, 5, 6, 7, 8, 5, 5, 9, 10, 11, 12, 13, 12, 12, 14, 15, 16, 17, 12]; // by map 1-24 (index 0 unused)

    private readonly P4Source _s;
    private readonly P4Text _t;

    internal P4GameData(P4Source source, P4Text text)
    {
        _s = source;
        _t = text;
    }

    private static string Cid(int n) => $"{Game}:{n}";

    /// <summary>The (b)/(c) table files by GameData-relative path.</summary>
    internal Dictionary<string, JsonNode> Build(JsonNode? shippedRuleTables)
    {
        var monsters = Monsters();
        var items = Items();
        var spells = Spells();
        var built = new Dictionary<string, JsonNode>
        {
            ["monsters.json"] = monsters,
            ["items.json"] = items,
            ["spells.json"] = spells,
        };
        built["names.json"] = NamesFile((JsonArray)monsters["monsters"]!, (JsonArray)items["items"]!, (JsonArray)spells["spells"]!);
        built["overworld.json"] = Overworld();
        built["dungeons.json"] = Dungeons();
        built["scrolls.json"] = Scrolls();
        built["towns.json"] = Towns();
        built["elementals.json"] = Elementals();
        if (shippedRuleTables is JsonObject overlay)
            built["rule-tables.json"] = RuleTables(overlay, (JsonArray)spells["spells"]!);
        built["strings/ja.json"] = Strings();
        return built;
    }

    // ---- monsters ----------------------------------------------------------------------------------------------------

    private JsonObject Monsters()
    {
        var d = _s.Read("DATM");
        var outList = new JsonArray();
        for (var i = 0; i < d.Length / 70; i++)
        {
            var rec = d[(i * 70)..((i + 1) * 70)];
            int? Item(int o)
            {
                var v = S16(rec, o);
                return v >= 1 && v < P4Source.ItemCount ? v : null;
            }
            var treasure = new[] { Item(0x3A), Item(0x3C), Item(0x3E) }.Where(v => v is not null).Select(v => v!.Value);
            outList.Add(Obj(
                ("id", Cid(i)), ("index", i), ("name", CStrAscii(rec, 0)),
                ("group_max", rec[0x14]), ("attack_power", S16(rec, 0x16)), ("damage", S16(rec, 0x18)),
                ("armor", S16(rec, 0x1A)), ("defense", S16(rec, 0x1C)), ("level", S16(rec, 0x1E)), ("xp", U32(rec, 0x20)),
                ("magic_resistance", rec[0x24]), ("hp_base", S16(rec, 0x26)), ("nice", S16(rec, 0x28)), ("gold", U32(rec, 0x2A)),
                ("spell_ability", S16(rec, 0x2E)), ("spell_ids", ArrOf(rec[0x30], rec[0x31], rec[0x32])),
                ("next_group_chance", S16(rec, 0x34)), ("companions", ArrOf(rec[0x36], rec[0x37])), ("movement", rec[0x38]),
                ("treasure_items", Arr(treasure)), ("creature_type", rec[0x40]), ("flier", rec[0x41] != 0),
                ("body_shape", rec[0x42]), ("damage_type_flag", rec[0x43] != 0), ("undead_or_demon", rec[0x44] != 0),
                ("special_chance", 0), ("specials", new JsonArray()), ("combat_rating", 0), ("reaction_modifier", 0)));
        }
        return Obj(
            ("_source", $"{Source}: DATM (disk 2), 84 x 70 B big-endian (FORMAT.md section 6.4)"),
            ("_fields", "group_max +14 [C] (rand 1..n; n = the party average level _par+0B when +14 > +0B + 1, s98); attack_power +16 [C] (attacks per turn " +
                        "max(1, trunc(v / 115 + 0.7)), hit trunc(v / n) - D; combat.md 4.2); damage +18 [C] (trunc(u^0.5 * v) - " +
                        "armour); armor +1A [C] (subtracted from party damage); defense +1C [C] (subtracted from the party's " +
                        "hit chance); level +1E [~]; xp +20 [C]; magic_resistance +24 [~] (resistance roll, combat.md 9); " +
                        "hp_base +26 [C] (per-monster HP: D4-055, rule-tables.json monster_hp); nice +28 [?] (summed into " +
                        "_emnice: Greet / Beg chances); gold +2A [C]; spell_ability +2E [~] (cast chance %); spell_ids +30 / +31 " +
                        "/ +32 bytes (1-65 spells, 66 / 67 breath, 68 paralysis, 69 plain attack; combat.md 4.1) [C]; " +
                        "next_group_chance +34 [C]; companions +36 / +37 = the two possible next-group ids [C]; movement +38 " +
                        "[C]; treasure_items +3A / +3C / +3E [C] (outdoor encounters only; ids 0 and > 156 dropped); " +
                        "creature_type +40 [?]; flier +41 [~]; body_shape +42 [C] (1 normal, 2 body only, 3 snake, 4 winged); " +
                        "damage_type_flag +43 [?] (giants etc., combat.md 3.4 type 5); undead_or_demon +44 [~] (Dispel Undead, " +
                        "Dissolve). special_chance / specials / combat_rating / reaction_modifier are Phantasie I fields with " +
                        "no IV meaning: 0 / empty."),
            ("monsters", outList));
    }

    // ---- items -------------------------------------------------------------------------------------------------------

    private static string ItemKind(int i) => i switch
    {
        0 => "none",
        <= 0x08 => "shield",
        <= 0x1B => "armor",
        <= 0x3F => "weapon",
        <= 0x4B => "bow",
        <= 0x54 => "thrown",
        <= 0x5E => "healing_potion",
        <= 0x68 => "magic_potion",
        <= 0x80 => "special_potion",
        <= 0x8B => "magic_item",
        <= 0x90 => "scroll",
        0x91 => "box",
        _ => "quest_item",
    };

    private static readonly Dictionary<string, string> StrCheck = new()
    {
        ["shield"] = "str", ["armor"] = "str", ["weapon"] = "str2_plus_dex", ["bow"] = "str_plus_dex", ["thrown"] = "str_plus_dex",
    };

    private JsonObject Items()
    {
        var outList = new JsonArray();
        foreach (var it in _s.Items())
        {
            var kind = ItemKind(it.Id);
            var rec = Obj(("id", Cid(it.Id)), ("index", it.Id), ("name", it.Id == 0 ? "NOTHING" : it.Name), ("value_factor", 0),
                ("value", it.Price), ("kind", kind), ("icon", it.Icon));
            if (StrCheck.TryGetValue(kind, out var check))
            {
                rec["points"] = it.Value;
                rec["min_str"] = it.Requirement;
                rec["str_check"] = check;
            }
            if (kind == "armor" && it.MovePct != 0)
                rec["move_pct"] = it.MovePct;
            outList.Add(rec);
        }
        return Obj(
            ("_source", $"{Source}: names _itm, records _idat (6 B BE: icon, value, requirement, move %, u16 price) [C] " +
                        "(FORMAT.md section 6.5, extracted/items.json)"),
            ("_note", "kind by id range [C] (_equip / _use): shield 1-8, armor 9-27, weapon 28-63 (melee), bow 64-75, thrown " +
                      "76-84, healing_potion 85-94 (Healing 1-10), magic_potion 95-104 (Magic 1-10), special_potion 105-128, " +
                      "magic_item 129-139, scroll 140-144 (item 140 + k shows SCR(k+1), _use_sc 0xfad0 [C]), box 145, " +
                      "quest_item 146-156 (keys, gems, pen, emerald, diamond, explosives). points = DEF / ATT (equals the item " +
                      "catalogue for every equipment item); min_str = the requirement b2, compared by str_check: str (shields, " +
                      "armour: b2 <= Str), str2_plus_dex (melee: b2 <= 2 Str + Dex), str_plus_dex (bows, thrown: b2 <= Str + " +
                      "Dex) [C] (_cal_user). value = the base price: shops sell at trunc(value * town price_index / 100) and " +
                      "buy at trunc(value / 4) + 1 [C] (towns.md 6). move_pct = armour's signed movement % (b3) [C]. icon = " +
                      "b0 [C]. D4-018 (user s81: manual Long Sword 14 points) is a rules option: the record keeps the code's " +
                      "13; rule-tables.json item_points_manual holds 14. Item 0 is 'NOTHING' (the table entry is empty)."),
            ("items", outList));
    }

    // ---- spells ------------------------------------------------------------------------------------------------------

    private static readonly Dictionary<int, string> SpellType = BuildSpellType();

    private static Dictionary<int, string> BuildSpellType()
    {
        var d = new Dictionary<int, string>();
        foreach (var n in new[] { 1, 2, 3, 4, 13, 40 })
            d[n] = "E";
        foreach (var n in new[] { 15, 50 })
            d[n] = "N";
        d[47] = "N/T";
        d[49] = "N/WD";
        foreach (var n in new[] { 11, 12, 16, 17, 18, 19, 42, 44, 45 })
            d[n] = "C/P";
        return d;
    }

    // The spell list's effect line per spell number (docs/manuals/phantasie4/phantasie4-manual-en.md "Spell list"); ours.
    private static readonly string[] SpellEffect =
    [
        "", "restores 1-9 HP; heals wounds", "restores 1-27 HP; heals wounds", "restores 1-57 HP; heals wounds",
        "restores 1-99 HP; heals wounds", "1-10 damage to one chosen enemy", "1-30 damage", "1-60 damage",
        "1-100 damage", "party stride +2", "party stride +5", "enemy stride to 1/2", "enemy stride to 3/4",
        "the whole party can fly", "pulls flying enemies to the ground", "identifies an item's nature",
        "party armour +1", "party armour +3", "party armour +6", "party armour +10",
        "confuses the enemy, costing it chances to cast", "confuses the enemy", "confuses the enemy",
        "confuses the enemy", "bow hit chance 50 %, damage +15 (one person)", "bow hit chance 100 %, damage +25",
        "bow hit chance 50 %, damage +15 (party)", "bow hit chance 100 %, damage +25 (party)",
        "bow hit chance to 250 % (one person)", "bow hit chance to 250 % (party)",
        "saps the enemy's strength, lowering its attack", "saps the enemy's strength", "saps the enemy's strength",
        "saps the enemy's strength", "1-8 damage to all enemies", "1-18 damage to all enemies",
        "1-30 damage to all enemies", "1-44 damage to all enemies", "puts the enemy to sleep",
        "moves everyone from the battlefield elsewhere", "revives one dead companion",
        "frightens the enemy into fleeing", "summons an elemental (Mud, Mist, Air)",
        "dissolves zombies, skeletons etc.", "caster's stride raised to 11; a superhuman warrior",
        "caster's stride raised to 14", "wakes sleeping companions", "moves the party to another town",
        "slowly dissolves the enemy", "tells the level of the area's enemies", "obtains one item (100 gold)",
        "7 damage to friend and foe in front", "15 damage", "24 damage", "35 damage",
        "1-18 damage to all friend and foe around", "1-25 damage", "1-35 damage", "1-43 damage",
        "party damage +2", "party damage +4", "party damage +7", "party damage +11",
        "Divine Aid: a god whispers a hint for the current map (spells-noncombat.md)",
        "Annihilate Enemy: not handled in the combat code [?]; casting empties MP (combat.md 9)",
        "Super Transport: Transportation to a random town of the group (spells-noncombat.md)",
    ];

    private static readonly Dictionary<int, int> ManualMpPages = new() { [11] = 1, [12] = 2, [48] = 1 };

    private static readonly int[] ListMp =
    [
        1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 2, 3, 3, 2, 1, 1, 2, 3, 4, 1, 2, 3, 4, 1, 3, 2, 4, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 2,
        3, 4, 2, 4, 1, 1, 1, 3, 2, 3, 4, 2, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4,
    ];

    private JsonObject Spells()
    {
        var codeMp = _s.Image(0x44B7C, 65);
        var rank = _s.Image(0x44C59, 66)[1..];
        var names = _s.Names["magic"];
        var outList = new JsonArray();
        for (var i = 0; i < names.Length; i++)
        {
            var n = i + 1;
            int mp;
            string cat;
            if (n <= 62)
            {
                mp = ManualMpPages.TryGetValue(n, out var m) ? m : ListMp[i];
                cat = SpellType.GetValueOrDefault(n, "C/A");
            }
            else
            {
                mp = codeMp[i];
                cat = n switch { 63 => "N/WD", 64 => "C/A", _ => "N/T" };
            }
            outList.Add(Obj(("id", Cid(i)), ("index", i), ("name", names[i]), ("dos_slot", i), ("category", cat),
                ("magic_point_cost", mp), ("code_magic_point_cost", codeMp[i]), ("rank", rank[i]),
                ("party_wide", names[i].StartsWith('*')), ("effect_summary", SpellEffect[n]),
                ("effect_coefficients_note", "Combat effects: logic-specs/combat.md 9; outside combat: spells-noncombat.md.")));
        }
        return Obj(
            ("_schema", "phantasie.spells/v1"),
            ("_source", $"{Source}: names _magic (spell number = index + 1) [C]; magic_point_cost / category / effect = the " +
                        "manual's spell list and pages (docs/manuals/phantasie4/phantasie4-manual-en.md); " +
                        "code_magic_point_cost = _getmp table 0x44B7C [C]; rank = table 0x44C59 [C]"),
            ("_note", "D4-044 (user s82: manual): magic_point_cost is the manual's (Slow 1 / 2 = 1 / 2, Arrow Flame 2 = 3, " +
                      "Party Arrow Flame 1 = 2, Dissolve = 1); the code's table (2 / 3, 2, 3, 3) is code_magic_point_cost, the " +
                      "X68000 option. #63-65 (Divine Aid, Annihilate Enemy, Super Transport) are the manual's undisclosed " +
                      "'????????' spells: cost and type from the code / the manual's section headings [~]. A name starting " +
                      "with '*' is the party-wide form [~]."),
            ("spells", outList));
    }

    // ---- names -------------------------------------------------------------------------------------------------------

    private JsonObject NamesFile(JsonArray mon, JsonArray it, JsonArray sp)
    {
        var t = _s.Names;
        JsonArray Pick(JsonArray a) => new(a.Select(x => (JsonNode)Obj(("index", x!["index"]!.DeepClone()), ("name", x["name"]!.DeepClone()))).ToArray());
        return Obj(
            ("_source", $"{Source}: P4.X pointer tables (extracted/names.json) [C]"),
            ("_note", "IV's own numbering. classes 0-9 (0-5 = Phantasie I's six in I's order; 6 Shadow Warrior, 7 Gargoyle; 8 " +
                      "Sage / 9 Lord only for Hall persons). races 0-34 (0-14 = I's fifteen in I's order; 15-17 Hill Giant, " +
                      "Gargoyle, Spriggan; 18-32 the mixed races; 33 '?' (Nikademus, the elementals); 34 Duck). Core's enums " +
                      "hold only I's members: IV-only classes and races stay raw numbers. body_areas in IV's order (_body: " +
                      "head, torso, right arm, left arm, right leg, left leg); body_states = wound levels 0-3 (_hurt holds the " +
                      "Japanese wound messages, strings/ja.json). origins = _origin; planes = _plname."),
            ("classes", Lst(Classes)),
            ("races", Lst(t["race"].Select(PyStrip))),
            ("spells", Pick(sp)),
            ("items", Pick(it)),
            ("monsters", Pick(mon)),
            ("spell_level_names", new JsonArray()),
            ("town_labels", new JsonArray()),
            ("skills", Lst(Skills)),
            ("social_classes", Lst(t["social"])),
            ("origins", Lst(t["origin"])),
            ("planes", Lst(t["plname"])),
            ("abilities", Lst(["Strength", "Intelligence", "Dexterity", "Constitution", "Charisma", "Luck"])),
            ("body_areas", Lst(Body)),
            ("body_states", Lst(Wounds)));
    }

    // ---- world -------------------------------------------------------------------------------------------------------

    /// <summary>A dungeon's display name: its English by id (the _dungeons string), else the Japanese.</summary>
    private string DungeonName(int n)
    {
        var ja = _s.Names["dungeons"][n];
        var id = P4xId(_s.NamePointers["dungeons"][n]);
        var en = _t.HasId(id) ? _t.En(id) : null;
        return string.IsNullOrEmpty(en) ? ja : en;
    }

    private string TownName(int n) => _t.En(P4xId(_s.NamePointers["towns"][n]));

    private JsonObject Overworld()
    {
        var mout = _s.Read("MOUT");
        if (mout.Length != 18 * 700)
            throw new InvalidDataException("MOUT: unexpected size");
        var inns = _s.Longs(0x3DABC, 7);
        var regions = new JsonObject();
        var places = new JsonArray();
        for (var n = 1; n <= 24; n++)
        {
            var r = MoutRecord[n];
            var rec = mout.AsSpan(r * 700, 700);
            var tiles = new int[26][];
            for (var y = 0; y < 26; y++)
                tiles[y] = rec.Slice(y * 26, 26).ToArray().Select(v => (int)v).ToArray();
            var h = rec[0x2A4..].ToArray();
            var towns = Enumerable.Range(0, 3).Select(k => (Town: h[2 * k], Byte2: h[2 * k + 1])).ToArray();
            var dungeons = Enumerable.Range(0, 3).Select(k => (Loc: h[6 + 3 * k], X: h[7 + 3 * k], Y: h[8 + 3 * k])).ToArray();
            var header = Obj(
                ("towns", new JsonArray(towns.Select(t => (JsonNode)Obj(("town", t.Town), ("byte_2", t.Byte2))).ToArray())),
                ("dungeons", new JsonArray(dungeons.Select(d => (JsonNode)Obj(("location", d.Loc), ("x", d.X), ("y", d.Y))).ToArray())),
                ("inns", Arr(h[16..20])),
                ("destination", Obj(("map_kind", h[20]), ("map", h[21]), ("x", h[22]), ("y", h[23]))));
            var plane = $"map{n}";
            var found = new List<(int Kind, int Sub, int X, int Y)>();
            for (var y = 0; y < 26; y++)
                for (var x = 0; x < 26; x++)
                {
                    int kind = tiles[y][x] / 10, sub = tiles[y][x] % 10;
                    if (kind is >= 14 and <= 18 && !found.Any(f => f.Kind == kind && f.Sub == sub))
                        found.Add((kind, sub, x, y));
                }
            foreach (var (kind, sub, x, y) in found.OrderBy(f => f.Y).ThenBy(f => f.X))
            {
                if (kind == 14 && towns[sub].Town != 0)
                {
                    int t = towns[sub].Town;
                    places.Add(Obj(("plane", plane), ("x", x), ("y", y), ("type", "town"), ("number", t), ("name", TownName(t)),
                        ("tile", kind * 10 + sub)));
                }
                else if (kind == 16 && dungeons[sub].Loc != 0)
                {
                    var d = dungeons[sub];
                    places.Add(Obj(("plane", plane), ("x", x), ("y", y), ("type", "dungeon"), ("number", d.Loc),
                        ("name", DungeonName(d.Loc)), ("tile", kind * 10 + sub), ("entry", Obj(("x", d.X), ("y", d.Y)))));
                }
                else if (kind == 15 && h[16 + sub] != 0)
                {
                    int inn = h[16 + sub];
                    var nameId = P4xId(inns[inn]);
                    places.Add(Obj(("plane", plane), ("x", x), ("y", y), ("type", "inn"), ("number", inn), ("name", _t.En(nameId)),
                        ("name_id", nameId), ("tile", kind * 10 + sub)));
                }
                else if (kind is 17 or 18)
                {
                    if (header["specials"] is not JsonArray specials)
                        header["specials"] = specials = new JsonArray();
                    specials.Add(Obj(("type", kind == 17 ? "pool" : "charon"), ("x", x), ("y", y)));
                }
            }
            regions[$"OUT{n}"] = Obj(
                ("region_id", n), ("plane", plane), ("size", 700), ("grid_width", 26), ("grid_height", 26), ("mout_record", r),
                ("tiles", Arr(tiles)), ("features", new JsonArray()), ("text_pages", new JsonArray()),
                ("embedded_text", new JsonArray()), ("header", header));
        }
        return Obj(
            ("_source", $"{Source}: MOUT (disk 2), 18 records x 700 B; record by map number = table 0x42DD2 [C] " +
                        "(logic-specs/outdoor.md section 1)"),
            ("_note", "One region per outdoor map number 1-24, keyed OUT<n> with region_id = n and plane = 'map<n>' (IV has " +
                      "no planes in the III sense; the key lets places name their map). Maps sharing a MOUT record (6, 7, 11, " +
                      "12 = rapids; 16, 18, 19, 24 = deep River of Hell) repeat its tiles. tiles[y][x] = the tile byte: " +
                      "terrain type = byte / 10, sub-index = byte % 10 [C]; rule-tables.json outdoor_terrain holds the type " +
                      "table (names, hours per step, encounter per mille, blocking). header = the record's bytes 0x2A4-0x2BB: " +
                      "towns {town id, byte 2 [?]}, dungeons {location = O_0xx, entry x, y}, inns (_inns index), destination " +
                      "of this map's pool / Charon {map kind, map, x, y} [C]; specials = the pool / Charon squares. Each town, " +
                      "inn, dungeon, pool and Charon is a 2 x 2 block of tiles; places list the top-left square; an inn's name_id is " +
                      "its _inns string (0x3DABC) [C]. Map grid: " +
                      "maps 1-15 sit 5 wide, maps 16-24 (Netherworld) 3 wide (outdoor.md section 1) [C]."),
            ("regions", regions),
            ("places", places));
    }

    private JsonObject Dungeons()
    {
        var levels = new JsonObject();
        var rowsByFile = _t.Rows.Where(r => r.Src.StartsWith("O_", StringComparison.Ordinal) && !r.NotText)
            .GroupBy(r => r.Src).ToDictionary(g => g.Key, g => g.ToList());
        var dnames = _s.Names["dungeons"];
        for (var n = 1; n <= 21; n++)
        {
            var pdt = _s.Read($"IND{n}.PDT");
            if (pdt.Length != 1922)
                throw new InvalidDataException($"IND{n}.PDT: unexpected size");
            var floor = new byte[900];
            for (var y = 0; y < 30; y++)
                for (var x = 0; x < 30; x++)
                    floor[y * 30 + x] = (byte)(BinaryPrimitives.ReadUInt16BigEndian(pdt.AsSpan(2 * (y * 31 + x))) & 0x3F);
            var ev = _s.EventFile(n);
            var (count, b, entries) = P4Events.Parse(ev);
            if (b != (count + 1) * 5)
                throw new InvalidDataException($"O_{n:000}: code base");
            var msgs = new JsonObject();
            foreach (var r in rowsByFile.GetValueOrDefault($"O_{n:000}", []))
            {
                if (r.Off < b)
                    continue;
                msgs[(r.Off - b).ToString(CultureInfo.InvariantCulture)] = Obj(
                    ("lines", Arr(_t.En(r).Split('$'))), ("options", new JsonArray()), ("file_offset", r.Off));
            }
            levels[$"DNG{n}"] = Obj(
                ("id", Cid(n)), ("name", DungeonName(n)), ("name_ja", dnames[n]), ("size", 1922),
                ("grid_width", 30), ("grid_height", 30), ("raw_hex", Convert.ToHexStringLower(floor)),
                ("words_hex", Convert.ToHexStringLower(pdt)), ("embedded_text", new JsonArray()), ("messages", msgs),
                ("events", Obj(
                    ("triggers", new JsonArray(entries.Select(e => (JsonNode)Obj(("x", e.X), ("y", e.Y), ("dir", e.Dir), ("address", e.Off))).ToArray())),
                    ("code_base", b), ("code_hex", Convert.ToHexStringLower(ev.AsSpan(b))))));
        }
        return Obj(
            ("_source", $"{Source}: IND<n>.PDT (disk 2, 31 x 31 big-endian words) + O_0<nn> (disk 1, event file) " +
                        "(logic-specs/events.md sections 1-2) [C]"),
            ("_note", "DNG<n> = location / dungeon map n (1-21, _dungeons; IND0.PDT is on the disk but no map number reads it " +
                      "[~]). The shared byte grid (raw_hex, 30 x 30, cell(x, y) = byte 30 y + x) holds each square's FLOOR TYPE " +
                      "only (word bits 0-5: 27-30 water, 6 opened chest; events.md 1). words_hex = the whole map as stored: " +
                      "31 x 31 big-endian words, word(x, y) at 2 (31 y + x): bits 0-5 floor, 6-10 north wall picture, 11-13 " +
                      "west wall, 14-15 [~]; row and column 30 hold the south / east walls of the last row / column. Doors 2 " +
                      "and 3 exist only where an event writes them. events.triggers = the event file's entries {x, y, dir (4 " +
                      "= any), address} in file order; x = 0x20 runs on map load, x = 0x21 on walking off the edge; " +
                      "events.code_hex = the bytecode from the code base (all addresses inside are base-relative, operands " +
                      "little-endian; opcodes events.md 4, tools/scripts/x68k_p4_events.py). messages: base-relative text " +
                      "address -> its English lines (s78 translation, '$' = line break; strings/ja.json holds the Japanese " +
                      "under the same address). Not checked in the running game."),
            ("levels", levels));
    }

    private JsonObject Scrolls()
    {
        var outList = new JsonArray();
        for (var k = 1; k <= 5; k++)
        {
            var lines = _t.Rows.Where(r => r.Src == $"SCR{k}").OrderBy(r => r.Line).ToList();
            var n = lines.Max(r => r.Line!.Value) + 1;
            var text = Enumerable.Repeat("", n).ToArray();
            foreach (var r in lines)
                text[r.Line!.Value] = _t.En(r);
            outList.Add(Obj(("id", k - 1), ("step", 0), ("text", string.Join("\n", text)), ("lines", Arr(text))));
        }
        var records = new JsonObject();
        for (var k = 0; k < 5; k++)
            records[(140 + k).ToString(CultureInfo.InvariantCulture)] = ArrOf(k);
        return Obj(
            ("_source", $"{Source}: SCR1-SCR5 (disk 2), fixed 35-byte lines, translated (extracted/text/strings.json) [C]"),
            ("_layout", "id = SCR file - 1; lines = the file's lines (blank where the line is empty); a line '*' ends a page " +
                        "(_use_sc shows up to 19 lines per page) [C]"),
            ("_notes", "item_records: scroll item 140 + k shows SCR(k + 1) (_use_sc builds 'scr' + ('1' + item - 0x8C)) [C]. " +
                       "Translations are keyed by the exact Japanese line, so a short line that recurs elsewhere can carry " +
                       "another context's English (e.g. SCR1 line 22); checked in game: not yet."),
            ("item_records", records),
            ("scrolls", outList));
    }

    private JsonObject Towns()
    {
        var twtyps = _s.Image(0x3F70E, 22);
        var ctab = _s.Longs(0x3FB3C, 22);
        var exits = _s.Longs(0x3FF20, 3 * 21);
        var ships = _s.Longs(0x4001C, 2 * 21);
        var outList = new JsonArray();
        for (var n = 1; n <= 21; n++)
        {
            int m = exits[3 * (n - 1)], x = exits[3 * (n - 1) + 1], y = exits[3 * (n - 1) + 2];
            int sx = ships[2 * (n - 1)], sy = ships[2 * (n - 1) + 1];
            outList.Add(Obj(("id", n), ("index", n - 1), ("name", TownName(n)), ("name_ja", _s.Names["towns"][n]),
                ("price_index", ctab[n]), ("town_type", twtyps[n]), ("exit", Obj(("map", m), ("x", x), ("y", y))),
                ("ship", sx < 0 ? null : Obj(("x", sx), ("y", sy))), ("stock", Arr(Enumerable.Repeat(0, P4Source.ItemCount)))));
        }
        return Obj(
            ("_source", $"{Source}: _towns (names), _twtyps 0x3F70E (type), _ctab 0x3FB3C (price factor) [C] " +
                        "(logic-specs/towns.md section 1)"),
            ("_layout", "id = map number _cmapn (1-21); price_index = T[map] in % (80 maps 1-9, 100 maps 10-12, 120 maps " +
                        "13-21), used by the shop, training and magic training; town_type 0 = Pendragon (Guild, Mystic, Armory, " +
                        "Bank, Inn, Hall of Glory), 1 = the same with a Shipwright instead of the Hall, 2 = no fifth building " +
                        "[C]; stock = the saved shop stock ('goods') per item id, all 0 in a new game (_load(0) clears it); each " +
                        "town load adds 8 random items from rule-tables.json shop_random_items (towns.md 6.3)."),
            ("_notes", ArrOf("No town has a temple (towns.md 1). exit = where leaving puts the party (_townexit 0x3FF20, int32 " +
                           "{outdoor map, x, y} by town - 1, read by _exittown) [C]; ship = the shipwright's mooring square on " +
                           "the exit map (_shipexit 0x4001C, int32 {x, y}; -1 = none: Pendragon, Tiris, Nether Village) [C]. " +
                           "As shipped: Astral City's and Astral Town's exits are a square of their own town block, Lingstroff's " +
                           "and Skull City's are two squares off their blocks, and Skull City's exit and ship squares are " +
                           "shining sea (towns.md 2). The outdoor map of each town is also in overworld.json places.")),
            ("towns", outList));
    }

    private JsonObject Elementals()
    {
        var e = _s.Read("ELMS");
        var outList = new JsonArray();
        for (var slot = 0; slot < e.Length / 190; slot++)
        {
            var r = e[(slot * 190)..((slot + 1) * 190)];
            outList.Add(Obj(
                ("name", CStrAscii(r, 14)), ("intelligence", r[0x30]), ("attributes", Arr(r[0x2F..0x35])),
                ("mp_ceiling", r[0x37]), ("magic_max", r[0x37]), ("magic_cur", r[0x38]), ("hp_max", r[0x39]), ("hp_cur", r[0x3A]),
                ("status", r[0x0A]), ("attack", r[0x3C]), ("parry", r[0x3E]), ("level", r[0x08]), ("guild_number", slot),
                ("race", r[0x19]), ("birth_race", r[0x19]), ("class", r[0x18]),
                ("weapon_damage_b", r[0x6B]), ("shield_soak", r[0xB6]), ("armor_soak", r[0xB7]),
                ("known_spells", Arr(Enumerable.Range(0, 65).Where(i => r[0x73 + i] == 1).ToArray())),
                ("magic_level", r[0x36]), ("movement_base", r[0x29]), ("social", r[0x1A]),
                ("strategy", Arr(r[0x2A..0x2F])), ("melee_bonus", r[0x3B])));
        }
        return Obj(
            ("_source", $"{Source}: ELMS (disk 2), 3 x 190 B character records [C] (FORMAT.md 6.2, 6.8)"),
            ("_notes", "Slot order as on disk: Mud, Mist, Air (Summon El. loads one into party slot 6; _par+14 picks it). " +
                       "class 3 = Fighter (Core's FIGHTER), race 33 = '?' (no Core member; raw). attributes = Str, Int, Dex, " +
                       "Con, Cha, Luck. attack / parry = skill values +3C / +3E; weapon_damage_b = the melee weapon value +6B; " +
                       "shield_soak / armor_soak = shield / armour values +B6 / +B7 (no item is worn; the values are " +
                       "stored). known_spells = spell number - 1 (state 1). strategy = the combat strategy bytes +2A..+2E " +
                       "(combat.md 8.2); melee_bonus = +3B (added to weapon_damage_b for the melee damage Wm by _set1)."),
            ("elementals", outList));
    }

    // ---- rule-tables (overlay + values from the player's files) ----------------------------------------------------

    private List<List<int>> SpellPages(int first, int count)
    {
        var bounds = _s.Image(0x44AC8 + 2 * first, 2 * count);
        return Enumerable.Range(0, count).Select(i =>
            _s.Image(0x44AE2 + bounds[2 * i], bounds[2 * i + 1] - bounds[2 * i] + 1).Select(n => n + 1).ToList()).ToList();
    }

    /// <summary>
    /// The paths of rule-tables.json that hold values read from the player's files (P4.X, TABL, FAME) or the original
    /// names; everything else in the file is ours (rules, notes, manual-decided amounts) and comes from the shipped file.
    /// </summary>
    public static readonly string[] RuleTablePathsFromCopy =
    [
        "classes", "races", "skills", "social_classes",
        "creation.race_ability_mod", "creation.class_ability_mod", "creation.combine_5x5", "creation.inheritance_exponent",
        "creation.new_person_exponent", "creation.random_race_lists", "creation.class_mask_by_race", "creation.stride_race",
        "creation.stride_class", "hp_growth.by_class", "magic_level.by_class", "skill_growth.by_skill", "spells_granted.by_class",
        "towns.price_index_by_map", "towns.type_by_map", "shop_random_items.items",
        "combat.mp_cost_manual", "combat.mp_cost_code", "combat.missile_class_bonus", "combat.flameball_code_squares",
        "combat.flameball_code_offsets", "combat.spell_pages_code.pages", "combat.spell_pages_code.heads",
        "combat.auto_spells_code", "combat.treasure.extra_items", "combat.battle.placement_columns",
        "combat.battle.edge_squares", "combat.battle.side_exits", "outdoor_terrain.name_ids_by_code_index",
        "outdoor_terrain.overview.blocks", "outdoor_terrain.overview.block_by_map", "magic_bag.items",
        "services.level_row_wrap.thief_row_before_table", "services.town_name_ids.ids", "services.divine_aid_hints",
        "services.ending.messages", "hall.persons",
    ];

    private JsonObject RuleTables(JsonObject overlay, JsonArray spellList)
    {
        var o = (JsonObject)overlay.DeepClone();
        var races = _s.Names["race"].Select(PyStrip).ToList();
        var pc = Enumerable.Range(0, P4Source.PlayerClasses).ToList();
        var randomLists = new JsonObject();
        for (var k = 0; k < 5; k++)
        {
            var v = _s.Longs(0x42a9e + 36 * k, 9);
            randomLists[k.ToString(CultureInfo.InvariantCulture)] = Arr(v[1..(1 + v[0])]);
        }
        var sideExits = _s.Longs(0x44670, 28);
        var endBounds = _s.Longs(0x40C00, 22);
        var messages = new JsonArray();
        for (var i = 0; i + 1 < endBounds.Length; i += 2)
        {
            int f = endBounds[i], l = endBounds[i + 1];
            messages.Add(Arr(_s.Longs(0x40C58 + 4 * f, l - f + 1).Select(P4xId).Where(_t.HasId)));
        }
        var values = new Dictionary<string, JsonNode?>
        {
            ["classes"] = Arr(Classes),
            ["races"] = Arr(races),
            ["skills"] = Arr(Skills),
            ["social_classes"] = Arr(_s.Names["social"]),
            ["creation.race_ability_mod"] = Arr(Enumerable.Range(0, 34).Select(_s.RaceAbilityMod)),
            ["creation.class_ability_mod"] = Arr(pc.Select(_s.ClassAbilityMod)),
            ["creation.combine_5x5"] = Arr(Enumerable.Range(0, 5).Select(a => _s.Longs(0x41516 + 20 * a, 5))),
            ["creation.inheritance_exponent"] = Arr(_s.Doubles(0x4157a, 27).Select(PyRound6)),
            ["creation.new_person_exponent"] = JsonValue.Create(_s.Doubles(0x42b74, 1)[0]),
            ["creation.random_race_lists"] = randomLists,
            ["creation.class_mask_by_race"] = Arr(Enumerable.Range(0, 33)
                .Select(r => Convert.ToString(_s.X.Image[0x42b52 + r], 2).PadLeft(8, '0'))),
            ["creation.stride_race"] = Arr(_s.Longs(0x41472, 33)),
            ["creation.stride_class"] = Arr(_s.Longs(0x414f6, 10)),
            ["hp_growth.by_class"] = Arr(pc.Select(_s.HpGrowth)),
            ["magic_level.by_class"] = Arr(pc.Select(_s.MagicLevel)),
            ["skill_growth.by_skill"] = Arr(Enumerable.Range(0, Skills.Length).Select(s => pc.Select(c => _s.SkillGrowth(s, c)))),
            ["spells_granted.by_class"] = Arr(pc.Select(_s.SpellsGranted)),
            ["towns.price_index_by_map"] = Arr(_s.Longs(0x3FB3C, 22)),
            ["towns.type_by_map"] = Arr(_s.Image(0x3F70E, 22)),
            ["shop_random_items.items"] = Arr(_s.GenItm()),
            ["combat.mp_cost_manual"] = Arr(spellList.Select(x => x!["magic_point_cost"]!.DeepClone())),
            ["combat.mp_cost_code"] = Arr(spellList.Select(x => x!["code_magic_point_cost"]!.DeepClone())),
            ["combat.missile_class_bonus"] = Obj(("bow", _s.Longs(0x4476E, 10)), ("thrown", _s.Longs(0x44796, 10))),
            ["combat.flameball_code_squares"] = Arr(_s.Image(0x44950, 4)),
            ["combat.flameball_code_offsets"] = Arr(_s.Longs(0x44954, 21)),
            ["combat.spell_pages_code.pages"] = Arr(SpellPages(0, 5)),
            ["combat.spell_pages_code.heads"] = Arr(_s.Longs(0x44A98, 6).Select(P4xId)),
            ["combat.auto_spells_code"] = Obj(("defence_random", _s.Longs(0x44146, 7)), ("attack_walk", _s.Longs(0x44162, 20))),
            ["combat.treasure.extra_items"] = Arr(_s.Longs(0x443CA, 31)),
            ["combat.battle.placement_columns"] = Arr(_s.Longs(0x44508, 7)),
            ["combat.battle.edge_squares"] = Arr(_s.Longs(0x446E0, 24)),
            ["combat.battle.side_exits"] = Arr(Enumerable.Range(0, 14).Select(i => new[] { sideExits[2 * i], sideExits[2 * i + 1] })),
            ["outdoor_terrain.name_ids_by_code_index"] = Arr(_s.Longs(0x42D30, 12).Select(P4xId)),
            ["outdoor_terrain.overview.blocks"] = Arr(Enumerable.Range(0, 3).Select(k => _s.Image(0x42ECC + 9 * k, 9))),
            ["outdoor_terrain.overview.block_by_map"] = Arr(_s.Longs(0x43732, 25)),
            ["magic_bag.items"] = Arr(_s.Longs(0x44E52, 10)),
            ["services.level_row_wrap.thief_row_before_table"] = Arr(_s.Tabl[0x6E7..0x6EA]),
            ["services.town_name_ids.ids"] = Arr(_s.Longs(0x3DA64, 22).Select(P4xId)),
            ["services.divine_aid_hints"] = Arr(_s.Longs(0x44E8A, 22).Select(P4xId)),
            ["services.ending.messages"] = messages,
            ["hall.persons"] = Arr(_s.Hall().Select(h => Obj(("slot", h.Slot), ("name", h.Name), ("race", h.Race), ("cls", h.Cls),
                ("level", h.Level), ("social", h.Social), ("origin", h.Origin), ("stats_str_int_dex_con_cha", h.Stats),
                ("p1_p2_p3_results", h.Results), ("descendants", h.Descendants), ("deeds_line", h.DeedsLine), ("alive", h.Alive)))),
        };
        if (values.Count != RuleTablePathsFromCopy.Length || RuleTablePathsFromCopy.Any(p => !values.ContainsKey(p)))
            throw new InvalidOperationException("rule-tables: path list and values disagree");
        // The places' spell lists the Python writes out as literals: check them against the table they come from.
        var places = SpellPages(5, 3);
        var expected = new[] { "outdoors", "dungeon", "town" }
            .Select(k => o["services"]?["spell_places"]?["x68000"]?[k]?.AsArray().Select(v => (int)v!).ToList()).ToList();
        for (var i = 0; i < 3; i++)
            if (expected[i] is { } e && !e.SequenceEqual(places[i]))
                throw new InvalidDataException("spell_places.x68000 does not match P4.X's _getmagic pages");
        foreach (var path in RuleTablePathsFromCopy)
            SetPath(o, path, values[path]);
        return o;
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var o = root;
        foreach (var p in parts[..^1])
        {
            if (o[p] is not JsonObject next)
                o[p] = next = new JsonObject();
            o = next;
        }
        o[parts[^1]] = value;
    }

    /// <summary>Python's <c>round(x, 6)</c> (correctly rounded through the exact decimal expansion).</summary>
    private static double PyRound6(double x) =>
        double.Parse(x.ToString("F6", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    // ---- strings ------------------------------------------------------------------------------------------------------

    private JsonObject Strings() => Obj(
        ("_schema", "phantasie.strings/v1"),
        ("_note", "Phantasie IV in-game text (X68000 P4.X, O_001-O_021, SCR1-5), Japanese as in the binary (Shift-JIS decoded). " +
                  "Ids: p4x.<image address> (relocation-confirmed rows and every Japanese row of the data segment), " +
                  "o<map>.<base-relative address> (event text, as the bytecode addresses it; '$' = line break), " +
                  "scr<n>.<line>. Built by tools/scripts/build_p4_gamedata.py."),
        ("game", "phantasie4"),
        ("language", "ja"),
        ("strings", _t.JaStrings.DeepClone()));
}
