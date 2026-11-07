using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// II's GameData (today's schema, the ST build's shape) from the Apple II files alone, so an Apple-only import is a
/// playable II. Decided entries apply whatever the source (D2-009, D2-015, D2-021, D2-022 / 023: the manual's values).
/// The PENDING Apple-vs-ST entries (DIFFERENCES-II D2-046..050) are not decided here: the Apple's own values are the only
/// ones on the player's disks, so they are used, and each one is named in a warning (<c>import.json</c>).
/// <para>Nothing original is taken from the ST: no spell names (the Apple numbers its spells), no ST text lines.
/// Our own data (I's shipped item points / spell categories / creation decisions, the rules files) is used as the ST
/// build uses it. When the ST copy is (or was) imported too, its tables win and this writes nothing.</para>
/// </summary>
internal static class A2GameData
{
    private const string Game = P2Apple2Pipeline.GameId;
    private const string Source = "Phantasie II, Apple II v1.0 2/5/86 (4am crack; reverse-engineering/apple2/phantasie2/FORMAT.md)";

    /// <summary>The creation-table race keys (the loader's fixed names; display names are in names.json).</summary>
    private static readonly string[] RaceKeys =
        ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN", "LIZ.MAN", "MINOTAUR"];

    private static readonly string[] Attrs = ["STR", "INT", "DEX", "CON", "CHA"];

    public static void Write(ImportContext ctx, A2Source src, JsonObject gfxIndex)
    {
        if (ctx.ReadImportedGameData(Game, "game.json") is JsonObject existing && (string?)existing["platform"] != "apple2")
        {
            ctx.Warn("Phantasie II's game data comes from the Atari ST copy (imported as well); the Apple II copy adds " +
                     "only its art pack (choose it under the graphics options).");
            return;
        }
        var built = Build(ctx, src, gfxIndex, out var warnings);
        foreach (var (file, node) in built)
            ctx.WriteGameData(Game, file, node, indent: 1);
        foreach (var w in warnings)
            ctx.Warn(w);
    }

    public static Dictionary<string, JsonObject> Build(ImportContext ctx, A2Source src, JsonObject gfxIndex, out List<string> warnings)
    {
        warnings = [];
        var raw = A2Tables.Build(src, forGameData: true);
        var b = new P2GameDataBuilder(ctx);
        var monstersRaw = raw["monsters.json"]["monsters"]!.AsArray();
        var namesRaw = raw["names.json"];

        var nm = new JsonObject
        {
            ["classes"] = J.Strs(namesRaw["classes"]!.AsArray().Select(x => (string)x!["name"]!)),
            ["races"] = J.Strs(namesRaw["races"]!.AsArray().Select(x => (string)x!["name"]!)),
            ["status"] = J.Strs(namesRaw["status"]!.AsArray().Select(x => (string)x!["name"]!)),
            ["spells"] = J.A(Enumerable.Range(0, 60).Select(s => (JsonNode?)J.O(("slot", s), ("name", $"SPELL {s + 1}")))),
        };
        var items = Items(ctx, raw["items.json"]);
        nm["items"] = J.A(items.Select(i => (JsonNode?)J.O(("index", J.Clone(i!["index"])), ("name", J.Clone(i["name"])))));

        var gfxSizes = MonsterWidths(gfxIndex);
        var built = new Dictionary<string, JsonObject>
        {
            ["monsters.json"] = Monsters(monstersRaw, gfxSizes),
            ["monster-traits.json"] = P2GameDataBuilder.MonsterTraits(monstersRaw),
            ["items.json"] = Retag(b.Items(items), $"{Source}: ITEM (names 1-180) and TINFO +89+id (value factors) [C]; id 0 " +
                                                   "(the empty slot) is not on the Apple: its value and kind are I's"),
            ["spells.json"] = Retag(b.Spells(nm), $"{Source}: the Apple has no spell names (spells are numbered 1-60, TOWN2 " +
                                                  "3060, 6882): each is named by its number (dos_slot + 1)"),
            ["names.json"] = Retag(P2GameDataBuilder.Names(nm, monstersRaw), $"{Source}: TOWN2 260 / 270, OUTDOORS2 290, ITEM, MN2x; " +
                                                                              "spells by number"),
            ["overworld.json"] = Overworld(raw["overworld.json"], warnings),
            ["dungeons.json"] = Dungeons(raw["dungeons.json"]),
            ["scrolls.json"] = Scrolls(raw["scrolls.json"]),
            ["towns.json"] = Retag(P2GameDataBuilder.Towns(Towns(raw["towns.json"], raw["overworld.json"])),
                $"{Source}: TF2 (the new-game template), 10 x 250 B"),
            ["creation-tables.json"] = Creation(ctx, raw["creation-tables.json"]),
            ["elementals.json"] = Elementals(),
            ["wilderness-encounters.json"] = P2GameDataBuilder.WildernessEncounters(),
        };
        built["game.json"] = Manifest(built);

        warnings.Add("Phantasie II from the Apple II copy: some of its numbers and texts differ from the Atari ST version " +
                     "and are still open questions (DIFFERENCES-II D2-046 to D2-050). This import uses the Apple's values: " +
                     "D2-047 five monster numbers (VIPER, FIRE ELEMENTAL, three of Pluto's beasts), D2-048 DEMONIAC's prices " +
                     "and the SCROLL D / E stock of IPSWICH and FERRON, D2-049 two skill cells and a few map / dungeon cells.");
        warnings.Add("D2-046: the map is shown as the remaster (and the Atari ST) draw it, but the Apple's scroll texts give " +
                     "directions for the Apple's map, which is turned a quarter (its SOUTH is the shown map's WEST, NORTH is " +
                     "EAST, WEST is NORTH), and the Black castle's entrances lead to the Apple's start cells.");
        warnings.Add("D2-050: the Apple II has no spell names; spells show as SPELL 1 to SPELL 58 (the Apple's numbers). " +
                     "Names, scrolls and dungeon messages are the Apple's (upper case, some other wording); the remaster's " +
                     "own lines stay as they are.");
        warnings.Add("Phantasie II's elementals are not on the Apple II disks: the remaster's three (the same numbers as " +
                     "Phantasie I's and the Atari ST's) are used.");
        return built;
    }

