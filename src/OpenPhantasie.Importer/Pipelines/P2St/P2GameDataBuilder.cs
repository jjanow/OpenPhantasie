using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// Port of <c>build_p2_gamedata.py</c>: GameData/phantasie2 from the ST extracts. Phantasie I's shipped GameData
/// supplies only numbers and our own text (item points / strength fields, spell categories / costs / summaries, the
/// manual's creation-table cells and lifespan tables), so importing II never needs I imported.
/// <para>Not written: <c>rules.json</c> and <c>travel-time.json</c> (class a; the committed files were edited after
/// the script, commits a3f9eaed and ed122414, so they stay shipped), <c>strings/en.json</c> and
/// <c>graphics/index.json</c> (hand-written).</para>
/// <para>The script's three checks against I's original names (I's undead list by name, items 0-100 and spells 0-53
/// equal to I's by name) are left out: I's shipped overlay has no names. The value checks stay.</para>
/// </summary>
internal sealed class P2GameDataBuilder
{
    private const string Game = "p2";
    private const string Source = "Phantasie II, Atari ST v1.0 (reverse-engineering/atari-st/phantasie2/extracted/, FORMAT.md)";

    private readonly ImportContext _ctx;

    public P2GameDataBuilder(ImportContext ctx) => _ctx = ctx;

    private static string Cid(int n) => $"{Game}:{n}";

    private JsonNode P1(string file) =>
        _ctx.ReadShipped("phantasie1", file) ?? throw new InvalidOperationException($"shipped phantasie1/{file} missing");

    // ---- monsters ----------------------------------------------------------------------------------------------

    public static JsonObject Monsters(JsonArray src, JsonObject gfx)
    {
        var sprites = gfx["monsters"]!.AsArray();
        var size = sprites.ToDictionary(g => (int)g!["index"]!, g => ((int)g!["width"]!, (int)g["height"]!));
        var list = new JsonArray();
        foreach (var m in src)
        {
            var i = (int)m!["index"]!;
            var rec = J.O(("id", Cid(i)));
            foreach (var (k, v) in m.AsObject())
                if (k != "pad_23")
                    rec[k] = v!.DeepClone();
            if ((string)sprites[i]!["name"]! != (string)m["name"]!)
                throw new InvalidDataException($"sprite {i} is not {(string)m["name"]!}");
            var (w, h) = size[i];
            rec["sprite_width"] = w;
            rec["sprite_height"] = h;
            list.Add(rec);
        }
        return J.O(
            ("_source", $"{Source}: PHANT.PRG data+0x1c76, 80 x 58 B big-endian (FORMAT.md section 4); sprite sizes " +
                        "PHANT.PRG 0x2112e (FORMAT.md section 10.3)"),
            ("_fields", "Same record as the Amiga I game (DOS 57-B record + pad byte at +0x23, dropped here). The encounter " +
                        "builder build_monster_groups 0x0a72e reads every field DOS I's builder reads, at the same " +
                        "places [C] (logic-specs/encounters.md); the combat use of the other fields is carried over from " +
                        "DOS I (GameData/phantasie1/monsters.json _fields) [~]. sprite_width / sprite_height = the " +
                        "sprite's pixel size [C]; II's group fit test uses sprite_height + 3 (I's DOS table: " +
                        "sprite_width), see encounters.md section 3. Traits: DISPELL UNDEAD on the ST hits monsters whose " +
                        "XP is divisible by 7 (0x842c), FEAR only lowers the encounter level (0x8326), as DOS I; the " +
                        "manual's versions are DIFFERENCES-II.md D2-022 / D2-023 (user s80: manual, as I; PLUTO'S UNDEAD SNAKE is undead): " +
                        "monster-traits.json, the ST's tests as rules options p2.dispell-undead / p2.fear = st."),
            ("monsters", list));
    }

