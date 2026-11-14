using System.Text.Json.Nodes;
using static OpenPhantasie.Importer.Pipelines.P3Dos.P3Extract;

namespace OpenPhantasie.Importer.Pipelines.P3Dos;

/// <summary>
/// Port of <c>tools/scripts/build_p3_gamedata.py</c>: the extract objects (<see cref="P3Extract"/>) → GameData
/// <c>phantasie3/</c> in the shared schemas. Only the (b) / (c) files of 10-importer.md 2.4 are built here; the build's
/// class (a) outputs (<c>creation-tables.json</c>, <c>rules.json</c>, <c>game.json</c>) ship with the remaster (they
/// have been extended by hand since, so the build's version would hide newer shipped fields).
/// </summary>
public static class P3Build
{
    private const string Game = "p3";
    internal const string Source = "Phantasie III, DOS SSI 1988 (reverse-engineering/dos/phantasie3/extracted/, FORMAT.md)";

    private static readonly Dictionary<string, string> CoreClass = new()
    {
        ["Fighter"] = "FIGHTER", ["Thief"] = "THIEF", ["Priest"] = "PRIEST", ["Ranger"] = "RANGER", ["Monk"] = "MONK",
        ["Wizard"] = "WIZARD",
    };

    private static readonly Dictionary<string, int> CoreClassIndex = new()
    {
        ["THIEF"] = 0, ["MONK"] = 1, ["RANGER"] = 2, ["FIGHTER"] = 3, ["PRIEST"] = 4, ["WIZARD"] = 5,
    };

    private static readonly string[] BodyAreas = ["head", "left_arm", "right_arm", "torso", "left_leg", "right_leg"];
    private static readonly string[] BodyStates = ["okay", "injured", "broken", "gone"];

    private static string Cid(int n) => $"{Game}:{n}";

    private static JsonArray Lst(IEnumerable<string> xs) =>
        Arr(xs.Select((x, i) => (JsonNode)new JsonObject { ["index"] = i, ["name"] = x }));

    private static JsonNode? C(JsonNode? n) => n?.DeepClone();

    private static IEnumerable<JsonObject> Objs(JsonNode? arr) => arr!.AsArray().Select(n => n!.AsObject());

    // ---- monsters ----------------------------------------------------------------------------------------------

    public static JsonObject Monsters(JsonObject src) => new()
    {
        ["_source"] = $"{Source}: CREATURE.DAT, 80 x 38 B (FORMAT.md section 4.4; = Amiga monsinfo.dat)",
        ["_fields"] = "Group build fill_group root 0xe3e4 (logic-specs/encounters.md section 1) [C]: group_max = byte 14, the " +
                      "group count is rnd(group_max) (not I's group_size_factor, which is left out); attack_power = byte 15 " +
                      "* 10; damage 16, armor 17, defense 18 (s8), level 19, special_chance 20, specials 21-23, " +
                      "combat_rating 24, hp_base 25 (raw: values > 200 mean 100 * b - 20000 HP), reaction_modifier 26 (s8; " +
                      "the manual's reputation is its sign [~]), byte_27 (s8 > 41: this group alone, at most 2 monsters, " +
                      "a large picture [~]), next_group_chance 28, companions 29-30, treasure_items 31-32 (III item ids), " +
                      "byte_33 (1 on undead / demons and Constrictor; copied to the group +0x2e, purpose [?]), gold 34-35 " +
                      "and xp 36-37 big-endian raw (values over 64,000 mean (v - 64,000) * 1000, encounters.md section 4). " +
                      "No undead list or never-flees list: III has no decision on either (D3-024's final bosses 77-79 " +
                      "are in rule-tables.json).",
        ["monsters"] = Arr(Objs(src["monsters"]).Select(m => (JsonNode)new JsonObject
        {
            ["id"] = Cid((int)m["index"]!), ["index"] = C(m["index"]), ["name"] = C(m["name"]),
            ["armor"] = C(m["armor"]), ["damage"] = C(m["damage"]), ["level"] = C(m["level"]), ["xp"] = C(m["xp"]),
            ["special_chance"] = C(m["special_chance"]), ["specials"] = C(m["specials"]), ["hp_base"] = C(m["hp_base"]),
            ["combat_rating"] = C(m["combat_rating"]), ["companions"] = C(m["companions"]),
            ["reaction_modifier"] = C(m["reaction_modifier"]), ["next_group_chance"] = C(m["next_group_chance"]),
            ["defense"] = C(m["defense"]), ["gold"] = C(m["gold"]), ["treasure_items"] = C(m["treasure_items"]),
            ["attack_power"] = (int)m["attack_power_div10"]! * 10,
            ["group_max"] = C(m["group_max"]), ["byte_27"] = C(m["byte_27"]), ["byte_33"] = C(m["byte_33"]),
        })),
    };