    private static JsonObject Retag(JsonObject o, string source)
    {
        o["_source"] = source;
        return o;
    }

    // ---- items ------------------------------------------------------------------------------------------------

    private static JsonArray Items(ImportContext ctx, JsonObject itemsRaw)
    {
        var p1 = ctx.ReadShipped("phantasie1", "items.json")?["items"]?.AsArray()
                 ?? throw new InvalidOperationException("shipped phantasie1/items.json missing");
        var p10 = p1.First(x => (int)x!["index"]! == 0)!;
        var list = new JsonArray { J.O(("index", 0), ("name", "NOTHING"), ("value_factor", J.Clone(p10["value_factor"]))) };
        foreach (var it in itemsRaw["items"]!.AsArray())
            list.Add(J.O(("index", J.Clone(it!["index"])), ("name", J.Clone(it["name"])), ("value_factor", J.Clone(it["value_factor"]))));
        return list;
    }

    // ---- monsters ---------------------------------------------------------------------------------------------

    private static Dictionary<int, int> MonsterWidths(JsonObject index)
    {
        // The idle picture's width (the ST's sprite_width).
        return index["monsters"]!.AsObject().ToDictionary(kv => int.Parse(kv.Key, CultureInfo.InvariantCulture),
            kv => (int)kv.Value!["idle"]!["size"]![0]!);
    }

    private static JsonObject Monsters(JsonArray src, Dictionary<int, int> widths)
    {
        var list = new JsonArray();
        foreach (var m in src)
        {
            var rec = new JsonObject();
            foreach (var (k, v) in m!.AsObject())
                if (k != "a2")
                    rec[k] = v!.DeepClone();
            var i = (int)m["index"]!;
            rec["sprite_width"] = widths[i];
            rec["sprite_height"] = J.Clone(m["a2"]!["group_height"]);
            rec["a2"] = m["a2"]!.DeepClone();
            list.Add(rec);
        }
        return J.O(
            ("_source", $"{Source}: MN20..MN29, 8 records x 25 CR-ended fields (OUTDOORS2 line 910) [C]"),
            ("_fields", "The ST build's field names (same meanings, FORMAT.md 3.1). sprite_width = the Apple idle picture's " +
                        "width; sprite_height = the Apple's rank height YS% (a2.group_height, OUTDOORS2 1480-1500), the " +
                        "number the Apple lays its groups out with; the group fit test (encounters.md 3) is the ST's [~]. " +
                        "a2 = the shape / SCALE / HCOLOR the art pack draws with. D2-047 (PENDING): five monsters' numbers " +
                        "differ from the ST's; these are the Apple's."),
            ("monsters", list));
    }