    // D2-022 (user s80): the manual's list, as I (GameData/phantasie1/monster-traits.json), plus PLUTO'S UNDEAD SNAKE.
    private static readonly string[] P2Undead =
        ["SKELETON", "ZOMBIE", "GHOUL", "LARGE SKELETON", "WIGHT", "WRAITH", "VAMPIRE", "GHOST", "SPIRIT", "PLUTO'S UNDEAD SNAKE"];

    public static JsonObject MonsterTraits(JsonArray mon)
    {
        var byName = new Dictionary<string, int>();
        foreach (var m in mon)
            byName[(string)m!["name"]!] = (int)m["index"]!;
        var undead = P2Undead.Select(n => byName.TryGetValue(n, out var i) ? i
            : throw new InvalidDataException($"monster {n} not in PHANT.PRG's table")).Order().ToList();
        return J.O(
            ("_source", "Remaster rule data, not in the ST monster table. Indices are monsters.json 'index'. " +
                        "See DIFFERENCES-II.md D2-022 / D2-023."),
            ("_fields", J.O(
                ("undead", "D2-022 (user s80: manual, as I's D-024, and PLUTO'S UNDEAD SNAKE (72) is undead): DISPELL UNDEAD " +
                           "affects these (manual: skeletons, zombies, ghosts and others returned from the grave). I's nine " +
                           "plus the snake. The ST's XP % 7 == 0 test (combat case 0x842c) is the rules option " +
                           "p2.dispell-undead = st: it also hits BANTIR (1), DEMON (48), HIGH DEMON (60) and misses the snake."),
                ("never_flees", "D2-023 (user s80: manual, as I: FEAR makes the monsters run; the ST's level drop is p2.fear = " +
                                "st). User s81: Pluto (79), PLUTO'S ICE DRAGON (78) and Pluto's eight minions (17, 43, 72-77; " +
                                "endgame.md 3.1) stand fast, as I's Black Lord; p2.fear-bosses = flee lets them run."))),
            ("undead", J.Ints(undead)),
            ("never_flees", J.Ints([17, 43, 72, 73, 74, 75, 76, 77, 78, 79])));
    }

    // ---- items -------------------------------------------------------------------------------------------------

    private static string KindOf(int i) => i switch
    {
        >= 1 and <= 20 => "shield",
        >= 21 and <= 40 => "armor",
        >= 41 and <= 100 => "weapon",
        _ => "other",
    };

    public JsonObject Items(JsonArray names)
    {
        var p1 = P1("items.json")["items"]!.AsArray().ToDictionary(x => (int)x!["index"]!, x => x!);
        var list = new JsonArray();
        foreach (var it in names)
        {
            var i = (int)it!["index"]!;
            var rec = J.O(("id", Cid(i)), ("index", i), ("name", J.Clone(it["name"])), ("value_factor", J.Clone(it["value_factor"])),
                ("kind", KindOf(i)));
            if (i <= 100)
            {
                // The script also checks the name equals I's; I's shipped overlay has no names, so only the value is checked.
                if ((int)p1[i]["value_factor"]! != (int)it["value_factor"]!)
                    throw new InvalidDataException($"item {i} differs from I: equipment data copied from I would be wrong");
                rec["points"] = J.Clone(p1[i]["points"]);
                rec["min_str"] = J.Clone(p1[i]["min_str"]);
                rec["str_check"] = J.Clone(p1[i]["str_check"]);
                if (i == 0)
                    rec["kind"] = J.Clone(p1[i]["kind"]);
            }
            list.Add(rec);
        }
        return J.O(
            ("_source", $"{Source}: PHANT.PRG value table data+0x180, name pointers data+0x2ec (FORMAT.md section 3) [C]"),
            ("_note", "Ids 0-100 have the same names and value factors as Phantasie I (all 181 values are identical). Their " +
                      "points / min_str / str_check are Phantasie I's index formulas (phantasie1/items.json _formulas) [~] " +
                      "for II: checked live only for the transfer starter kit (SM SHIELD 4, MACE 4, LEATHER 3 points, " +
                      "parity/roster-live.md). 101-110 are renamed HEAL POT1..9 / HEALING10; 121-140 = 14 lettered scrolls " +
                      "+ 6 blank slots; 141-148 AMULET 1-8; 149 FLMN WAND; 150 ---ORB---; 151-180 new treasures. What " +
                      "potions, amulets, wand and orb do in II is [?] (00-function-map.md section 5.6)."),
            ("items", list));
    }