    // ---- items -------------------------------------------------------------------------------------------------

    public static JsonObject Items(JsonObject src)
    {
        var check = new Dictionary<string, string>
            { ["shield"] = "str", ["armor"] = "str", ["bow"] = "str_plus_dex", ["weapon"] = "str2_plus_dex" };
        var list = new JsonArray();
        foreach (var it in Objs(src["items"]))
        {
            var i = (int)it["id"]!;
            var rec = new JsonObject
            {
                ["id"] = Cid(i), ["index"] = i, ["name"] = i == 0 ? "NOTHING" : C(it["name"]),
                ["value_factor"] = 0, ["value"] = C(it["value"]), ["kind"] = C(it["kind"]),
            };
            if (i is >= 1 and <= 100)
            {
                rec["points"] = C(it["points"]);
                rec["min_str"] = C(it["requirement"]);
                rec["str_check"] = check[(string)it["kind"]!];
            }
            list.Add(rec);
        }
        return new JsonObject
        {
            ["_source"] = $"{Source}: names = master string 150 + id; points DS:0x113c, requirement DS:0x11a2 (ids 0-100), " +
                          "value DS:0xf92 (u16) [C] (FORMAT.md section 4.1)",
            ["_note"] = "D3-005 (user s79: DOS names): 'Dagger +1/+2', 'Compound Bow +2', 'God Armour', 'Halberd' as the DOS " +
                        "string table spells them. Every point and requirement value equals the manual's tables (III PDF " +
                        "11-12). kind: shield 1-20, armor 21-40, bow 41-60, weapon 61-100 (melee), healing_potion 101-110, " +
                        "magic_potion 111-120, scroll 121-140, treasure 141-180 (180 = Wand of Nikademus). min_str = the " +
                        "attribute requirement, compared with str_check: str (shields, armour), str_plus_dex (bows), " +
                        "str2_plus_dex (melee) [C] (ovl2 0x39b5). value_factor is Phantasie I's V (price V*V*index/500) and " +
                        "is 0 here: III's price is value * town price_index / 100 (armory root 0x0fb8) [~]. Item 0 is " +
                        "'NOTHING' (the DOS string is '0').",
            ["items"] = list,
        };
    }

    // ---- spells ------------------------------------------------------------------------------------------------

    /// <summary>III manual spell table (III PDF 10 / pr.15): type codes.</summary>
    private static string? ManualType(int n) => n switch
    {
        1 or 2 or 3 or 4 or 44 => "E",
        >= 5 and <= 8 => "C/A",
        (>= 9 and <= 20) or 45 or 48 or 50 or 55 or 56 => "C/P",
        (>= 21 and <= 43) or 46 or 47 or 49 or 51 => "C/A",
        52 => "N/WD",
        54 => "N/T",
        _ => null,
    };

    /// <summary>
    /// The III spell numbers whose DOS name is Phantasie I's spell at the same number (the build matched them by name,
    /// spaces and dots ignored, against I's <c>spells.json</c>; every match sits at I's index n - 1). Fixed here so the
    /// import reads only I's own fields (category, effect_summary) by index and never I's names. Not in the set: 13-16
    /// (Arrow Flame, I's Strength), 48 (Summon Element.), 52 (Monster Evaluate), 53 (empty), 55-57 (III only).
    /// </summary>
    public static readonly IReadOnlySet<int> SameNameAsI = new HashSet<int>(
        Enumerable.Range(1, 12).Concat(Enumerable.Range(17, 31)).Concat([49, 50, 51, 54]));

    private static readonly Dictionary<int, (string Summary, string Note)> DecodedSpells = new()
    {
        [55] = ("The caster's Fire Bow percentage is 250 for the rest of the fight.",
            "[C] root 0xca34 (combat_spell_29_56): bow % word FAR:0xdd9c + 2*member = 250; damage word untouched (an " +
            "Arrow Flame's damage stays). Matches the manual (bows.md section 5). Cost 3 = slot 54 % 4 + 1."),
        [56] = ("Every party member's Fire Bow percentage is 250 for the rest of the fight.",
            "[C] root 0xca46: the same 250 for every party slot (0 .. party count - 1, whatever the status). Matches the " +
            "manual (bows.md section 5). Cost 4 = slot 55 % 4 + 1."),
    };

