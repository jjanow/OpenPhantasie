using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Pipelines.P2Apple2;
using OpenPhantasie.Importer.Pipelines.P3Dos;
using OpenPhantasie.Importer.Profiles;
using static OpenPhantasie.Importer.Pipelines.P3Dos.P3Extract;

namespace OpenPhantasie.Importer.Pipelines.P3Apple2;

/// <summary>
/// Phantasie III's game tables from the Apple II disks alone (s212; port of <c>tools/scripts/extract_a2_p3_tables.py</c>),
/// in the shapes <see cref="P3Extract"/> gives for DOS III, so the same <see cref="P3Build"/> turns them into GameData.
/// Layouts, program lines and every Apple ≠ DOS cell: <c>reverse-engineering/apple2/phantasie3/FORMAT.md</c>.
/// <para><see cref="Extract"/> gives the Apple's own values (those it does not hold are <c>null</c>, never DOS's; text
/// is the Apple's own, the hi-res window font decoded); <see cref="Write"/> writes the tables with DOS III's value in
/// every number cell where they differ, from the shipped <c>phantasie3/apple2-import.json</c> (the rule for an Apple-only
/// III: DOS's values, as II's Apple takes the ST's and III's Amiga DOS's), keeping the Apple's text. When DOS III's
/// tables are imported too (this run or earlier) they win and this writes nothing.</para>
/// <para>Reads side A (boot: <c>TOWN3</c>, <c>TF3</c>, <c>TINFO</c>, <c>OT&lt;n&gt;</c>, <c>MN3&lt;n&gt;</c>, <c>SC&lt;n&gt;</c>)
/// and side B (utilities / the Dungeon disk: <c>DNG&lt;n&gt;</c>) of the v3.0 originals (<c>.woz</c>, decoded by
/// <see cref="WozImage"/>) or the v1.0 [cr] sides.</para>
/// </summary>
public sealed class P3Apple2Tables
{
    /// <summary>Starts every <c>_source</c> these files carry (the <see cref="P3Build"/> label replaced), so a later
    /// run can tell them from DOS III's.</summary>
    public const string Label = "Phantasie III, Apple II SSI 1987 (reverse-engineering/apple2/phantasie3/FORMAT.md)";

    private static readonly string[] AppleClass = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"];

    /// <summary>III's race numbering (names.json: 5 the 'Random' menu entry, 16 Elemental, 17 Undead) → the Apple's
    /// race number (TOWN3 270, Phantasie I's order); the Apple has neither Random nor Elemental.</summary>
    private static readonly int?[] IiiRaceFromApple = [0, 1, 2, 4, 3, null, 8, 12, 10, 13, 14, 7, 11, 5, 6, 9, null, 15];

    /// <summary>III's social classes (4 Darklord, 5 Superhero) → the Apple's record value (TOWN3 295).</summary>
    private static readonly int[] IiiSocialFromApple = [0, 1, 2, 3, 5, 4];

    /// <summary>III's 22 terrain numbers → the Apple's TR$ index (STARTER 120) of the same ground [~].</summary>
    private static readonly int?[] IiiTerrainFromApple =
        [0, 1, 2, 3, 4, 5, 6, 14, 8, 9, 10, 12, null, 7, 13, null, 11, null, null, null, null, null];

    private static readonly string[] AppleTerrain = ["GRASS", "FOREST", "HILLS", "MOUNTAINS", "BUILDINGS", "DUNGEON", "PATHWAY",
        "BRIDGE", "LAVA", "DESERT", "WATER", "MIST", "THE=LIGHT=PLANE", "THE=DARK=PLANE", "NETHERWORLD====LEVEL=THREE"];

    /// <summary>III dungeon n (1..11) and inn n (1..8) → the wilderness section (<c>OT&lt;s&gt;</c>) holding it.</summary>
    private static readonly int[] DungeonSection = [5, 3, 15, 14, 13, 11, 23, 21, 41, 29, 31];

    private static readonly int[] InnSection = [8, 10, 15, 13, 7, 6, 18, 21];

    private static readonly int[] Sections = [.. Enumerable.Range(1, 25), 29, 31, 41, 42, 43];

    /// <summary>III's place-list order (DOS's place test in code order), by (type, number).</summary>
    private static readonly (string Type, int Number)[] PlaceOrder =
    [
        ("town", 17), ("dungeon", 9), ("town", 11), ("town", 12), ("town", 13), ("dungeon", 1), ("dungeon", 2), ("inn", 1),
        ("inn", 2), ("inn", 6), ("town", 3), ("town", 8), ("town", 14), ("dungeon", 3), ("dungeon", 6), ("inn", 3), ("inn", 4),
        ("inn", 5), ("town", 1), ("town", 2), ("town", 4), ("town", 5), ("town", 9), ("dungeon", 4), ("dungeon", 5), ("inn", 7),
        ("town", 6), ("town", 7), ("town", 10), ("dungeon", 7), ("dungeon", 8), ("inn", 8), ("town", 15), ("dungeon", 10),
        ("town", 16), ("dungeon", 11),
    ];

    /// <summary>Text files in DOS's record order: SC1..SC20 without SC6 (Scroll IV, the map drawn in characters).</summary>
    private static readonly int[] ScrollFiles = [1, 2, 3, 4, 5, .. Enumerable.Range(7, 14)];

    private static readonly string[] MonFields =
    [
        "name", "armor", "damage", "level", "xp", "special_chance", "special_0", "special_1", "special_2", "hp_base",
        "combat_rating", "companion_0", "companion_1", "reaction_modifier", "next_group_chance", "defense", "gold",
        "group_max", "treasure_0", "treasure_1", "attack_power", "shape",
    ];