    // ---- spells and names --------------------------------------------------------------------------------------

    private static readonly (int Local, int Slot)[] Divine = [(54, 57), (55, 56)];
    private static readonly Dictionary<int, string> DivineEffect = new()
    {
        [54] = "DIVINE I (#58): in a dungeon, a god (DEMIGOD / ATHENA / APOLLO / ZEUS) gives advice, dungeon message 105; " +
               "nothing elsewhere or in combat. Gained only in Phantasie I (magic-divine.md section 3) [C] static.",
        [55] = "DIVINE II (#57): in wilderness region 26 it summons a fight (monster r1*r2*0.0072, or PLUTO'S WYVERN / " +
               "SNAKE-DRAGON on r < 25); nothing elsewhere or in combat. Gained at the DNG3 pool (magic-divine.md section 2) " +
               "[C] static.",
    };

    private static List<(int Local, int Slot, string Name)> SpellSlots(JsonObject names)
    {
        var slots = names["spells"]!.AsArray().ToDictionary(s => (int)s!["slot"]!, s => (string)s!["name"]!);
        return Enumerable.Range(0, 54).Select(k => (k, k)).Concat(Divine).Select(o => (o.Item1, o.Item2, slots[o.Item2])).ToList();
    }

    public JsonObject Spells(JsonObject names)
    {
        var p1 = P1("spells.json")["spells"]!.AsArray().ToDictionary(s => (int)s!["index"]!, s => s!);
        var list = new JsonArray();
        foreach (var (local, slot, name) in SpellSlots(names))
        {
            var rec = J.O(("id", Cid(local)), ("index", local), ("name", name), ("dos_slot", slot));
            if (local < 54)
            {
                // Manual: II keeps I's 54-spell list, costs and types. (The script's name check against I is left out.)
                rec["category"] = J.Clone(p1[local]["category"]);
                rec["magic_point_cost"] = J.Clone(p1[local]["magic_point_cost"]);
                rec["effect_summary"] = J.Clone(p1[local]["effect_summary"]);
                rec["effect_coefficients_note"] = "As Phantasie I per the manual; II's spell code not compared [~].";
            }
            else
            {
                rec["category"] = "divine";
                rec["magic_point_cost"] = slot % 4 + 1;
                rec["effect_summary"] = DivineEffect[local];
                rec["effect_coefficients_note"] = "MP cost [C] (magic-divine.md section 1): (slot % 4) + 1 as every " +
                                                  "spell. Divine spells are never learnt at the Guild " +
                                                  "(guild_learn_spell 0x0d0d2, slot < 55) [C].";
            }
            list.Add(rec);
        }
        return J.O(
            ("_schema", "phantasie.spells/v1"),
            ("_source", $"{Source}: spell name table (pointer run data+0x5c4) [C]"),
            ("_note", "Local index = remaster spell number (I's numbering, DIVINE I = 54); dos_slot = the ST table slot (= the " +
                      "GUILD @96 flag word). The game numbers spells slot + 1: DIVINE II = #57, DIVINE I = #58 (D2-004). " +
                      "Slots 54, 55, 58, 59 have no name and are left out."),
            ("spells", list));
    }