    /// <param name="p1Spells">Phantasie I's <c>spells.json</c> <c>spells</c> (only <c>category</c> and
    /// <c>effect_summary</c> are read, by index).</param>
    public static JsonObject Spells(JsonObject src, JsonArray p1Spells)
    {
        var list = new JsonArray();
        foreach (var s in Objs(src["spells"]))
        {
            var n = (int)s["number"]!;
            var rec = new JsonObject { ["id"] = Cid(n - 1), ["index"] = n - 1, ["name"] = C(s["name"]), ["dos_slot"] = n - 1 };
            if (n == 53)
            {
                rec["category"] = "removed";
                rec["magic_point_cost"] = null;
                rec["effect_summary"] = "Empty slot: Vision (#53) was removed (manual III PDF 15 / pr.24: 'All areas are " +
                                        "already mapped').";
            }
            else if (n == 57)
            {
                rec["category"] = "divine";
                rec["magic_point_cost"] = null;
                rec["effect_summary"] = "Divine Aid: III's only divine spell; learned outside the Guild, held in record " +
                                        "+0xf0 (FORMAT.md section 5). Effect and cost not decoded [?].";
            }
            else
            {
                var type = ManualType(n) ?? throw new InvalidDataException($"spell {n}: no manual type");
                rec["category"] = type;
                rec["magic_point_cost"] = (n - 1) % 4 + 1;
                var same = SameNameAsI.Contains(n) && n - 1 < p1Spells.Count ? p1Spells[n - 1]?.AsObject() : null;
                if (same is not null && (string?)same["category"] == type)
                {
                    rec["effect_summary"] = C(same["effect_summary"]);
                    rec["effect_coefficients_note"] = "Same spell, type and cost as Phantasie I per the manual " +
                                                      "(09-trilogy-manual-p3.md 1d); III's spell code not compared [?].";
                }
                else if (DecodedSpells.TryGetValue(n, out var d))
                {
                    rec["effect_summary"] = d.Summary;
                    rec["effect_coefficients_note"] = d.Note;
                }
                else
                {
                    rec["effect_summary"] = null;
                    rec["effect_coefficients_note"] = "New or changed in III (manual III PDF 10-11): effect not decoded [?].";
                }
            }
            list.Add(rec);
        }
        return new JsonObject
        {
            ["_schema"] = "phantasie.spells/v1",
            ["_source"] = $"{Source}: names = master string 331 + n [C]; type and cost = the manual's spell table (III PDF 10 / " +
                          "pr.15) [C] (render)",
            ["_note"] = "index = spell number - 1 (as Phantasie I for #1-52 and #54). #53 is the DOS table's empty slot, kept so " +
                        "positions line up; #57 Divine Aid's cost is not in the manual and not decoded [?]. #13-16 Arrow " +
                        "Flame / Party Arrow Flame replace I's Strength; #55 / #56 Accuracy / Party Accuracy are new.",
            ["spells"] = list,
        };
    }

    // ---- names -------------------------------------------------------------------------------------------------

    public static JsonObject Names(JsonObject nm, JsonArray mon, JsonArray it, JsonArray sp)
    {
        IEnumerable<string> Group(string k) => Objs(nm[k]).Select(x => (string)x["name"]!);
        JsonArray IndexName(JsonArray a) => Arr(a.Select(x => (JsonNode)new JsonObject
            { ["index"] = C(x!["index"]), ["name"] = C(x["name"]) }));
        return new JsonObject
        {
            ["_source"] = $"{Source}: MAIN.EXE master string table DS:0x1308 (FORMAT.md section 3) [C]",
            ["_note"] = "III's own numbering: classes Fighter 0 .. Wizard 5; races Human 0 .. Troll 15 with 5 = the 'Random' " +
                        "menu choice (rnd(10) + 5 -> 6..15), Elemental 16, Undead 17; status Dead 0, Okay 1, Asleep 2, Out 3, " +
                        "Down 4. Core's enums keep Phantasie I's order: rule-tables.json core_class / core_race map them by " +
                        "name. origins = record +0x58 values (master strings 129 + n). body_areas = record +0xf2 + 2k order " +
                        "(string 456).",
            ["classes"] = Lst(Group("classes").Select(P3Exe.PyStrip)),
            ["races"] = Lst(Group("races").Select(P3Exe.PyStrip)),
            ["status"] = Lst(Group("status")),
            ["spells"] = IndexName(sp),
            ["items"] = IndexName(it),
            ["monsters"] = IndexName(mon),
            ["spell_level_names"] = new JsonArray(),
            ["town_labels"] = new JsonArray(),
            ["skills"] = Lst(Group("skills")),
            ["social_classes"] = Lst(Group("social_classes")),
            ["origins"] = Lst(Group("origins").Skip(3)),
            ["body_areas"] = Lst(["Head", "Left Arm", "Right Arm", "Torso", "Left Leg", "Right Leg"]),
            ["body_states"] = Lst(Group("body_states")),
        };
    }

    // ---- world -------------------------------------------------------------------------------------------------