    // ---- overworld --------------------------------------------------------------------------------------------

    /// <summary>
    /// The ST shape: text cells re-encoded in the ST's page form (Core's <c>LocationTextPages</c>): the Apple prints one
    /// CR-ended string from slot <c>arg</c>; its screen lines become pages <c>arg</c>, <c>arg + 1</c>... and the arg
    /// becomes <c>arg</c> / <c>10 + arg</c> / <c>20 + arg</c> for one / two / three lines.
    /// </summary>
    private static JsonObject Overworld(JsonObject ow, List<string> warnings)
    {
        var regions = new JsonObject();
        var played = new List<string>();
        foreach (var (key, node) in ow["regions"]!.AsObject())
        {
            var r = node!.AsObject();
            var tiles = r["tiles"]!.AsArray();
            var pages = new SortedDictionary<int, string>();
            var feats = new JsonArray();
            foreach (var f in r["features"]!.AsArray())
            {
                var o = J.O(("index", J.Clone(f!["index"])), ("x", J.Clone(f["x"])), ("y", J.Clone(f["y"])), ("arg", J.Clone(f["arg"])),
                    ("dest_x", J.Clone(f["dest_x"])), ("dest_y", J.Clone(f["dest_y"])));
                if (f["a2_lines"] is JsonArray lines)
                {
                    var arg = (int)f["arg"]!;
                    var n = Math.Clamp(lines.Count, 1, 3);
                    if (arg >= 10 || lines.Count > 3)
                        warnings.Add($"{key}: the text at ({(int)f["x"]!}, {(int)f["y"]!}) does not fit the ST page form; shown cut.");
                    for (var k = 0; k < n; k++)
                    {
                        var text = k < lines.Count ? (string)lines[k]! : "";
                        if (pages.TryGetValue(arg + k, out var had) && had != text)
                            warnings.Add($"{key}: text page {arg + k} is shared by two texts; the first is kept.");
                        else
                            pages[arg + k] = text;
                    }
                    o["arg"] = arg + 10 * (n - 1);
                }
                feats.Add(o);
            }
            if ((bool)r["a2_played"]!)
                played.Add(key.Replace(".DAT", ""));
            regions[key] = J.O(("region_id", J.Clone(r["region_id"])), ("size", J.Clone(r["size"])), ("grid_width", 26),
                ("grid_height", 20), ("tiles", tiles.DeepClone()), ("features", feats),
                ("text_pages", J.A(pages.Select(p => (JsonNode?)J.O(("index", p.Key), ("text", p.Value))))),
                ("embedded_text", new JsonArray()));
        }
        if (played.Count > 0)
            warnings.Add($"This Apple II copy has been played: on {string.Join(", ", played)} the fog is already lifted where the " +
                         "party went (the Apple saves the region's map back to the disk). Fixed fights are not stored there, so " +
                         "only the fog differs. An unplayed copy gives the original fog.");
        var rules = new JsonObject();
        foreach (var (k, v) in P2StExtract.ClassRules)
            rules[k] = v;
        return J.O(
            ("_source", $"{Source}: OUTT<n> (side A; 768 B as shipped, 756 B once played) [C]"),
            ("_note", "26 x 20 grid as the ST build: the Apple stores the same bytes (cell (CX, CY) = the ST's (y, 25 - x)) and " +
                      "draws them turned a quarter (D2-046, PENDING); the map here is the ST's orientation. Fog = tile + 120. " +
                      "Text cells: the Apple's CR-ended string from slot arg, put into the ST's page form (pages = its screen " +
                      "lines, arg + 10 per extra line)."),
            ("grid_width", 26), ("grid_height", 20), ("tile_rules", rules), ("regions", regions));
    }