    public static JsonObject Names(JsonObject nm, JsonArray mon)
    {
        JsonArray Lst(string key) => J.A(nm[key]!.AsArray().Select((x, i) => (JsonNode?)J.O(("index", i), ("name", J.Clone(x)))));
        return J.O(
            ("_source", $"{Source}: PHANT.PRG data-segment pointer runs (FORMAT.md section 3) [C]"),
            ("_note", "16 races: index 15 = UNDEAD (I's names.json lists 15). spell_level_names and town_labels are not " +
                      "extracted for II yet [?]."),
            ("classes", Lst("classes")),
            ("races", Lst("races")),
            ("status", Lst("status")),
            ("spells", J.A(SpellSlots(nm).Select(s => (JsonNode?)J.O(("index", s.Local), ("name", s.Name), ("dos_slot", s.Slot))))),
            ("items", J.A(nm["items"]!.AsArray().Select(x => (JsonNode?)J.O(("index", J.Clone(x!["index"])), ("name", J.Clone(x["name"])))))),
            ("monsters", J.A(mon.Select(m => (JsonNode?)J.O(("index", J.Clone(m!["index"])), ("name", J.Clone(m["name"])))))),
            ("spell_level_names", new JsonArray()),
            ("town_labels", new JsonArray()));
    }

    // ---- world -------------------------------------------------------------------------------------------------

    public static JsonObject Overworld(JsonObject ow)
    {
        var regions = new JsonObject();
        foreach (var (key, r) in ow["regions"]!.AsObject())
        {
            var reg = (JsonObject)r!.DeepClone();
            reg["embedded_text"] = new JsonArray();
            regions[key] = reg;
        }
        return J.O(
            ("_source", $"{Source}: {(string)ow["_source"]!}"),
            ("_note", (string)ow["_note"]! + " Debug fields of the extract (raw feature bytes, the 1000-1499 gap, picture polygons) " +
                                             "are left out; see extracted/maps/overworld.json."),
            ("grid_width", J.Clone(ow["grid_width"])), ("grid_height", J.Clone(ow["grid_height"])),
            ("tile_rules", J.Clone(ow["tile_rules"])),
            ("regions", regions));
    }

    private const int EventRecords = 0x4f6;
    private const int ScriptBase = 0x5aa;
    private static readonly HashSet<int> FourByteOps = [12, 13, 14, 15, 18, 19, 21];

    /// <summary>Event features a party can meet: those on the grid plus every GOTO EVENT target of their scripts.</summary>
    private static SortedSet<int> LiveEvents(byte[] raw)
    {
        var todo = new Stack<int>();
        foreach (var b in new HashSet<int>(raw.Take(33 * 38).Where(b => b >= 0xdd).Select(b => (int)b)))
            todo.Push(b);
        var seen = new SortedSet<int>();
        var scripts = new HashSet<int>();
        while (todo.Count > 0)
        {
            var f = todo.Pop();
            if (seen.Contains(f) || f > 0xff)
                continue;
            seen.Add(f);
            var rec = EventRecords + 5 * (f - 0xdc);
            var work = new Stack<int>();
            for (var c = 1; c <= Math.Max(raw[rec + 1], (byte)1); c++)
                if (rec + c + 1 < raw.Length)
                    work.Push(raw[rec + c + 1]);
            while (work.Count > 0)
            {
                var s = work.Pop();
                if (!scripts.Add(s))
                    continue;
                var p = ScriptBase + 2 * s;
                for (var n = 0; n < 17; n++)
                {
                    if (p + 3 >= raw.Length)
                        break;
                    int o = raw[p], a = raw[p + 1], b = raw[p + 2];
                    var op = o % 128;
                    if (op > 0x3f)
                        op -= 0x40;
                    if (op == 22)
                        todo.Push(a + 0xdc);
                    else if (op == 20)
                        work.Push(a);
                    else if (op == 21)
                        work.Push(b);
                    if (o <= 0x7f)
                        break;
                    p += FourByteOps.Contains(op) || o == 0xc0 ? 4 : 2;
                }
            }
        }
        return seen;
    }