    public static JsonObject Overworld(JsonObject w)
    {
        var regions = new JsonObject();
        foreach (var p in Objs(w["planes"]))
            regions[$"PLANE{(int)p["plane"]!}"] = new JsonObject
            {
                ["region_id"] = C(p["plane"]), ["plane"] = C(p["key"]), ["size"] = (int)p["width"]! * (int)p["height"]!,
                ["grid_width"] = C(p["width"]), ["grid_height"] = C(p["height"]), ["source"] = C(p["source"]),
                ["arrival"] = C(p["arrival"]), ["tiles"] = C(p["tiles"]), ["features"] = new JsonArray(),
                ["text_pages"] = new JsonArray(), ["embedded_text"] = new JsonArray(),
            };
        string[] keys = ["plane", "x", "y", "type", "number", "name", "tile"];
        return new JsonObject
        {
            ["_source"] = $"{Source}: {(string)w["_source"]!}",
            ["_note"] = "One region per plane, keyed PLANE<n> with region_id = the game's plane number (0 Scandor, 1 Light, " +
                        "2 Dark, 4 Netherworld; logic-specs/planes.md section 1-2). tiles[y][x]. III has no feature records: " +
                        "its places are hard-coded squares (places below). arrival = the square a party lands on when it " +
                        "comes from another plane. Terrain: tile ranges below (Phantasie I's tile_rules do not apply).",
            ["regions"] = regions,
            ["start"] = C(w["start"]),
            ["places"] = Arr(Objs(w["places"]).Select(p =>
            {
                var o = new JsonObject();
                foreach (var k in keys)
                    o[k] = C(p[k]);
                return (JsonNode)o;
            })),
            ["terrain"] = C(w["terrain"]),
        };
    }

    public static JsonObject Dungeons(JsonObject dg)
    {
        var levels = new JsonObject();
        foreach (var d in Objs(dg["dungeons"]))
        {
            var msgs = new JsonObject();
            foreach (var t in Objs(d["texts"]))
            {
                var lines = Slices((string)t["text"]!, 38);
                while (lines.Count > 1 && lines[^1].Length == 0)
                    lines.RemoveAt(lines.Count - 1);
                if (lines.Count == 0)
                    lines.Add("");
                msgs[((int)t["index"]!).ToString()] = new JsonObject { ["lines"] = Strs(lines), ["options"] = new JsonArray() };
            }
            levels[$"DNG{(int)d["dungeon"]!}"] = new JsonObject
            {
                ["id"] = Cid((int)d["dungeon"]!), ["name"] = C(d["name"]), ["size"] = ((string)d["raw_hex"]!).Length / 2,
                ["grid_width"] = C(d["width"]), ["grid_height"] = C(d["height"]), ["raw_hex"] = C(d["raw_hex"]),
                ["embedded_text"] = new JsonArray(), ["messages"] = msgs,
                ["tables"] = new JsonObject
                {
                    ["level"] = C(d["level"]), ["forced_fights"] = C(d["forced_fights"]), ["exits"] = C(d["exits"]),
                    ["event_squares"] = C(d["event_squares"]), ["event_program_hex"] = C(d["event_program_hex"]),
                    ["text_slots"] = C(d["text_slots"]),
                },
            };
        }
        return new JsonObject
        {
            ["_source"] = $"{Source}: {(string)dg["_source"]!}",
            ["_note"] = "DNG<n> = dungeon n = DNGS.DAT record letter - 'A' (letters J H E D A C B K I F G, FORMAT.md 9.5). Grid 24 x 30 (cell(x, y) = raw byte 24y + x). messages: " +
                        "text slot index -> the text (pointer table at 0x593, text at pointer - 0x800), cut into 38-character " +
                        "lines [~] (the dungeon message width is not traced; 38 is the scroll window's). Which slot an event " +
                        "square's message byte names (message - 1 or message) is [?]. tables: level (0-5, the monster band " +
                        "12L), forced_fights (cells 0x80-0x89), exits (cells 0xd7-0xd9; destinations 29 / 31 / 41 = Light / " +
                        "Dark / Netherworld [C], others [?]), event_squares (cells 0xdd-0xff), event programs raw (op types " +
                        "1-4 traps, 5 fight; the rest [?]).",
            ["_cells"] = C(dg["_cells"]),
            ["levels"] = levels,
        };
    }