    private readonly Container _a, _b;
    private readonly byte[] _tinfo, _info, _towns;
    private readonly Dictionary<int, byte[]> _sections = new();

    public P3Apple2Tables(FileSet files)
    {
        _a = files.Containers.FirstOrDefault(c => Has(c, "TOWN3") && Has(c, "TF3"))
             ?? throw new FileNotFoundException("Phantasie III Apple II side A (TOWN3, TF3) not found");
        _b = files.Containers.FirstOrDefault(c => Has(c, "DUNGON3") && Has(c, "DNG1"))
             ?? throw new FileNotFoundException("Phantasie III Apple II side B (DUNGON3, DNG1) not found");
        _tinfo = A("TINFO");
        _info = A("INFO");
        _towns = A("TF3");
        foreach (var s in Sections)
            _sections[s] = A($"OT{s}");
    }

    private static bool Has(Container c, string name) => c.Files.Any(f => f.Path == name);

    private static byte[] Read(Container c, string name) =>
        (c.Files.FirstOrDefault(f => f.Path == name) ?? throw new FileNotFoundException($"{name} not found on {c.Description}", name)).Data;

    private byte[] A(string name) => Read(_a, name);

    private byte[] B(string name) => Read(_b, name);

    // ---- text ---------------------------------------------------------------------------------------------------