    /// <summary>
    /// relabel_menus: where event record byte 1 (the option count) differs from the decoded menu or a label is
    /// blank, the labels are the message's last words (2+ spaces apart) [~]. Returns the ids changed.
    /// </summary>
    private static List<int> RelabelMenus(JsonObject lv, Dictionary<int, JsonObject> slots)
    {
        var raw = Convert.FromHexString((string)lv["raw_hex"]!);
        var msgs = lv["messages"]!.AsObject();
        var changed = new List<int>();
        foreach (var f in LiveEvents(raw))
        {
            var rec = EventRecords + 5 * (f - 0xdc);
            int mid = raw[rec] & 0x7f, count = raw[rec + 1];
            if (msgs[mid.ToString()] is not JsonObject m || count < 2)
                continue;
            var options = m["options"]!.AsArray();
            if (options.Count == count && options.All(o => Py.Strip((string)o!).Length > 0))
                continue;
            for (var k = 0; k < 3; k++)
            {
                var text = slots.TryGetValue(mid + k, out var slot) ? (string?)slot["text"] ?? "" : "";
                var words = Py.SplitOnSpaceRuns(Py.Strip(text));
                if ((words.Count == count || words.Count == count + 1) && (k > 0 || words.Count == count + 1))
                {
                    m["options"] = J.Strs(words.Skip(words.Count - count));
                    if (k == 0)
                        m["lines"] = J.Strs([words[0]]);
                    changed.Add(mid);
                    break;
                }
            }
        }
        return changed;
    }

    public static JsonObject Dungeons(JsonObject dg, JsonObject ms)
    {
        var levels = new JsonObject();
        var relabelled = new List<(string, List<int>)>();
        foreach (var (key, lvNode) in dg["levels"]!.AsObject())
        {
            var lv = (JsonObject)lvNode!.DeepClone();
            levels[key] = lv;
            var n = int.Parse(key[3..]);
            var area = ms["areas"]!.AsArray().First(a => (int)a!["id"]! == n)!;
            var slots = new Dictionary<int, JsonObject>();
            foreach (var s in area["slots"]!.AsArray())
                if (!(bool)s!["empty"]!)
                    slots[(int)s["id"]!] = s.AsObject();
            var ids = RelabelMenus(lv, slots);
            if (ids.Count > 0)
                relabelled.Add((key, ids));
        }
        return J.O(
            ("_source", $"{Source}: {(string)dg["_source"]!}"),
            ("_note", "Phantasie I level format [C] (FORMAT.md section 9.4). Messages decoded with I's rules; II's event / " +
                      "script interpreter is not compared with I's yet [?] (three references land on continuation records). " +
                      "Menus of live events whose option count differs from event record byte 1 (or with blank labels) are " +
                      "relabelled from the message's own words [~] (build_p2_gamedata.relabel_menus): " +
                      string.Join(", ", relabelled.Select(r => $"{r.Item1} msg {string.Join(", ", r.Item2)}")) + "."),
            ("levels", levels));
    }

    public static JsonObject Messages(JsonObject ms) => J.O(
        ("_source", $"{Source}: {(string)ms["_source"]!}"),
        ("_cipher", J.Clone(ms["_cipher"])),
        ("_layout", J.Clone(ms["_layout"])),
        ("areas", J.A(ms["areas"]!.AsArray().Select(a => (JsonNode?)J.O(
            ("id", J.Clone(a!["id"])), ("source", J.Clone(a["source"])), ("size", J.Clone(a["size"])),
            ("slot_count", a["slots"]!.AsArray().Count), ("record_count", a["records"]!.AsArray().Count),
            ("slots", J.Clone(a["slots"])), ("records", J.Clone(a["records"])))))));

    /// <summary>
    /// find_items (PHANT.PRG <c>0xaeb2</c>–<c>0xaee6</c>) never hands out a blank-named scroll slot: 122 / 123 become 121,
    /// 133 becomes 132, 135 / 136 become 134, 137 becomes 138 [C]. item_records maps those slots to the same lettered scroll.
    /// </summary>
    private static readonly Dictionary<int, int> BlankScrollItems = new()
    {
        [122] = 121, [123] = 121, [133] = 132, [135] = 134, [136] = 134, [137] = 138,
    };