    public static JsonObject Scrolls(JsonObject sc)
    {
        var itemRecords = new JsonObject();
        foreach (var i in Objs(sc["items"]))
            itemRecords[((int)i["item"]!).ToString()] = C(i["records"]);
        return new JsonObject
        {
            ["_source"] = $"{Source}: {(string)sc["_source"]!}",
            ["_layout"] = "id = SCROLLS.DAT record (0-18); lines = 38-character slices [~]",
            ["_notes"] = C(sc["_notes"]),
            ["item_records"] = itemRecords,
            ["scrolls"] = Arr(Objs(sc["records"]).Select(r => (JsonNode)new JsonObject
                { ["id"] = C(r["record"]), ["step"] = 0, ["text"] = C(r["text"]), ["lines"] = C(r["lines"]) })),
        };
    }

    /// <param name="shipped">The shipped <c>towns.json</c> (our notes), or null.</param>
    public static JsonObject Towns(JsonObject tw, JsonObject? shipped)
    {
        static string Plane(int n) => n <= 14 ? "scandor" : n switch { 15 => "light", 16 => "dark", _ => "netherworld" };
        var list = new JsonArray();
        foreach (var t in Objs(tw["towns"]))
        {
            var n = (int)t["town"]!;
            if (n == 0)
                continue;
            list.Add(new JsonObject
            {
                ["id"] = n, ["index"] = n - 1, ["name"] = C(t["name"]), ["price_index"] = C(t["price_index"]),
                ["stock"] = C(t["stock"]), ["plane"] = Plane(n),
            });
        }
        var o = new JsonObject
        {
            ["_source"] = $"{Source}: {(string)tw["_source"]!}",
            ["_layout"] = C(tw["_layout"]),
            ["_notes"] = Strs([
                "id = the town number (FAR:0xdd98; 1 Pendragon .. 14 Sciattica, 15 Light, 16 Dark, 17 Death). The " +
                "INITARMS row 0 is unused and left out.",
                "plane: the plane the town is on (root 0x1e18 picks the map by town: 15 Light, 16 Dark, 17 " +
                "Netherworld) [C] (planes.md section 2). III has no TRANSPORTATION list and no restock list " +
                "(restocking [?]); the town square is in overworld.json places.",
                "Price: value * price_index / 100 (armory root 0x0f22, FORMAT.md section 4.1) [~]; training gold " +
                "uses the same index (training.md section 3).",
            ]),
            ["towns"] = list,
        };
        // Hand step (git eed7a3fc, s91 wrap: the s89 D3-033 armory restock decision): the committed _layout / _notes were
        // rewritten after the build to name the restock decision. They are our text: take them from the shipped file when it has them.
        if (shipped is not null)
            foreach (var key in new[] { "_layout", "_notes" })
                if (shipped[key] is { } v)
                    o[key] = v.DeepClone();
        return o;
    }

    // ---- elementals --------------------------------------------------------------------------------------------

    public static JsonObject Elementals(JsonObject rt)
    {
        var airSpells = rt["air_elemental_spells"]!["values"]!.AsArray().Select(v => (int)v!).ToList();
        (string Name, int Mp, int Hp, int Atk, int Parry, List<int> Spells)[] rows =
        [
            ("Air", 30, 150, 150, 32, airSpells),
            ("Mist", 20, 200, 210, 42, Enumerable.Range(25, 8).ToList()),
            ("Mud", 12, 250, 255, 38, []),
        ];
        var list = new JsonArray();
        for (var slot = 0; slot < rows.Length; slot++)
        {
            var (name, mp, hp, atk, parry, spells) = rows[slot];
            list.Add(new JsonObject
            {
                ["name"] = name, ["intelligence"] = 25, ["attributes"] = Ints(Enumerable.Repeat(25, 6)), ["mp_ceiling"] = mp,
                ["magic_max"] = mp, ["magic_cur"] = mp, ["hp_max"] = hp, ["hp_cur"] = hp, ["status"] = 1, ["attack"] = atk,
                ["parry"] = parry, ["level"] = 15, ["guild_number"] = slot, ["race"] = 16, ["birth_race"] = 16,
                ["class"] = CoreClassIndex["FIGHTER"],
                ["worn"] = new JsonObject { ["shield"] = 4, ["armor"] = 22, ["bow"] = 41, ["weapon"] = 64 },
                ["known_spells"] = Ints(spells.Select(n => n - 1)),
            });
        }
        return new JsonObject
        {
            ["_source"] = $"{Source}: ovl2 0x3f04 (logic-specs/elementals.md section 1) [C] static; Air's spell list DS:0x1bb2 " +
                          "[C] bytes",
            ["_notes"] = "class = Core's FIGHTER (3; III's own number is 0). race / birth_race 16 = III's Elemental (no Core " +
                         "enum member). attributes = STR, INT, DEX, CON, CHA, LCK. worn = the class kit, worn, carried count 0. " +
                         "known_spells = spell number - 1. guild_number = DOS roster slot (0-2; the manual lists them last). " +
                         "Other skills 0. In III the elemental needs one party member (not six) and does not count as one.",
            ["elementals"] = list,
        };
    }