    // ---- dungeons ---------------------------------------------------------------------------------------------

    private const int EventRecords = 0x4f6;
    private static readonly Regex Marker = new(@"(?<![A-Z0-9])([1-9])-", RegexOptions.CultureInvariant);

    /// <summary>The largest option count of an event record pointing at each message (record byte 1).</summary>
    private static Dictionary<int, int> OptionCounts(byte[] raw)
    {
        var d = new Dictionary<int, int>();
        for (var f = 0xdc; f <= 0xff; f++)
        {
            var rec = EventRecords + 5 * (f - 0xdc);
            int mid = raw[rec] & 0x7f, count = raw[rec + 1];
            d[mid] = Math.Max(d.GetValueOrDefault(mid), count);
        }
        return d;
    }

    /// <summary>
    /// An Apple menu ("...  1-LEAVE  2-OPEN IT") into the ST's lines + options. The markers 1- .. n- must appear in
    /// order; otherwise (e.g. "LABELED 1-9 / PUSH WHICH BUTTON?") the options are the digits, as the ST shows them.
    /// </summary>
    internal static (List<string> Lines, List<string> Options) SplitMenu(IReadOnlyList<string> lines, int count)
    {
        var text = string.Join("\n", lines);
        var at = new List<(int Start, int End)>();
        var pos = 0;
        for (var k = 1; k <= count; k++)
        {
            Match? hit = null;
            for (var m = Marker.Match(text, pos); m.Success; m = m.NextMatch())
                if (m.Groups[1].Value == k.ToString(CultureInfo.InvariantCulture))
                {
                    hit = m;
                    break;
                }
            if (hit is null)
                return (lines.ToList(), Enumerable.Range(1, count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList());
            at.Add((hit.Index, hit.Index + hit.Length));
            pos = hit.Index + hit.Length;
        }
        var head = text[..at[0].Start].Split('\n').Select(A2Text.RStrip).ToList();
        while (head.Count > 0 && A2Text.Strip(head[^1]).Length == 0)
            head.RemoveAt(head.Count - 1);
        var options = new List<string>();
        for (var k = 0; k < at.Count; k++)
        {
            var end = k + 1 < at.Count ? at[k + 1].Start : text.Length;
            options.Add(Regex.Replace(text[at[k].End..end].Replace('\n', ' '), @"\s+", " ").Trim());
        }
        return (head, options);
    }

    /// <summary>How many of the markers 1-, 2-, ... appear in order (0 when fewer than two).</summary>
    private static int MarkedOptions(IReadOnlyList<string> lines)
    {
        var text = string.Join("\n", lines);
        var pos = 0;
        var k = 0;
        while (true)
        {
            var next = (k + 1).ToString(CultureInfo.InvariantCulture);
            Match? hit = null;
            for (var m = Marker.Match(text, pos); m.Success; m = m.NextMatch())
                if (m.Groups[1].Value == next)
                {
                    hit = m;
                    break;
                }
            if (hit is null || k == 9)
                break;
            k++;
            pos = hit.Index + hit.Length;
        }
        return k >= 2 ? k : 0;
    }

    private static JsonObject Dungeons(JsonObject dg)
    {
        var levels = new JsonObject();
        foreach (var (key, node) in dg["levels"]!.AsObject())
        {
            var lv = node!.AsObject();
            var raw = Convert.FromHexString((string)lv["raw_full_hex"]!);
            var counts = OptionCounts(raw);
            var msgs = new JsonObject();
            foreach (var (mid, m) in lv["messages"]!.AsObject())
            {
                var lines = m!["lines"]!.AsArray().Select(x => (string)x!).ToList();
                var count = counts.GetValueOrDefault(int.Parse(mid, CultureInfo.InvariantCulture));
                if (count < 2)
                    count = MarkedOptions(lines); // a menu a script asks for (no event record names it)
                var (l, o) = count >= 2 ? SplitMenu(lines, count) : (lines, []);
                msgs[mid] = J.O(("lines", J.Strs(l)), ("options", J.Strs(o)));
            }
            levels[key] = J.O(("id", J.Clone(lv["id"])), ("size", raw.Length), ("raw_hex", Py.Hex(raw)),
                ("embedded_text", new JsonArray()), ("messages", msgs));
        }
        return J.O(
            ("_source", $"{Source}: DNG1..DNG8 (side B, 6140 B) [C]"),
            ("_note", "raw_hex = the file's first 2048 bytes (the ST's level size): 0-1739 the Phantasie I level format, " +
                      "byte-equal to the ST's except two cells (D2-049, PENDING), then the start of the Apple's message slots. " +
                      "Messages: the Apple's text (CALL 800, slot 1700 + 40 * id, to its CR); a menu written in the text " +
                      "('1-LEAVE  2-OPEN IT') becomes options when an event record asks for that many choices (record " +
                      "byte 1); otherwise the options are the digits. Wording differs from the ST's in places (D2-050)."),
            ("levels", levels));
    }

    // ---- scrolls ----------------------------------------------------------------------------------------------

    private static JsonObject Scrolls(JsonObject sc)
    {
        var pages = sc["a2_pages"]!.AsObject();
        var list = new JsonArray();
        for (var id = 0; id <= 20; id++)
        {
            var lines = id == 0 ? sc["a2_pluto_speech"]!["lines"]!.AsArray().Select(x => (string)x!).ToList()
                : pages[$"SCROLLL{id}"] is JsonArray a ? a.Select(x => (string)x!).ToList() : [];
            list.Add(J.O(("id", id), ("step", 0), ("lines", J.Strs(lines)),
                ("text", string.Join(" ", lines.Select(A2Text.Strip).Where(s => s.Length > 0)))));
        }
        var map = new JsonObject();
        foreach (var s in sc["scrolls"]!.AsArray())
            map[((int)s!["item_id"]!).ToString(CultureInfo.InvariantCulture)] =
                J.Ints(s["files"]!.AsArray().Select(f => int.Parse(((string)f!)[7..], CultureInfo.InvariantCulture)));
        return J.O(
            ("_source", $"{Source}: SCROLLL1..20 (side A), SCROLLL0 (side B), 1000-B text-screen dumps [C]"),
            ("_layout", "Record n = the screen SCROLLLn shows (24 rows of 40); 0 = Pluto's speech (DUNGON2 4300), 17 is not " +
                        "on the disk (empty). item_records: item i shows SCROLLL(i - 120) and the next files while row 22 " +
                        "starts with '!' (TOWN2 8000-8020)."),
            ("item_records", map),
            ("scrolls", list));
    }

    // ---- towns ------------------------------------------------------------------------------------------------

    private static JsonObject Towns(JsonObject tw, JsonObject ow)
    {
        var townCells = new Dictionary<int, JsonArray>();
        foreach (var (_, node) in ow["regions"]!.AsObject())
        {
            var r = node!.AsObject();
            var feats = new Dictionary<(int, int), int>();
            foreach (var f in r["features"]!.AsArray())
                feats[((int)f!["x"]!, (int)f["y"]!)] = (int)f["arg"]!;
            var tiles = r["tiles"]!.AsArray();
            for (var x = 0; x < 26; x++)
                for (var y = 0; y < 20; y++)
                    if (P2StSource.Unfog((int)tiles[y]![x]!) == 40 && feats.TryGetValue((x, y), out var arg) && arg != 0)
                    {
                        if (!townCells.TryGetValue(arg - 1, out var list))
                            townCells[arg - 1] = list = [];
                        list.Add(J.O(("region", J.Clone(r["region_id"])), ("x", x), ("y", y)));
                    }
        }
        var towns = new JsonArray();
        foreach (var t in tw["towns"]!.AsArray())
        {
            var i = (int)t!["index"]!;
            towns.Add(J.O(("id", J.Clone(t["id"])), ("index", i), ("name", J.Clone(t["name"])), ("price_index", J.Clone(t["price_index"])),
                ("stock", J.Clone(t["stock"])), ("restock_items", J.Clone(t["restock_items"])), ("exit_region", J.Clone(t["exit_region"])),
                ("exit_x", J.Clone(t["exit_x"])), ("exit_y", J.Clone(t["exit_y"])),
                ("map_town_cells", townCells.TryGetValue(i, out var cells) ? cells : new JsonArray())));
        }
        return J.O(
            ("_layout", J.Clone(tw["_layout"])),
            ("_notes", J.Strs([
                "TF2, the Apple's new-game template (TWN21..TWN210 are the played copies) [C]; exit_x / exit_y in the ST's map convention.",
                "map_town_cells: cells with tile 40 whose feature arg - 1 == this town index.",
                "The Apple has ten towns (the ST's unused STARVILLE / OLYMPIA records are not on it). D2-048 (PENDING): " +
                "DEMONIAC's price factor and the SCROLL D / E stock of IPSWICH and FERRON are the Apple's.",
                "Restock: the Apple sets 9 pool items per entry (TOWN2 860), the ST 4; the remaster plays the ST rule (D2-045).",
            ])),
            ("towns", towns));
    }

    // ---- creation tables --------------------------------------------------------------------------------------

    private static JsonObject Creation(ImportContext ctx, JsonObject a2)
    {
        var p1ct = ctx.ReadShipped("phantasie1", "creation-tables.json") ?? throw new InvalidOperationException("shipped phantasie1/creation-tables.json missing");
        var rm = new JsonObject();
        var byRace = a2["race_attribute_modifiers"]!["by_race"]!.AsObject().ToList();
        for (var i = 0; i < byRace.Count && i < RaceKeys.Length; i++)
        {
            var mods = byRace[i].Value!["modifiers"]!.AsObject();
            rm[RaceKeys[i]] = J.O(("modifiers", mods.DeepClone()),
                ("ceilings", J.O(Attrs.Select(s => (s, (JsonNode?)((int)mods[s]! + 8))).ToArray())));
        }
        var spell = (JsonObject)a2["spell_levels"]!["by_class"]!.DeepClone();
        // D2-009 (decided: manual, as in I) — the Apple already has the manual's HALFLING DEX 12 and MONK slot 41;
        // the cells are set from I's shipped tables all the same, as the ST build does.
        var hal = rm["HALFLING"]!;
        var p1Hal = p1ct["race_attribute_modifiers"]!["by_race"]!["HALFLING"]!;
        hal["modifiers"]!["DEX"] = J.Clone(p1Hal["modifiers"]!["DEX"]);
        hal["ceilings"]!["DEX"] = J.Clone(p1Hal["ceilings"]!["DEX"]);
        spell["MONK"]![41] = J.Clone(p1ct["spell_levels"]!["by_class"]!["MONK"]![41]);
        var appleWiz43 = (int)spell["WIZARD"]![43]!;
        spell["WIZARD"]![43] = 13; // D2-015 (user s79: manual)

        var factors = J.O(("_note", "D2-021: the same factors as DOS I (dos_tables.lifespan_factors of GameData/phantasie1); " +
                                    "the Apple's own aging rule is not compared [?]."));
        foreach (var (k, v) in p1ct["dos_tables"]!["lifespan_factors"]!.AsObject())
            if (!k.StartsWith('_'))
                factors[k] = v!.DeepClone();
        return J.O(
            ("_schema", "phantasie.creation-tables/v1 (shape of GameData/phantasie1/creation-tables.json, raw values)"),
            ("_source", $"{Source}: TINFO (side A, 1730 B at $08FC; FORMAT.md 3.4) [C]"),
            ("_decisions", "D2-009 (user: manual, as in I): HALFLING DEX modifier 12 / ceiling 20 and MONK spell 41 not " +
                           "learnable at level 7 (the Apple's own cells already say so). D2-015 (user s79: manual): WIZARD " +
                           "spell 43 learnable at level 13. D2-049 (PENDING): MONK level 10 skill 6 and PRIEST level 15 skill " +
                           "8 are the Apple's (0). Skill coefficients are one byte on the Apple (the ST stores u16)."),
            ("_lifespan", "lifespan_bands = the manual's bands +-10 %, as I (D2-021, user s80: manual); copied from " +
                          "GameData/phantasie1/creation-tables.json."),
            ("hp_factors", J.O(("layout", "class * 15 + level"), ("by_class", a2["hp_factors"]!["by_class"]!.DeepClone()))),
            ("mp_ceiling", J.O(("layout", "class * 15 + level"), ("by_class", a2["mp_ceiling"]!["by_class"]!.DeepClone()))),
            ("skill_coefficients", J.O(("layout", "class * 150 + level * 10 + skill (u8)"),
                ("skill_order", a2["skill_coefficients"]!["skill_order"]!.DeepClone()),
                ("by_class_level", a2["skill_coefficients"]!["by_class_level"]!.DeepClone()))),
            ("class_attribute_modifiers", J.O(("by_class", a2["class_attribute_modifiers"]!["by_class"]!.DeepClone()))),
            ("race_attribute_modifiers", J.O(("note", "15 races x 5 bytes (TINFO 275). ceiling = modifier + 8 as in Phantasie I [~]"),
                ("by_race", rm))),
            ("spell_levels", J.O(("layout", "class * 60 + spell slot (u8); 0 = never"), ("by_class", spell))),
            ("lifespan_bands", J.Clone(p1ct["lifespan_bands"])),
            ("dos_tables", J.O(
                ("_source", "The Apple's own cells that the tables above replace by the manual, played by the rules option " +
                            "p2.wizard-resurrection = st (the option's original value) and p2.lifespans = st."),
                ("spell_levels", J.O(("_note", "D2-015: TINFO 380 WIZARD spell slot 43 as on the Apple."),
                    ("WIZARD", J.O(("43", appleWiz43))))),
                ("lifespan_factors", factors))));
    }

    // ---- elementals and manifest ------------------------------------------------------------------------------

    private static JsonObject Elemental(string name, int hp, int attack, int parry, int guild, int dmg, int armor, int[] spells) => J.O(
        ("name", name), ("intelligence", 25), ("mp_ceiling", 20), ("magic_max", 20), ("magic_cur", 20), ("hp_max", hp), ("hp_cur", hp),
        ("status", 1), ("attack", attack), ("parry", parry), ("level", 20), ("guild_number", guild), ("weapon_damage_b", dmg),
        ("shield_soak", 100), ("armor_soak", armor), ("race", 15), ("birth_race", 16), ("class", 3), ("known_spells", J.Ints(spells)));

    private static JsonObject Elementals() => J.O(
        ("_source", "Remaster data: the Apple II disks hold no elementals [?]. The numbers are Phantasie I's and the Atari ST's " +
                    "Guild template's (all three versions agree); the names are the remaster's."),
        ("elementals", J.A([
            Elemental("FIRE ELEMENTAL", 100, 200, 60, 23, 40, 7, [4, 5, 6, 7]),
            Elemental("WATER ELEMENTAL", 150, 150, 80, 24, 30, 14, [20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31]),
            Elemental("EARTH ELEMENTAL", 200, 100, 100, 25, 20, 21, [12, 13, 14, 15, 16, 17, 18, 19]),
        ])));

    private static JsonObject Manifest(IReadOnlyDictionary<string, JsonObject> built)
    {
        int Count(string file, string key) => built[file][key] switch
        {
            JsonArray a => a.Count,
            JsonObject o => o.Count,
            _ => throw new InvalidOperationException(key),
        };
        return J.O(
            ("_schema", "phantasie.game/v1"),
            ("_source", "Built by the importer (p2-apple2) from " + Source),
            ("_note", "II's tables from the Apple II copy (no messages.json: the Apple keeps dungeon text in the levels). Its " +
                      "graphics pack is graphics/apple2.json."),
            ("game", "p2"),
            ("platform", "apple2"),
            ("counts", J.O(
                ("monsters", Count("monsters.json", "monsters")),
                ("items", Count("items.json", "items")),
                ("spells", Count("spells.json", "spells")),
                ("classes", Count("names.json", "classes")),
                ("races", Count("names.json", "races")),
                ("overworld_regions", Count("overworld.json", "regions")),
                ("dungeon_levels", Count("dungeons.json", "levels")),
                ("scrolls", Count("scrolls.json", "scrolls")),
                ("towns", Count("towns.json", "towns")),
                ("elementals", Count("elementals.json", "elementals")))),
            ("spell_cost_rule", null),
            ("_spell_cost_rule", "As the ST build: (slot % 4) + 1 for every spell, filled in spells.json."));
    }
}