    /// <summary>
    /// item_records (s201): scroll item 121–140 → the lettered scroll (its <c>id</c>) it shows, so the Client never falls
    /// back to "item − 120" (which put SCROLL B, item 124, on SCROLL D's text).
    /// </summary>
    private static JsonObject ScrollItemRecords(JsonObject sc)
    {
        var byItem = sc["scrolls"]!.AsArray().ToDictionary(s => (int)s!["item_id"]!, s => (int)s!["id"]!);
        var map = new JsonObject();
        for (var item = 121; item <= 140; item++)
            if (byItem.TryGetValue(BlankScrollItems.GetValueOrDefault(item, item), out var id))
                map[item.ToString(System.Globalization.CultureInfo.InvariantCulture)] = J.Ints([id]);
        return map;
    }

    public static JsonObject Scrolls(JsonObject sc) => J.O(
        ("_source", $"{Source}: {(string)sc["_source"]!}"),
        ("_layout", J.Clone(sc["_layout"])),
        ("_item_map", J.Clone(sc["_item_map"])),
        ("item_records", ScrollItemRecords(sc)),
        ("page_layout", "rows"),
        ("scrolls", J.A(sc["scrolls"]!.AsArray().Select(s => (JsonNode?)J.O(
            ("id", J.Clone(s!["id"])), ("step", 0), ("item_id", J.Clone(s["item_id"])), ("name", J.Clone(s["name"])),
            ("records", J.Clone(s["records"])), ("lines", J.Clone(s["lines"])), ("text", J.Clone(s["text"])))))));

    private static string? TownPlane(int id) => id switch
    {
        >= 1 and <= 6 => "ferronrah",
        >= 7 and <= 10 => "netherworld",
        _ => null,
    };

    private static List<int> TransportDestinations(int id) => id switch
    {
        >= 1 and <= 6 => Enumerable.Range(1, 6).Where(t => t != id).ToList(),
        >= 7 and <= 10 => [new Dictionary<int, int> { [7] = 8, [8] = 7, [9] = 10, [10] = 9 }[id]],
        _ => [],
    };

    public static JsonObject Towns(JsonObject tw)
    {
        var list = new JsonArray();
        foreach (var t in tw["towns"]!.AsArray())
        {
            var rec = (JsonObject)t!.DeepClone();
            var id = (int)rec["id"]!;
            rec["plane"] = TownPlane(id);
            rec["transport_destinations"] = J.Ints(TransportDestinations(id));
            list.Add(rec);
        }
        var notes = (JsonArray)tw["_notes"]!.DeepClone();
        notes.Add("plane and transport_destinations come from PHANT.PRG code, not from TWNS data " +
                  "(reverse-engineering/atari-st/phantasie2/logic-specs/towns.md sections 3-4) [C] static.");
        notes.Add("Every town offers the same services (one town dispatch, 0x0db5a); towns differ only in this data and in " +
                  "TRANSPORTATION's list (towns.md section 2).");
        return J.O(
            ("_source", $"{Source}: TWNS.INT bytes 2446-5949 (the initial TWNS.DAT), 12 x 292 B (FORMAT.md section 9.5)"),
            ("_layout", J.Clone(tw["_layout"])),
            ("_notes", notes),
            ("_formulas", J.O(
                ("buy_price", "floor(V*V * price_index / 500) + 1 (0x0c2f2), as I"),
                ("sell_price", "V*V / 8 at the Inn's Distribute SELL row; stock + 1, capped at 99 (0x0eaf4)"),
                ("restock", "town entry (0x0c0a8): 18x stock[rand/274] = 0, then 4x stock[restock_items[rand/(32767/avg_level)]]" +
                            " = 1 (index 0 when avg_level = 0), as I"),
                ("training_fee", "min(15000, floor(level^2.2) * (20 - CHA)^2 * price_index / 100) (0x0cff0..0x0d086); I clamps " +
                                 "20 - CHA at 0, II does not (no effect while CHA <= 20)"),
                ("learn_fee", "price_index * level * 90 / (CHA * INT * (9 - class)) (0x0d272), as I"))),
            ("towns", list));
    }