    // ---- rule tables -------------------------------------------------------------------------------------------

    /// <summary>D3-032 (user s82): the rows where III's tables differ from Phantasie I's, with I's values.</summary>
    private static JsonObject SkillTableFixes(JsonObject rt, JsonObject p1ct)
    {
        var slot = new Dictionary<string, int>
        {
            ["attack"] = 0, ["parry"] = 1, ["find_item"] = 2, ["find_trap"] = 3, ["disarm_trap"] = 4, ["listen"] = 5,
            ["pick_lock"] = 7, ["swim"] = 9,
        };
        var p1 = p1ct["skill_coefficients"]!["by_class_level"]!;
        var output = new JsonObject();
        foreach (var (skill, rows) in rt["skill_tables"]!["by_skill"]!.AsObject())
        {
            foreach (var cl in Classes)
            {
                var mine = Enumerable.Range(0, 15).Select(lv => (int)p1[CoreClass[cl]]![lv]![slot[skill]]!).ToList();
                var theirs = rows![cl]!.AsArray().Select(v => (int)v!).ToList();
                if (!mine.SequenceEqual(theirs))
                {
                    if (output[skill] is not JsonObject o)
                        output[skill] = o = new JsonObject();
                    o[cl] = Ints(mine);
                }
            }
        }
        return output;
    }

    /// <summary>
    /// rule-tables.json blocks added to the committed file by hand after the build (ours: decoded constants and user
    /// decisions; git: <c>creation_age</c> s98 0d0d5ed0, <c>armory_restock</c> s89/s91 eed7a3fc, <c>town_entry_payout</c>
    /// s144 fc03d8ee, <c>transfer</c> s147 67e0e66f). Appended from the shipped file in this order.
    /// </summary>
    public static readonly string[] RuleTablesShippedBlocks = ["creation_age", "armory_restock", "town_entry_payout", "transfer"];

    public static JsonObject RuleTables(JsonObject rt, JsonObject nm, JsonObject p1ct, JsonObject? shipped)
    {
        string[] coreRaces = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL",
            "KOBOLD", "ORC", "GOBLIN", "LIZARDMAN", "MINOTAUR", "UNDEAD"];
        var raceMap = nm["transfer_race_map_I_to_III"]!.AsArray().Select(v => (int)v!).ToList();
        var coreRace = new JsonObject();
        for (var r = 0; r < 18; r++)
            if (raceMap.Contains(r))
                coreRace[r.ToString()] = coreRaces[raceMap.IndexOf(r)];
        var coreClass = new JsonObject();
        for (var k = 0; k < Classes.Length; k++)
            coreClass[k.ToString()] = CoreClass[Classes[k]];
        JsonObject Skill(string id, string name, string? stat, string? table) =>
            new() { ["id"] = id, ["name"] = name, ["seed_stat"] = stat, ["table"] = table };
        var hit = rt["hit_location"]!;
        var hitRows = new JsonArray();
        for (var i = 0; i < hit["_rows"]!.AsArray().Count; i++)
            hitRows.Add(new JsonObject { ["use"] = C(hit["_rows"]![i]), ["cumulative"] = C(hit["rows"]![i]) });
        var letters = rt["dungeon_letters"]!["values"]!.AsArray().Select(v => (string)v!).ToList();
        var entrance = rt["dungeon_entrance_x"]!["by_letter"]!;