    private static string Ascii7(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var b in raw)
            sb.Append((char)(b & 0x7F));
        return sb.ToString();
    }

    /// <summary>The hi-res window font (<c>IMPTEXT</c>, glyph k = character 37 + k): these characters draw as another.</summary>
    public static string Font(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c switch { '=' => ' ', ':' => '-', '@' => ':', '<' => ',', '?' => ')', '>' => '?', ';' => '"', _ => c });
        return sb.ToString();
    }

    private static string Printable(string s) => new(s.Where(c => c is >= ' ' and < '\x7f').ToArray());

    /// <summary>A string laid out for a <paramref name="width"/>-column hi-res window, as one line: the font decoded,
    /// each window row stripped, a row ending in '-' joined to the next without a space.</summary>
    public static string WindowText(string s, int width, bool decodeFont = true)
    {
        var output = "";
        for (var i = 0; i < s.Length; i += width)
        {
            var cut = s.Substring(i, Math.Min(width, s.Length - i));
            var row = P3Exe.PyStrip(decodeFont ? Font(cut) : cut);
            if (row.Length == 0)
                continue;
            if (output.EndsWith('-'))
                output = output[..^1];
            else if (output.Length > 0)
                output += " ";
            output += row;
        }
        return output;
    }

    /// <summary>One line word-wrapped into <paramref name="width"/>-column rows, each but the last padded with spaces
    /// (DOS's layout, so P3Build's 38-column slices are the rows) [~]; a word longer than a row is cut.</summary>
    public static string Layout(string text, int width)
    {
        var rows = new List<string>();
        var cur = "";
        foreach (var w in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = w;
            while (word.Length > width)
            {
                if (cur.Length > 0)
                {
                    rows.Add(cur);
                    cur = "";
                }
                rows.Add(word[..width]);
                word = word[width..];
            }
            if (cur.Length == 0)
                cur = word;
            else if (cur.Length + 1 + word.Length <= width)
                cur += " " + word;
            else
            {
                rows.Add(cur);
                cur = word;
            }
        }
        if (cur.Length > 0 || rows.Count == 0)
            rows.Add(cur);
        return string.Concat(rows.Take(rows.Count - 1).Select(r => r.PadRight(width))) + rows[^1];
    }

    private static string CrString(byte[] raw, int at)
    {
        var end = Array.IndexOf(raw, (byte)0x0D, at);
        return Printable(Ascii7(raw.AsSpan(at, (end < 0 ? raw.Length : end) - at)));
    }

    /// <summary>A side-A program's lines by number. The lines are walked in sequence (never by absolute link: a program
    /// is linked for its own address); the catalog length can stop short of the end link, so a cut last line ends it.</summary>
    private Dictionary<int, string> Program(string name)
    {
        var data = A(name);
        var output = new Dictionary<int, string>();
        for (var p = 0; p + 4 <= data.Length;)
        {
            int next = data[p] | data[p + 1] << 8, line = data[p + 2] | data[p + 3] << 8;
            var end = Array.IndexOf(data, (byte)0, p + 4);
            if (next < 0x100 || end < 0)
                break;
            output[line] = A2Text.Detok(data.AsSpan(p + 4, end - p - 4));
            p = end + 1;
        }
        return output;
    }

    private static List<string> DataItems(string line) =>
        line[(line.IndexOf(" DATA ", StringComparison.Ordinal) + 6)..].Split(',').Select(x => x.Trim()).ToList();

    private static Dictionary<int, string> Assignments(string line, string variable)
    {
        var output = new Dictionary<int, string>();
        foreach (var raw in line.Split(':'))
        {
            var part = raw.Trim();
            if (!part.StartsWith(variable + "(", StringComparison.Ordinal) || !part.Contains('=') || !part.Contains('"'))
                continue;
            var k = int.Parse(part[(variable.Length + 1)..part.IndexOf(')')], System.Globalization.CultureInfo.InvariantCulture);
            output[k] = part.Split('"')[1];
        }
        return output;
    }

    private static JsonArray Lst(IEnumerable<string?> names) =>
        Arr(names.Select((x, i) => (JsonNode)new JsonObject { ["index"] = i, ["name"] = x }));

    private static JsonArray Bytes(ReadOnlySpan<byte> raw) => Ints(raw.ToArray().Select(b => (int)b));

    private static int AppleClassOf(string iiiClass) => Array.IndexOf(AppleClass, iiiClass.ToUpperInvariant());

    // ---- monsters -----------------------------------------------------------------------------------------------

    public JsonObject Monsters()
    {
        var list = new JsonArray();
        var i = 0;
        for (var f = 0; f < 10; f++)
        {
            var raw = A($"MN3{f}");
            if (!raw.AsSpan().SequenceEqual(B($"MN3{f}")))
                throw new InvalidDataException($"MN3{f}: side A and side B differ");
            var nul = Array.IndexOf(raw, (byte)0);
            var fields = Ascii7(raw.AsSpan(0, nul < 0 ? raw.Length : nul)).Split('\r');
            for (var k = 0; k < 8; k++, i++)
            {
                if (fields.Length < 22 * (k + 1))
                    throw new InvalidDataException($"MN3{f}: record {k} short");
                var v = MonFields.Select((name, j) => (name, fields[22 * k + j])).ToDictionary(x => x.name, x => x.Item2);
                int N(string key) => int.Parse(v[key], System.Globalization.CultureInfo.InvariantCulture);
                list.Add(new JsonObject
                {
                    ["index"] = i, ["name"] = P3Exe.PyStrip(Printable(Font(v["name"]))),
                    ["group_max"] = N("group_max"), ["attack_power_div10"] = null, ["attack_power"] = N("attack_power"),
                    ["damage"] = N("damage"), ["armor"] = N("armor"), ["defense"] = N("defense"), ["level"] = N("level"),
                    ["special_chance"] = N("special_chance"),
                    ["specials"] = Ints([N("special_0"), N("special_1"), N("special_2")]),
                    ["combat_rating"] = N("combat_rating"), ["hp_base"] = N("hp_base"), ["reaction_modifier"] = N("reaction_modifier"),
                    ["byte_27"] = null, ["next_group_chance"] = N("next_group_chance"),
                    ["companions"] = Ints([N("companion_0"), N("companion_1")]),
                    ["treasure_items"] = Ints([N("treasure_0"), N("treasure_1")]),
                    ["byte_33"] = null, ["gold"] = N("gold"), ["xp"] = N("xp"), ["shape"] = N("shape"),
                });
            }
        }
        return new JsonObject
        {
            ["_source"] = "MN30..MN39 (side A = side B), 8 text records each, 22 CR-ended fields read by OUTDOORS3 / DUNGON3 " +
                          "line 910 [C]; monster m = record m mod 8 of MN3<m div 8>. Values as the Apple holds them (full " +
                          "numbers, no DOS byte encoding); attack_power is the whole value (DOS stores / 10); byte_27 and " +
                          "byte_33 have no Apple field (shape = the Apple's picture number)",
            ["monsters"] = list,
        };
    }

    // ---- items --------------------------------------------------------------------------------------------------

    private static string ItemKindOf(int i) => i switch
    {
        <= 20 => "shield", <= 40 => "armor", <= 60 => "bow", <= 100 => "weapon", <= 110 => "healing_potion",
        <= 120 => "magic_potion", <= 140 => "scroll", _ => "treasure",
    };

    /// <summary>TOWN3 1150-1190: (points, requirement) of item y (1..100).</summary>
    private static (int Points, int Requirement) ItemPoints(int y) => y switch
    {
        < 21 => (y, y),
        < 31 => (y - 20, y * 2 - 40),
        < 41 => (y - 29, y * 2 - 64),
        < 61 => (y - 40, y * 2 - 82),
        < 81 => ((int)(y / 1.36 - 43), y * 3 - 180),
        _ => ((int)(y * 1.2 - 95), y * 3 - 240),
    };

    private string TownName(int t) => P3Exe.PyStrip(Ascii7(_towns.AsSpan(250 * (t - 1) + 230, 20)).TrimEnd('\x15'));

    public JsonObject Items()
    {
        var names = Ascii7(A("ITEM")).Split('\r');
        var list = new JsonArray
        {
            new JsonObject { ["id"] = 0, ["name"] = null, ["kind"] = null, ["value"] = null, ["points"] = null, ["requirement"] = null },
        };
        for (var i = 1; i <= 180; i++)
        {
            int f = _tinfo[89 + i];
            var it = new JsonObject
            {
                ["id"] = i, ["name"] = names[i - 1], ["kind"] = ItemKindOf(i), ["value"] = (int)(f * f / 5.0 + 1), ["value_factor"] = f,
            };
            if (i <= 100)
            {
                var (p, r) = ItemPoints(i);
                it["points"] = p;
                it["requirement"] = r;
            }
            list.Add(it);
        }
        return new JsonObject
        {
            ["_source"] = "names ITEM (180 CR-ended, TOWN3 230); value = INT(f^2 / 5 + 1), f = TINFO[89 + id] (the armory price " +
                          "INT(f^2 / 5 * CF + 1) at price factor 100, TOWN3 1117) [C]; points / requirement: TOWN3 1150-1190 " +
                          "[C]; town price index = the town record's byte 226 (TOWN3 825)",
            ["town_price_index"] = Arr(Enumerable.Range(0, 18).Select(t => (JsonNode)new JsonObject
            {
                ["town"] = t, ["name"] = t == 0 ? null : TownName(t), ["index"] = t == 0 ? null : _towns[250 * (t - 1) + 226],
            })),
            ["items"] = list,
        };
    }

    public static JsonObject Spells() => new()
    {
        ["_source"] = "the Apple has no spell names: spells are learnt and cast by number 1-60 (TOWN3 3060, 6882) [C]",
        ["spells"] = Arr(Enumerable.Range(1, 57).Select(n => (JsonNode)new JsonObject { ["number"] = n, ["name"] = null })),
    };

    // ---- names --------------------------------------------------------------------------------------------------

    public JsonObject Names()
    {
        var town = Program("TOWN3");
        var starter = Program("STARTER");
        var classes = DataItems(town[260]);
        var races = DataItems(town[270]).Take(16).ToList();
        var skills = DataItems(town[130]);
        var social = Assignments(town[295], "SC$");
        var origins = Assignments(town[290], "H$");
        var body = Assignments(town[290], "S$");
        var statusShort = Assignments(starter[450], "ST$");
        var classShort = DataItems(starter[430]);
        if (!DataItems(starter[120]).SequenceEqual(AppleTerrain))
            throw new InvalidDataException("STARTER 120: terrain names changed");
        var terrain = AppleTerrain.Select(Font).ToArray();
        return new JsonObject
        {
            ["_source"] = "TOWN3 / STARTER program text (DATA lines and string assignments) and the OT section texts [C]; the " +
                          "Apple's own wording; null where the Apple has no such name",
            ["combat_actions"] = null,
            ["status_short"] = Lst(new[] { 0, 4, 2, 1, 3 }.Select(k => statusShort[k])),
            ["class_short"] = Lst(P3Extract.Classes.Select(c => classShort[AppleClassOf(c)])),
            ["rank_labels"] = null,
            ["places"] = Lst(DungeonSection.Select(s => WindowText(CrString(_sections[s], 226), 15))),
            ["inns"] = Lst(InnSection.Select(s => "THE " + WindowText(CrString(_sections[s], 171), 8) + " INN")),
            ["terrain"] = Lst(IiiTerrainFromApple.Select(a => a is { } k ? terrain[k] : null)),
            ["skills"] = Lst(new[] { 0, 1, 9, 5, 3, 4, 2, 7, 6 }.Select(k => skills[k])),
            ["races"] = Lst(IiiRaceFromApple.Select(a => a is { } k ? races[k] : null)),
            ["classes"] = Lst(P3Extract.Classes.Select(c => classes[AppleClassOf(c)])),
            ["towns"] = Lst(Enumerable.Range(1, 14).Select(TownName)),
            ["origins"] = Lst(new[] { 15, 16, 17 }.Select(TownName).Concat(Enumerable.Range(0, 6).Select(k => origins[k]))),
            ["status"] = Lst(["DEAD", "OKAY", null, null, null]),
            ["social_classes"] = Lst(IiiSocialFromApple.Select(a => social[a])),
            ["body_states"] = Lst([body[1], body[2], null, body[3]]),
            ["transfer_class_map_I_to_III"] = Ints(AppleClass.Select(c =>
                Array.FindIndex(P3Extract.Classes, x => x.Equals(c, StringComparison.OrdinalIgnoreCase)))),
            ["transfer_race_map_I_to_III"] = Ints(Enumerable.Range(0, 16).Select(a => Array.IndexOf(IiiRaceFromApple, a))),
            ["_maps"] = "BACKUP 4000-5015 copies Phantasie I / II records as they are: the I -> III maps are the identity in " +
                        "the Apple's numbering, given here in III's numbering",
        };
    }

    // ---- towns --------------------------------------------------------------------------------------------------

    public JsonObject TownsTable()
    {
        var list = new JsonArray
        {
            new JsonObject { ["town"] = 0, ["name"] = null, ["price_index"] = null, ["kinds"] = null, ["stock"] = null, ["stocked"] = new JsonArray() },
        };
        for (var t = 1; t <= 17; t++)
        {
            var rec = _towns.AsSpan(250 * (t - 1), 250);
            var stock = new int[181];
            for (var i = 1; i <= 180; i++)
                stock[i] = rec[i - 1];
            list.Add(new JsonObject
            {
                ["town"] = t, ["name"] = TownName(t), ["price_index"] = (int)rec[226], ["kinds"] = null, ["stock"] = Ints(stock),
                ["restock_pool"] = Bytes(rec[180..226]),
                ["exit"] = new JsonObject { ["section"] = (int)rec[227], ["x"] = (int)rec[228], ["y"] = (int)rec[229] },
                ["stocked"] = Arr(Enumerable.Range(0, 181).Where(i => stock[i] != 0)
                    .Select(i => (JsonNode)new JsonObject { ["id"] = i, ["count"] = stock[i] })),
            });
        }
        return new JsonObject
        {
            ["_source"] = "TF3 (the new-game template; 17 x 250 B, record t = town t, file offset k = address LA + 1 + k): stock " +
                          "of item id at id - 1 (TOWN3 1115, 1290, 8450), restock pool 180..225 (TOWN3 860), price factor 226 " +
                          "(825), exit section / x / y 227..229 (6020), name 230..249 padded with 0x15 (810) [C]",
            ["_layout"] = "stock[id] for item id 0..180; no 'kinds' byte on the Apple",
            ["towns"] = list,
        };
    }

    // ---- scrolls ------------------------------------------------------------------------------------------------

    private static List<string> ScrollInner(List<string> lines)
    {
        var inner = lines.Skip(1).Take(22).Select(l => P3Exe.PyRStrip(l.PadRight(40)[1..39])).ToList();
        while (inner.Count > 0 && inner[^1].Length == 0)
            inner.RemoveAt(inner.Count - 1);
        return inner;
    }

    public JsonObject Scrolls()
    {
        var records = new JsonArray();
        var cont = new Dictionary<int, bool>();
        for (var n = 1; n <= 20; n++)
        {
            var raw = A($"SC{n}");
            cont[n] = raw[1872 - 1024] == 161; // TOWN3 8020: row 22 column 0 is '!' -> the next file follows
            if (n == 6)
                continue;
            var inner = ScrollInner(A2Text.TextPage(raw));
            records.Add(new JsonObject
            {
                ["record"] = Array.IndexOf(ScrollFiles, n), ["file"] = $"SC{n}", ["length"] = null,
                ["text"] = string.Join("\n", inner), ["lines"] = Strs(inner),
            });
        }
        var items = new JsonArray();
        for (var item = 121; item <= 140; item++)
        {
            var files = new List<int>();
            for (var f = item - 120; ; f++)
            {
                files.Add(f);
                if (!cont[f] || f >= 20)
                    break;
            }
            items.Add(new JsonObject
            {
                ["item"] = item, ["name"] = null,
                ["records"] = Ints(files.Where(x => x != 6).Select(x => Array.IndexOf(ScrollFiles, x))),
            });
        }
        return new JsonObject
        {
            ["_source"] = "SC1..SC20 (side A): 1000-B dumps of the text page $400, shown as is (TOWN3 8010); item i (121..140) " +
                          "shows SC(i - 120) and the next file while row 22 column 0 is '!' (8020) [C]. Records in DOS's " +
                          "numbering: SC6 (Scroll IV, item 126: the map drawn in characters, DOS's picture) is not a text " +
                          "record. text = the 38 columns inside the frame, rows 1..22, joined by newlines",
            ["scroll_iv_lines"] = Strs(A2Text.TextPage(A("SC6"))),
            ["records"] = records,
            ["items"] = items,
        };
    }

    // ---- world --------------------------------------------------------------------------------------------------

    private JsonArray PlaneGrid(int first, int cols, int rows)
    {
        var grid = new JsonArray();
        for (var y = 0; y < 15 * rows; y++)
        {
            var row = new int[10 * cols];
            for (var c = 0; c < cols; c++)
                for (var x = 0; x < 10; x++)
                    row[10 * c + x] = _sections[first + c + 5 * (y / 15)][10 * (y % 15) + x];
            grid.Add(Ints(row));
        }
        return grid;
    }

    public JsonObject World()
    {
        JsonObject Plane(int id, string key, string source, int w, int h, JsonArray tiles) => new()
        {
            ["plane"] = id, ["key"] = key, ["source"] = source, ["width"] = w, ["height"] = h, ["arrival"] = null, ["tiles"] = tiles,
        };
        var nether = new JsonArray();
        for (var y = 0; y < 15; y++)
            nether.Add(Ints(new[] { 41, 42, 43 }.SelectMany(s => _sections[s].Skip(10 * y).Take(10).Select(b => (int)b))));
        var planes = new JsonArray
        {
            Plane(0, "scandor", "OT1..OT25 (5 x 5 sections of 10 x 15)", 50, 75, PlaneGrid(1, 5, 5)),
            Plane(1, "light", "OT29", 10, 15, PlaneGrid(29, 1, 1)),
            Plane(2, "dark", "OT31", 10, 15, PlaneGrid(31, 1, 1)),
            Plane(4, "netherworld", "OT41..OT43", 30, 15, nether),
        };

        var places = new List<JsonObject>();
        foreach (var sec in Sections)
        {
            var raw = _sections[sec];
            var (plane, ox, oy) = sec switch
            {
                <= 25 => ("scandor", 10 * ((sec - 1) % 5), 15 * ((sec - 1) / 5)),
                29 => ("light", 0, 0),
                31 => ("dark", 0, 0),
                _ => ("netherworld", 10 * (sec - 41), 0),
            };
            for (var y = 0; y < 15; y++)
                for (var x = 0; x < 10; x++)
                {
                    int b = raw[10 * y + x];
                    JsonObject P(string type, int number, string name) => new()
                    {
                        ["plane"] = plane, ["x"] = ox + x, ["y"] = oy + y, ["type"] = type, ["number"] = number, ["name"] = name,
                        ["tile"] = b, ["section"] = sec,
                    };
                    if (b == 40)
                        places.Add(P("town", raw[150], WindowText(CrString(raw, 154), 8)));
                    else if (b == 41)
                        places.Add(P("inn", Array.IndexOf(InnSection, sec) + 1, WindowText(CrString(raw, 171), 8)));
                    else if (b / 10 == 5)
                    {
                        var p = P("dungeon", Array.IndexOf(DungeonSection, sec) + 1, WindowText(CrString(raw, 226), 15));
                        p["dungeon_file"] = (int)raw[151];
                        p["entrance"] = new JsonObject { ["x"] = (int)raw[152], ["y"] = (int)raw[153] };
                        places.Add(p);
                    }
                }
        }
        places = places.OrderBy(p => Array.IndexOf(PlaceOrder, ((string)p["type"]!, (int)p["number"]!))).ToList();
        if (places.Count != PlaceOrder.Length)
            throw new InvalidDataException($"OT sections: {places.Count} places, expected {PlaceOrder.Length}");

        // TOWN3 710 / 6020: a new game starts in Pendragon; leaving puts the party on the town record's exit square.
        int sec0 = _towns[227], sx = _towns[228], sy = _towns[229];
        var terrainNames = IiiTerrainFromApple.Select(a => a is { } k ? Font(AppleTerrain[k]) : null).ToList();
        return new JsonObject
        {
            ["_source"] = "OT<n> sections (side A), 10 x 15 tile bytes at LA + 10y + x (OUTDOORS3 210), then the section's " +
                          "town / dungeon / inn and texts [C]; tile = 10 * terrain + monster level (5070)",
            ["planes"] = planes,
            ["start"] = new JsonObject { ["plane"] = 0, ["x"] = 10 * ((sec0 - 1) % 5) + sx, ["y"] = 15 * ((sec0 - 1) / 5) + sy },
            ["places"] = Arr(places),
            ["terrain"] = new JsonObject
            {
                ["_note"] = "Apple tile byte b: terrain b // 10 (STARTER 120 TR$, here in III's numbering), level b % 10; 40 " +
                            "town, 41 inn, 50..59 a dungeon; 100 ocean / River Styx (blocked, 5270); no tile-id ranges as DOS's",
                ["names"] = new JsonArray(terrainNames.Select(n => (JsonNode?)n).ToArray()),
                ["ranges"] = Arr(Enumerable.Range(0, 12).Select(t => (JsonNode)new JsonObject
                {
                    ["first"] = 10 * t, ["last"] = 10 * t + 9, ["terrain"] = Array.IndexOf(IiiTerrainFromApple, t),
                })),
                ["other"] = null, ["netherworld_overrides"] = null, ["blocked_from"] = null, ["no_encounter"] = null,
            },
        };
    }

    // ---- dungeons -----------------------------------------------------------------------------------------------

    private const int DngPtr = 0x593, DngTextBase = 0x800, DngW = 24, DngH = 30;

    private static (int Slots, JsonArray Texts) DungeonTexts(byte[] rec)
    {
        var ptrs = new List<int>();
        int o = DngPtr, end = rec.Length;
        while (o + 1 < end)
        {
            var p = rec[o] << 8 | rec[o + 1];
            if (p != 0)
                end = Math.Min(end, p - DngTextBase);
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
            var cr = Array.IndexOf(rec, (byte)0x0D, at);
            var raw = Ascii7(rec.AsSpan(at, cr - at));
            // Window texts (with the font's '=' spaces) are laid out for the 15-column window (DUNGON3 6000 / 6310
            // via 800); the others print on the 40-column text screen (8210 via 7300).
            texts.Add(new JsonObject
            {
                ["index"] = i, ["offset"] = P3Exe.Hx(at),
                ["text"] = Layout(raw.Contains('=') ? WindowText(raw, 15) : WindowText(raw, 40, decodeFont: false), 38),
            });
        }
        return (ptrs.Count, texts);
    }

    public JsonObject Dungeons(JsonObject names)
    {
        var placeNames = names["places"]!.AsArray();
        var list = new JsonArray();
        for (var n = 1; n <= DungeonSection.Length; n++)
        {
            int f = _sections[DungeonSection[n - 1]][151];
            var rec = B($"DNG{f}");
            var cells = new JsonArray();
            for (var y = 0; y < DngH; y++)
                cells.Add(Bytes(rec.AsSpan(y * DngW, DngW)));
            var squares = new JsonArray();
            for (var k = 0; k < 35; k++)
            {
                var b = rec.AsSpan(0x2E4 + 5 * k, 5);
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
            var (slots, texts) = DungeonTexts(rec);
            list.Add(new JsonObject
            {
                ["dungeon"] = n, ["record"] = f - 1, ["letter"] = ((char)('A' + f - 1)).ToString(), ["file"] = $"DNG{f}",
                ["name"] = placeNames[n - 1]!["name"]!.DeepClone(), ["level"] = (int)rec[0x2D0], ["width"] = DngW, ["height"] = DngH,
                ["cells"] = cells, ["forced_fights"] = Bytes(rec.AsSpan(0x2D1, 10)), ["exits"] = exits, ["event_squares"] = squares,
                ["event_program_hex"] = P3Exe.Hex(rec.AsSpan(0x393, 0x200)), ["text_slots"] = slots, ["texts"] = texts,
                ["raw_hex"] = P3Exe.Hex(rec),
            });
        }
        return new JsonObject
        {
            ["_source"] = "DNG1..DNG10 (side B), one record each in DOS DNGS.DAT's layout (map 24 x 30, tables to 0x593, text " +
                          "pointers, texts) [C]: 4132 B (the text area is shorter). DUNGON3 / DNGLD3: dungeon file = the " +
                          "section's OT byte 151; dungeons 1 and 8 both use DNG10",
            ["dungeons"] = list,
        };
    }

    // ---- rule tables --------------------------------------------------------------------------------------------

    public JsonObject RuleTables()
    {
        JsonObject ByClass(int @base, int stride, int n)
        {
            var o = new JsonObject();
            foreach (var c in P3Extract.Classes)
                o[c] = Bytes(_tinfo.AsSpan(@base + stride * AppleClassOf(c), n));
            return o;
        }
        (string Skill, int Slot)[] slots =
        [
            ("attack", 0), ("parry", 1), ("swim", 9), ("listen", 5), ("find_trap", 3), ("disarm_trap", 4), ("find_item", 2), ("pick_lock", 7),
        ];
        var skills = new JsonObject();
        var bases = new JsonObject();
        foreach (var (skill, slot) in slots)
        {
            var o = new JsonObject();
            foreach (var c in P3Extract.Classes)
                o[c] = Ints(Enumerable.Range(0, 15).Select(lv => (int)_tinfo[830 + 150 * AppleClassOf(c) + 10 * lv + slot]));
            skills[skill] = o;
            bases[skill] = $"slot {slot}";
        }
        var levels = new JsonObject();
        var rows = new JsonObject();
        foreach (var c in P3Extract.Classes)
        {
            var lv = _tinfo.AsSpan(380 + 60 * AppleClassOf(c), 60).ToArray();
            levels[c] = Ints(lv.Select(b => (int)b));
            var per = new SortedDictionary<int, List<int>>();
            for (var sp = 1; sp <= 57; sp++)
                if (lv[sp - 1] != 0)
                {
                    if (!per.TryGetValue(lv[sp - 1], out var l))
                        per[lv[sp - 1]] = l = [];
                    l.Add(sp);
                }
            var depth = per.Count == 0 ? 0 : per.Values.Max(v => v.Count);
            rows[c] = Arr(Enumerable.Range(0, depth).Select(r => (JsonNode)Ints(Enumerable.Range(1, 15)
                .Select(level => per.TryGetValue(level, out var l) && r < l.Count ? l[r] : 0))));
        }
        var letters = DungeonSection.Select(s => ((char)('A' + _sections[s][151] - 1)).ToString()).ToList();
        var entrance = new JsonObject();
        for (var n = 1; n <= DungeonSection.Length; n++)
        {
            var raw = _sections[DungeonSection[n - 1]];
            if (n == 1)
            {
                if (raw[152] != 5)
                    throw new InvalidDataException("dungeon 1's entrance x is not 5");
                continue;
            }
            entrance[((char)('A' + raw[151] - 1)).ToString()] = (int)raw[152];
        }
        var offsets = new JsonArray { null };
        foreach (var b in _info.AsSpan(223, 15))
            offsets.Add((int)b);
        for (var i = 0; i < 10; i++)
            offsets.Add(null);
        JsonObject None(string address, params (string Key, JsonNode? Value)[] more)
        {
            var o = new JsonObject { ["_address"] = address };
            foreach (var (k, v) in more)
                o[k] = v;
            return o;
        }
        return new JsonObject
        {
            ["_source"] = "TINFO (loaded at XA + 310 = $08FC, XA = 1990; offsets = address - 2300) and INFO ($302) [C]",
            ["classes"] = Strs(P3Extract.Classes),
            ["hp_dice"] = new JsonObject
            {
                ["_address"] = "TINFO 0 + 15 class + level (TOWN3 1520: dice = v * (CON + 5) / 16 of rnd 1..3)",
                ["_note"] = "levels 1..15 (the Apple has no level 16+ row)", ["by_class"] = ByClass(0, 15, 15),
            },
            ["training_xp_curve"] = None("none: TOWN3 2820 computes the XP needed (1000 * (1.8^L - 0.8) / class score) [C]", ("values", null)),
            ["training_gold_curve"] = None("none: TOWN3 2900 computes the cost (CF * (20 - CHA)^2 * L^2.2, at most 15000) [C]", ("values", null)),
            ["magic_ceiling"] = new JsonObject { ["_address"] = "TINFO 740 + 15 class + level (TOWN3 1505)", ["by_class"] = ByClass(740, 15, 15) },
            ["skill_tables"] = new JsonObject
            {
                ["_address"] = "TINFO 830 + 150 class + 10 level + slot (TOWN3 1512)", ["_bases"] = bases, ["by_skill"] = skills,
            },
            ["spell_rows"] = new JsonObject
            {
                ["_address"] = "TINFO 380 + 60 class + spell - 1 = level; rows rebuilt by level, spells ascending", ["by_class"] = rows,
            },
            ["spell_levels"] = new JsonObject { ["_note"] = "TINFO 380 + 60 class + spell - 1 (spells 1..60)", ["by_class"] = levels },
            ["score_weights"] = None("none: the Mystic's score is level / 6 + log(1 + quest bits) * 16.23 (TOWN3 4630-4640) [C]", ("values", null)),
            ["hit_location"] = None("none: OUTDOORS3 8100 rolls the area (rnd ^ (DT / 2) * 6) [C]",
                ("_columns", null), ("_rows", null), ("rows", null)),
            ["air_elemental_spells"] = None("none: the Apple has no elementals", ("values", null)),
            ["wilderness_area_levels"] = None("none: each square's tile byte carries its own level (b mod 10, OUTDOORS3 5070)", ("values", null)),
            ["wilderness_terrain_offsets"] = new JsonObject
            {
                ["_address"] = "INFO 223 + terrain + rnd 0..3 (OUTDOORS3 6216 PEEK(993 + TR + RND * 4)); in DOS's index (t + rnd " +
                               "1..4) entry k + 1 = INFO 223 + k; entries 0 and 16..25 are not in INFO",
                ["values"] = offsets,
            },
            ["dungeon_letters"] = new JsonObject
            {
                ["_address"] = "the section's OT byte 151 = DNG file (letter A + file - 1); dungeons 1 and 8 share DNG10",
                ["values"] = Strs(letters),
            },
            ["dungeon_entrance_x"] = new JsonObject { ["_address"] = "the section's OT byte 152 (y = byte 153)", ["by_letter"] = entrance },
        };
    }

    // ---- GameData -----------------------------------------------------------------------------------------------

    private const string Game = P3DosPipeline.Game;

    /// <summary>The shipped cells (DOS III's values where the Apple's numbers differ or it has none).</summary>
    public const string CellsFile = "apple2-import.json";

    private static readonly CellPatches DosCells =
        new("apple", "dos", "the Apple copy's data", "the Apple's", "DOS III's", "DOS III's value");

    /// <summary>True when this Apple copy gives III's tables: none imported yet, or the ones there are its own.</summary>
    public static bool OwnsTables(ImportContext ctx) =>
        ctx.ReadImportedGameData(Game, "monsters.json") is not JsonObject existing
        || ((string?)existing["_source"])?.StartsWith(Label, StringComparison.Ordinal) == true;

    /// <summary>Write III's tables from the Apple with DOS's values (<see cref="BuildWithDosValues"/>); false when
    /// DOS III's tables (this run or earlier) or the Amiga's win.</summary>
    public static bool Write(ImportContext ctx)
    {
        if (!OwnsTables(ctx))
        {
            ctx.Warn("Phantasie III's game data comes from another copy imported as well (DOS or Amiga); the Apple II " +
                     "copy adds nothing to it.");
            return false;
        }
        var warnings = new List<string>();
        foreach (var (file, node) in BuildWithDosValues(ctx, warnings))
        {
            ctx.Cancel.ThrowIfCancellationRequested();
            ctx.WriteGameData(Game, file, node, indent: 1);
        }
        foreach (var w in warnings)
            ctx.Warn(w);
        return true;
    }

    /// <summary><see cref="Build"/>, then DOS III's value in every cell of the shipped <see cref="CellsFile"/>.</summary>
    public static List<(string File, JsonObject Node)> BuildWithDosValues(ImportContext context, List<string> warnings)
    {
        var built = Build(context);
        var cells = context.ReadShipped(Game, CellsFile)?["dos_cells"]?.AsArray()
                    ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile} missing");
        DosCells.Apply(cells, built.ToDictionary(t => t.File, t => t.Node), warnings);
        return built;
    }

    /// <summary>
    /// The ten class (b) / (c) files <see cref="P3DosPipeline"/> writes, built by <see cref="P3Build"/> from the Apple's
    /// tables (provenance relabelled), with the Apple's own values (the import uses <see cref="BuildWithDosValues"/>).
    /// What the Apple does not hold stays null or empty here; <c>attack_power</c> is the Apple's whole value.
    /// </summary>
    public static List<(string File, JsonObject Node)> Build(ImportContext context)
    {
        var x = new P3Apple2Tables(context.Files).Extract();
        var p1Spells = context.ReadShipped("phantasie1", "spells.json")?["spells"]?.AsArray()
                       ?? throw new InvalidOperationException("shipped phantasie1/spells.json is missing");
        var p1Creation = context.ReadShipped("phantasie1", "creation-tables.json")?.AsObject()
                         ?? throw new InvalidOperationException("shipped phantasie1/creation-tables.json is missing");
        JsonObject Shipped(string file) => context.ReadShipped(Game, file)?.AsObject()
                                           ?? throw new InvalidOperationException($"shipped phantasie3/{file} is missing");

        // P3Build's DOS shapes: attack power is stored / 10 (the Apple's whole value is set after), lists the Apple
        // lacks go in empty, names it lacks as "" (null again after).
        var monsters = x["monsters.json"];
        var monstersIn = (JsonObject)monsters.DeepClone();
        foreach (var m in monstersIn["monsters"]!.AsArray())
            m!["attack_power_div10"] = 0;
        var names = x["names.json"];
        var namesIn = (JsonObject)names.DeepClone();
        foreach (var (_, list) in namesIn)
            if (list is JsonArray a)
                foreach (var e in a)
                    if (e is JsonObject o && o.ContainsKey("name") && o["name"] is null)
                        o["name"] = "";
        var rt = x["rule-tables.json"];
        var rtIn = (JsonObject)rt.DeepClone();
        rtIn["air_elemental_spells"]!["values"] ??= new JsonArray();
        rtIn["hit_location"]!["_rows"] ??= new JsonArray();
        rtIn["hit_location"]!["rows"] ??= new JsonArray();

        var builtMonsters = P3Build.Monsters(monstersIn);
        var apple = monsters["monsters"]!.AsArray();
        var outMonsters = builtMonsters["monsters"]!.AsArray();
        for (var i = 0; i < outMonsters.Count; i++)
            outMonsters[i]!["attack_power"] = apple[i]!["attack_power"]!.DeepClone();
        var items = P3Build.Items(x["items.json"]);
        var spells = P3Build.Spells(x["spells.json"], p1Spells);
        var builtNames = P3Build.Names(namesIn, outMonsters, items["items"]!.AsArray(), spells["spells"]!.AsArray());
        foreach (var (key, list) in builtNames)
            if (list is JsonArray a)
                foreach (var e in a)
                    if (e is JsonObject o && o["name"] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0)
                        o["name"] = null;
        var built = new List<(string File, JsonObject Node)>
        {
            ("monsters.json", builtMonsters),
            ("items.json", items),
            ("spells.json", spells),
            ("names.json", builtNames),
            ("overworld.json", P3Build.Overworld(x["world.json"])),
            ("dungeons.json", P3Build.Dungeons(x["dungeons.json"])),
            ("scrolls.json", P3Build.Scrolls(x["scrolls.json"])),
            ("towns.json", P3Build.Towns(x["towns.json"], Shipped("towns.json"))),
            ("elementals.json", P3Build.Elementals(rtIn)),
            ("rule-tables.json", P3Build.RuleTables(rtIn, namesIn, p1Creation, Shipped("rule-tables.json"))),
        };
        foreach (var (_, node) in built)
            Relabel(node);
        return built;
    }

    /// <summary>P3Build prefixes provenance with DOS III's label; these files come from the Apple.</summary>
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

    /// <summary>The nine extract tables, keyed as <c>extract_a2_p3_tables.py</c> writes them.</summary>
    public Dictionary<string, JsonObject> Extract()
    {
        var names = Names();
        return new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["monsters.json"] = Monsters(),
            ["items.json"] = Items(),
            ["spells.json"] = Spells(),
            ["names.json"] = names,
            ["towns.json"] = TownsTable(),
            ["scrolls.json"] = Scrolls(),
            ["world.json"] = World(),
            ["dungeons.json"] = Dungeons(names),
            ["rule-tables.json"] = RuleTables(),
        };
    }
}