    // ---- creation tables, elementals ---------------------------------------------------------------------------

    public JsonObject Creation(JsonObject ct)
    {
        var p1ct = P1("creation-tables.json");
        var hal = ct["race_attribute_modifiers"]!["by_race"]!["HALFLING"]!;
        if ((int)hal["modifiers"]!["DEX"]! != 11)
            throw new InvalidDataException("HALFLING DEX changed upstream");
        // D2-009 (user: "manual, as in I"): the same two cells Phantasie I's GameData sets from the manual (D-001, D-006).
        var p1Hal = p1ct["race_attribute_modifiers"]!["by_race"]!["HALFLING"]!;
        hal["modifiers"]!["DEX"] = J.Clone(p1Hal["modifiers"]!["DEX"]);
        hal["ceilings"]!["DEX"] = J.Clone(p1Hal["ceilings"]!["DEX"]);
        var monk = ct["spell_levels"]!["by_class"]!["MONK"]!.AsArray();
        if ((int)monk[41]! != 7)
            throw new InvalidDataException("MONK spell 41 changed upstream");
        monk[41] = J.Clone(p1ct["spell_levels"]!["by_class"]!["MONK"]![41]);
        // D2-015 (user s79: manual, wizard level 13).
        var wiz = ct["spell_levels"]!["by_class"]!["WIZARD"]!.AsArray();
        if ((int)wiz[43]! != 0)
            throw new InvalidDataException("WIZARD spell 43 changed upstream");
        wiz[43] = 13;

        var o = J.O(
            ("_schema", J.Clone(ct["_schema"])),
            ("_source", $"{Source}: TWNS.INT bytes 0-2445 (byte-identical to Amiga I, value-identical to DOS I)"),
            ("_decisions", "D2-009 (DIFFERENCES-II.md, user: manual, as in I): HALFLING DEX modifier 12 / ceiling 20 " +
                           "(D-001) and MONK spell 41 (#42 SLEEP) not learnable at level 7 (D-006). D2-015 (user s79: " +
                           "manual): WIZARD spell 43 (#44 RESURRECTION) learnable at level 13, as the RANGER. Raw values: " +
                           "extracted/towns/creation-tables.json."),
            ("_lifespan", "lifespan_bands = the manual's bands +-10 %, as I (D2-021, user s80: manual, as I's D-003; copied " +
                          "from GameData/phantasie1/creation-tables.json). ST rule [C] (logic-specs/encounters.md section 8; create_character 0x0cc84..0x0cd0a, town_aging 0x0bba6): lifespan " +
                          "@252 = @10 x factor, @10 = rand/5461 + 8 (8..13, 14 when rand >= 32766); factor by race index: " +
                          "HUMAN, PIXIE 1; DWARF, HALFLING, GNOME 5/2; ELF 19; every other race (UNDEAD too) 2/5 " +
                          "(jump table 0x22976) = DOS I's factors. Aging on town entry as DOS: past 7/10 of @252 once " +
                          "'has aged' (stats x 4/5), past @252 'has died'. It is dos_tables.lifespan_factors, played by the " +
                          "rules option p2.lifespans = st. Race 15 UNDEAD (transfer / elementals only) has no row: the " +
                          "manual counts it among 'the others' (40-50) and the ST gives it 2/5 [C]."));
        foreach (var (k, v) in ct)
            if (!k.StartsWith('_'))
                o[k] = v!.DeepClone();
        o["lifespan_bands"] = J.Clone(p1ct["lifespan_bands"]);
        var factors = J.O(("_note", "D2-021 [C] (create_character 0x0cc84..0x0cd0a, jump table 0x22976): @252 = @10 * num / den, " +
                                    "the same factors as DOS I (dos_tables.lifespan_factors of GameData/phantasie1); checked on " +
                                    "town entry as @264 >> 8 (town_aging 0x0bba6)."));
        foreach (var (k, v) in p1ct["dos_tables"]!["lifespan_factors"]!.AsObject())
            if (!k.StartsWith('_'))
                factors[k] = v!.DeepClone();
        o["dos_tables"] = J.O(
            ("_source", "The Atari ST's own cells that the tables above replace by the manual, applied per save by the " +
                        "rules options p2.wizard-resurrection = st and p2.lifespans = st (Original (Atari ST) preset). " +
                        "D2-009 has no ST option (user: manual, as in I), so its cells are not here."),
            ("spell_levels", J.O(("_note", "D2-015: TWNS.INT +0x5a WIZARD spell slot 43 (#44 RESURRECTION) = 0, never learnable."),
                ("WIZARD", J.O(("43", 0))))),
            ("lifespan_factors", factors));
        return o;
    }