        var o = new JsonObject
        {
            ["_schema"] = "phantasie.p3-rule-tables/v1",
            ["_source"] = $"{Source}: extracted/rule-tables.json (MAIN.EXE DGROUP, tools/scripts/extract_dos_p3_rules.py) " +
                          "and the specs named per block. Data for Core's III rules; nothing here is a formula.",
            ["classes"] = Strs(Classes),
            ["core_class"] = coreClass,
            ["_core_race"] = "III race -> Core Race name, from the inverse of the I -> III transfer map DS:0x4e [C]. 5 (the " +
                             "'Random' choice) and 16 (Elemental) have no Core member.",
            ["core_race"] = coreRace,
            ["skills"] = new JsonObject
            {
                ["_note"] = "The nine skills in record order (+0x30 + 4k value, +0x32 + 4k seed; FORMAT.md section 5) [C]. " +
                            "seed: '2*STAT+30' (root 0xa1f2); Fire Bow: 2*Level + 4*DEX + STR + 10, +30 Thief, +15 Elf " +
                            "(the manual's bonuses). table = the class table in skill_tables (training.md section 5).",
                ["order"] = new JsonArray(
                    Skill("attack", "Attack", "STR", "attack"),
                    Skill("parry", "Parry", "DEX", "parry"),
                    Skill("find_item", "Find Item", "INT", "find_item"),
                    Skill("find_trap", "Find Trap", "INT", "find_trap"),
                    Skill("disarm_trap", "Disarm Trap", "DEX", "disarm_trap"),
                    Skill("listen", "Listen", "INT", "listen"),
                    Skill("fire_bow", "Fire Bow", null, null),
                    Skill("pick_lock", "Pick Lock", "DEX", "pick_lock"),
                    Skill("swim", "Swimming", "CON", "swim")),
                ["fire_bow_seed"] = new JsonObject
                {
                    ["base"] = 10, ["per_level"] = 2, ["per_dex"] = 4, ["per_str"] = 1,
                    ["class_bonus"] = new JsonObject { ["Thief"] = 30 }, ["race_bonus"] = new JsonObject { ["2"] = 15 },
                },
                ["cap"] = 250,
                ["trainings_per_level"] = 3,
            },
            ["skill_tables"] = new JsonObject
            {
                ["_note"] = "C_k[class][level 1..15], III class order. " + (string)rt["skill_tables"]!["_address"]!,
                ["by_skill"] = C(rt["skill_tables"]!["by_skill"]),
            },
            ["skill_table_fixes"] = new JsonObject
            {
                ["_note"] = "D3-032 (user s82): five cells of skill_tables look like data errors (Thief Pick " +
                            "Lock L3, Ranger Attack L15, Ranger Parry L15, the Parry table one byte short so " +
                            "Monk / Wizard read a level ahead). These rows are Phantasie I's values, used by " +
                            "default; skill_tables as read is the p3.skill-tables = dos option.",
                ["by_skill"] = SkillTableFixes(rt, p1ct),
            },
            ["hp_dice"] = new JsonObject
            {
                ["_note"] = (string)rt["hp_dice"]!["_note"]! + "; levels 1..22", ["by_class"] = C(rt["hp_dice"]!["by_class"]),
            },
            ["training_xp_curve"] = new JsonObject
            {
                ["_note"] = "G[level], need = (1000 * G[L] - 80000) / max(class score, 17) (training.md " +
                            "section 2). " + (string)rt["training_xp_curve"]!["_address"]!,
                ["values"] = C(rt["training_xp_curve"]!["values"]),
            },
            ["training_gold_curve"] = new JsonObject
            {
                ["_note"] = "T[level], cost = T[L] * price_index * (20 - CHA)^2 / 100, 5..15000 " +
                            "(training.md section 3). " + (string)rt["training_gold_curve"]!["_address"]!,
                ["values"] = C(rt["training_gold_curve"]!["values"]),
            },
            ["max_level"] = new JsonObject
            {
                ["_note"] = "training clamps the level at 21; level >= 21 cannot train (training.md section 1) [C]", ["value"] = 21,
            },
            ["spell_rows"] = new JsonObject
            {
                ["_note"] = C(rt["spell_rows"]!["_address"]), ["by_class"] = C(rt["spell_rows"]!["by_class"]),
            },
            ["body"] = new JsonObject
            {
                ["_note"] = "Six areas in record order (+0xf2 + 2k), states 0..3 [C] (FORMAT.md section 5, " +
                            "logic-specs/body-areas.md). hit_location rows: cumulative percent in the column order given " +
                            "(DS:0x12cc) [C]; for a character the column k maps to record area [0, 3, 5, 2, 4, 1][k] (root " +
                            "0xe9ee). tall_monster_ids: the first group's id that sets the 'tall' flag (root 0x104c0) [C] " +
                            "(44 is probably meant to be 43 Valley Giant [~]). limbless_monster_ids: injury messages say " +
                            "\"'s body\" for every area (0xe98c) [C].",
                ["areas"] = Strs(BodyAreas),
                ["states"] = Strs(BodyStates),
                ["hit_location_columns"] = C(hit["_columns"]),
                ["hit_location_rows"] = hitRows,
                ["character_area_for_column"] = Ints([0, 3, 5, 2, 4, 1]),
                ["tall_monster_ids"] = Ints([44, 67, 68]),
                ["limbless_monster_ids"] = Ints([2, 6, 17, 27, 29, 39, 40, 41, 51, 52, 62, 65]),
            },
            ["social_classes"] = new JsonObject
            {
                ["_note"] = "Creation (ovl2 0x227d) [C]: r = rnd(100) for races 0-4 (Human, Dwarven, Elven, Gnome, Halfling), " +
                            "rnd(75) for the others; r < 30 Peasant, < 60 Labourer, < 90 Craftsman, else Noble. Starting gold " +
                            "in the bank = class * gold_per_class (FORMAT.md section 5.1). 4 Darklord / 5 Superhero are endgame " +
                            "titles (D3-006). D3-023 (user s81: manual): each level-up also pays class * gold_per_class " +
                            "(DOS pays nothing: an option, not built here). rules.json starting_gold is 0 for this reason.",
                ["names"] = Strs(Objs(nm["social_classes"]).Select(x => (string)x["name"]!)),
                ["roll_die_by_race_group"] = new JsonObject { ["base_races"] = 100, ["others"] = 75 },
                ["base_races"] = Ints([0, 1, 2, 3, 4]),
                ["thresholds"] = Ints([30, 60, 90]),
                ["gold_per_class"] = 256,
            },
            ["class_kit"] = new JsonObject
            {
                ["_note"] = "Every new (and transferred) character (ovl2 0x227d) [C]: carried [4, 22, bow, 64], all worn; bow 44 " +
                            "Short Bow for a Thief, else 41 Self Bow. first_spell by class (spell number; 0 = none).",
                ["shield"] = 4, ["armor"] = 22, ["weapon"] = 64,
                ["bow"] = new JsonObject { ["Thief"] = 44, ["default"] = 41 },
                ["first_spell"] = new JsonObject
                    { ["Fighter"] = 0, ["Thief"] = 0, ["Priest"] = 1, ["Ranger"] = 1, ["Monk"] = 5, ["Wizard"] = 5 },
            },
            ["score_weights"] = new JsonObject
            {
                ["_note"] = "Quest bits 0..7 of record +0x8a; score = sum + level / 6 (endgame.md section 2). " +
                            (string)rt["score_weights"]!["_address"]!,
                ["values"] = C(rt["score_weights"]!["values"]),
            },
            ["final_bosses"] = new JsonObject
            {
                ["_note"] = "D3-024 (user s81): the party cannot Flee from these (Nikademus x2, Lord Wood); an " +
                            "option lets it. Monster ids.",
                ["ids"] = Ints([77, 78, 79]),
            },
            ["found_items"] = new JsonObject
                { ["_note"] = "D3-020 (user s81: manual 90; options unlimited, DOS 60)", ["manual"] = 90, ["dos"] = 60 },
            ["roster"] = new JsonObject
            {
                ["_note"] = "D3-029 (user s81: manual 'dozens' = no limit by default; DOS 20 records incl. the three " +
                            "elementals, so 17 characters, as an option).",
                ["dos_records"] = 20, ["dos_characters"] = 17,
            },
            ["wilderness_encounters"] = new JsonObject
            {
                ["_note"] = "s83, root 0xde54 (logic-specs/encounters.md section 7) [C]: a step on any terrain but 10 / 8 / 15 " +
                            "fights when rnd(100) < chance; L = area_levels[x / 10 + 5 * (y / 15)] (root 0x1198); night " +
                            "encounters below night_below, then monster 12L + {0, 1, 4}; else kind 2 (12L + rnd(10) + 1) below " +
                            "kind2_below, kind 3 (12L + terrain_offsets[t + rnd(4)]) otherwise; ids > 74 or < 0 become 11, 61 " +
                            "becomes 60 (root 0xf143). Sleeping monsters below sleeping_below; the monsters notice the party " +
                            "when rnd(100) < 8L.",
                ["chance"] = 8, ["night_below"] = 38, ["kind2_below"] = 26, ["sleeping_below"] = 37, ["notice_per_level"] = 8,
                ["surprise_below"] = 51, ["party_surprise_below"] = 31, ["run_away_below"] = 81,
                ["area_levels"] = C(rt["wilderness_area_levels"]!["values"]),
                ["terrain_offsets"] = C(rt["wilderness_terrain_offsets"]!["values"]),
            },
            ["dungeon_entries"] = new JsonObject
            {
                ["_note"] = "Entrance square (x, 0) of dungeon n (logic-specs/dungeon-events.md section 1 / 7) [C]: x = the " +
                            "record letter's entrance x (DS:0x1bb6); dungeon 1 enters at x 5 (FAR:0xe59a, ovl4 0x84e). " +
                            "Index = dungeon number - 1.",
                ["x"] = Ints(letters.Select((ch, n) => n == 0 ? 5 : (int)entrance[ch]!)),
                ["letters"] = Strs(letters),
            },
            ["transfer_maps"] = new JsonObject
            {
                ["_note"] = "I -> III (DS:0x48 class, DS:0x4e race), indexed by Phantasie I's number [C]",
                ["class"] = C(nm["transfer_class_map_I_to_III"]), ["race"] = C(nm["transfer_race_map_I_to_III"]),
            },
        };
        if (shipped is not null)
            foreach (var key in RuleTablesShippedBlocks)
                if (shipped[key] is { } v)
                    o[key] = v.DeepClone();
        return o;
    }
}