    public static JsonObject Elementals(JsonArray el) => J.O(
        ("_source", $"{Source}: TWNS.INT bytes 5950+22*318 (the Guild template's three elementals) [C]"),
        ("_notes", "Every field equals GameData/phantasie1/elementals.json except the names (FIRE_ELEM ...). " +
                   "known_spells = flag words equal to 1 (FIREFLASH I-IV), as the live SPELLS page shows (roster-live.md)."),
        ("elementals", J.A(el.Select(e => (JsonNode?)J.O(ElementalKeep.Select(k => (k, J.Clone(e![k]))).ToArray())))));

    private static readonly string[] ElementalKeep =
        ["name", "intelligence", "mp_ceiling", "magic_max", "magic_cur", "hp_max", "hp_cur", "status", "attack", "parry",
         "level", "guild_number", "weapon_damage_b", "shield_soak", "armor_soak", "race", "birth_race", "class", "known_spells"];

    // ---- constants and manifest --------------------------------------------------------------------------------

    public static JsonObject WildernessEncounters() => J.O(
        ("_source", "PHANT.PRG data 0x1fefe (16 bytes) [C] (logic-specs/encounters.md section 4): the same table as " +
                    "Phantasie I's COM DS 0x1a5c, read by the day branch of night_watch_encounter 0xfe38."),
        ("_note", "Day monster (0-based) = 12*(tile % 10) + 2*pair_table[tile / 10 + r / 25] + 2r/100 (r = rand / 327)."),
        ("pair_table", J.Ints([4, 1, 2, 3, 3, 4, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1])));

    public static JsonObject Manifest(IReadOnlyDictionary<string, JsonObject> built)
    {
        int Count(string file, string key) => built[file][key] switch
        {
            JsonArray a => a.Count,
            JsonObject o => o.Count,
            _ => throw new InvalidOperationException(key),
        };
        return J.O(
            ("_schema", "phantasie.game/v1"),
            ("_source", "Built by tools/scripts/build_p2_gamedata.py from " + Source),
            ("_note", "The tables this folder holds and their sizes, read by OpenPhantasie.Data GameTables.Load (and " +
                      "GameDataRepository.Load for play, with Core's Phantasie2Campaign; 09-trilogy.md T3). Its Atari ST graphics pack is graphics/atari-st.json " +
                      "(not built by this script)."),
            ("game", Game),
            ("platform", "atari-st"),
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
            ("_spell_cost_rule", "I: (DOS slot % 4) + 1 for every spell (OV2). II uses the same rule on its own slots [C] " +
                                 "(magic-divine.md section 1: DIVINE II slot 56 = 1 MP, DIVINE I slot 57 = 2 MP); every " +
                                 "spells.json cost is filled, but the checked rule stays null because the loader's slot map " +
                                 "(SpellId.DosSlot) only knows I's numbering."));
    }
}
