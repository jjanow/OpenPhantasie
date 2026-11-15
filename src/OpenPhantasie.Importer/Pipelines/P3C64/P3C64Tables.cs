using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P3Apple2;
using OpenPhantasie.Importer.Pipelines.P3Dos;
using OpenPhantasie.Importer.Profiles;
using static OpenPhantasie.Importer.Pipelines.P3Dos.P3Extract;

namespace OpenPhantasie.Importer.Pipelines.P3C64;

/// <summary>
/// Phantasie III's game tables from the C64 disk alone (s218; v1.0 or v1.2 (alt), both sides as <c>.d64</c> or C64PP's
/// <c>.g64</c>), in the shapes <see cref="P3Extract"/> gives for DOS III, so the same <see cref="P3Build"/> turns them into
/// GameData. Layouts and every C64 ≠ DOS cell: <c>reverse-engineering/c64/phantasie3/FORMAT.md</c> section 3.
/// <para><see cref="Extract"/> gives the C64's own values (those it does not hold are <c>null</c> or empty, never DOS's;
/// text is the C64's own); <see cref="Write"/> writes the tables with DOS III's value in every number cell where they
/// differ, from the shipped <c>phantasie3/c64-import.json</c> (the rule of the Apple and Amiga III imports: DOS's values,
/// the copy's own text), and the starter party of <c>PCHARS0</c> (<see cref="P3C64StarterGuild"/>). When DOS III's, the
/// Amiga's or the Apple's tables are imported too (this run or earlier) they win and this writes nothing.</para>
/// <para>Reads side 1 (<c>PHANT.OBJ</c>, <c>MONSTERS</c>, <c>DATA</c>) and side 2 (<c>TOWN.OBJ</c>, <c>TOWN A</c>–<c>Q</c>,
/// <c>DNG0</c>–<c>DNG9</c>, <c>SCROLL A</c>–<c>T</c>, <c>MATERIALMAP</c>, <c>PCHARS0</c>). Every file is a PRG: its two
/// load-address bytes are dropped and offsets count from there.</para>
/// </summary>
public sealed class P3C64Tables
{
    /// <summary>Starts every <c>_source</c> these files carry (the <see cref="P3Build"/> label replaced), so a later
    /// run can tell them from DOS III's.</summary>
    public const string Label = "Phantasie III, C64 SSI 1987 (reverse-engineering/c64/phantasie3/FORMAT.md)";

    private const string Game = P3DosPipeline.Game;

    /// <summary>The shipped cells (DOS III's values where the C64's numbers differ or it has none).</summary>
    public const string CellsFile = "c64-import.json";

    private static readonly CellPatches DosCells =
        new("c64", "dos", "the C64 copy's data", "the C64's", "DOS III's", "DOS III's value");

    /// <summary>The C64's class order (Phantasie I's, Core's <c>CharacterClass</c>): the TOWN.OBJ tables and records.</summary>
    private static readonly string[] C64Class = ["Thief", "Monk", "Ranger", "Fighter", "Priest", "Wizard"];

    /// <summary>III's race numbering (names.json) → the C64's race number (Core's <c>Race</c>: the PHANT.OBJ name table at
    /// $0852 in Phantasie I's order); 5 is III's 'Random' menu entry, 16 Elemental.</summary>
    private static readonly int?[] IiiRaceFromC64 = [0, 1, 2, 4, 3, null, 8, 12, 10, 13, 14, 7, 11, 5, 6, 9, 16, 15];

    /// <summary>III's status order (Dead, Okay, Asleep, Out, Down) → the C64's status word (PHANT.OBJ 1343: DEAD OKAY DOWN
    /// SLEP OUT).</summary>
    private static readonly int[] IiiStatusFromC64 = [0, 1, 3, 4, 2];

    /// <summary>III's social classes (4 Darklord, 5 Superhero) → the C64's word (PHANT.OBJ 1283: … SUPER HERO, DARK LORD).</summary>
    private static readonly int[] IiiSocialFromC64 = [0, 1, 2, 3, 5, 4];

    /// <summary>III's skill order (Attack, Parry, Swimming, Listen, Find Trap, Disarm Trap, Find Item, Pick Lock, Fire Bow)
    /// → the C64 sheet's order (ATTACK PARRY FIND ITEM SPOT TRAP DISARM TRAP LISTEN FIRE BOW PICK LOCK SWIM).</summary>
    private static readonly int[] IiiSkillFromC64 = [0, 1, 8, 5, 3, 4, 2, 7, 6];

    /// <summary>
    /// III dungeon n (1..11) → the C64's dungeon description (PHANT.OBJ: 11 pointers after the terrain words, runtime =
    /// file − $1000), matched by what the text describes (DOS's dungeon names) [~]: the C64 keeps them in another order.
    /// </summary>
    private static readonly int[] DungeonText = [4, 5, 3, 2, 6, 7, 1, 0, 10, 8, 9];

    /// <summary>The SCROLL files in DOS's record order: A..T without F (Scroll IV, the map drawn in characters).</summary>
    private static readonly int[] ScrollFiles = [1, 2, 3, 4, 5, .. Enumerable.Range(7, 14)];

    // PHANT.OBJ (file offsets after the load address; it runs at file address − $1000, so a pointer p is at p − $400).
    private const int PhantRaces = 1106, PhantClasses = 1235, PhantSocial = 1283, PhantStatus = 1343, PhantOrigins = 1363,
        PhantSpells = 1417, PhantPoints = 1912, PhantTowns = 7247;

    // TOWN.OBJ (runs where it loads, $1C19).
    private const int TownSkillTables = 685, TownRequirement = 1447, TownValueLo = 1548, TownValueHi = 1729,
        TownPriceIndex = 1955, TownHpDice = 2346, TownSpellRows = 2448, TownMagicCeiling = 13601;

    private const int MonsterRecords = 4992, ItemNamesAt = 2313, ItemNameWidth = 9;

    /// <summary>The spell rows per class in TOWN.OBJ 2448 (the C64 class order).</summary>
    private static readonly int[] SpellRowCount = [1, 2, 2, 1, 3, 4];

    private readonly FileSet _files;
    private readonly byte[] _phant, _town;

    public P3C64Tables(FileSet files)
    {
        _files = files;
        _phant = Body("PHANT.OBJ");
        _town = Body("TOWN.OBJ");
        Version = VersionOf(_phant);
    }

    /// <summary>The build (v1.0 or v1.2 (alt)): v1.2's PHANT.OBJ is 6 bytes longer, its area levels 2 bytes later.</summary>
    public C64Build Version { get; }

    public sealed record C64Build(string Name, int AreaLevels);

    private static C64Build VersionOf(byte[] phant) => phant.Length switch
    {
        22547 => new("v1.0", 14888),
        22553 => new("v1.2 (alt)", 14890),
        _ => throw new InvalidDataException($"PHANT.OBJ: {phant.Length} bytes, not a known C64 III build"),
    };

    /// <summary>A file's bytes after its 2-byte PRG load address.</summary>
    private byte[] Body(string name)
    {
        var prg = _files.Read(name);
        return prg.Length < 2 ? throw new InvalidDataException($"{name}: no load address") : prg[2..];
    }

    // ---- text ---------------------------------------------------------------------------------------------------

    /// <summary>A string ended by a byte with bit 7 set (the C64 programs' inline and table strings).</summary>
    private static string Bit7(byte[] raw, int at)
    {
        var sb = new StringBuilder();
        for (var i = at; i < raw.Length; i++)
        {
            sb.Append((char)(raw[i] & 0x7F));
            if ((raw[i] & 0x80) != 0)
                break;
        }
        return sb.ToString();
    }

    private static string Fixed(byte[] raw, int at, int width) =>
        P3Exe.PyStrip(new string(raw.AsSpan(at, width).ToArray().Select(b => (char)(b & 0x7F)).ToArray()));

    /// <summary>A PHANT.OBJ string at runtime address <paramref name="pointer"/>.</summary>
    private string PhantAt(int pointer) => Bit7(_phant, pointer - 0x400);

    private int PhantPointer(int at) => _phant[at] | _phant[at + 1] << 8;

    /// <summary>The 56 spell names in DOS's order #1-52, 54-57 (no #53, as DOS).</summary>
    private List<string> SpellNames()
    {
        var names = new List<string>();
        for (var at = PhantSpells; names.Count < 56; at++)
        {
            var s = Bit7(_phant, at);
            names.Add(s);
            at += s.Length - 1;
        }
        if (names[^1] != "DIVINE")
            throw new InvalidDataException("PHANT.OBJ: the spell names moved");
        return names;
    }

    private List<string> Sequence(byte[] raw, int at, int count)
    {
        var list = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var s = Bit7(raw, at);
            list.Add(s);
            at += s.Length;
        }
        return list;
    }

    /// <summary>C64 screen code → character (1..26 letters, 32..63 as ASCII; bit 7 = reverse video).</summary>
    public static char ScreenCode(byte b)
    {
        b &= 0x7F;
        return b switch { 0 => '@', >= 1 and <= 26 => (char)('A' + b - 1), >= 32 and < 64 => (char)b, 27 => '[', 29 => ']', _ => ' ' };
    }

    private static JsonArray Lst(IEnumerable<string?> names) =>
        Arr(names.Select((x, i) => (JsonNode)new JsonObject { ["index"] = i, ["name"] = x }));

    private static JsonArray Bytes(ReadOnlySpan<byte> raw) => Ints(raw.ToArray().Select(b => (int)b));

    private static int C64ClassOf(string iiiClass) => Array.IndexOf(C64Class, iiiClass);

    // ---- monsters -----------------------------------------------------------------------------------------------

    public JsonObject Monsters() => MonstersFrom(Body("MONSTERS")[MonsterRecords..],
        "MONSTERS + 4992 (after 4,992 B of pictures): 80 x 38 B in DOS CREATURE.DAT's layout + 1 byte [C]; byte_27 = the " +
        "C64's own picture numbers");

    // ---- items --------------------------------------------------------------------------------------------------

    public JsonObject Items()
    {
        var data = Body("DATA");
        var list = new JsonArray();
        for (var i = 0; i < 181; i++)
        {
            var it = new JsonObject
            {
                ["id"] = i, ["name"] = i == 0 ? null : Fixed(data, ItemNamesAt + ItemNameWidth * (i - 1), ItemNameWidth),
                ["kind"] = ItemKind(i), ["value"] = _town[TownValueLo + i] | _town[TownValueHi + i] << 8,
            };
            if (i <= 100)
            {
                it["points"] = (int)_phant[PhantPoints + i];
                it["requirement"] = (int)_town[TownRequirement + i];
            }
            list.Add(it);
        }
        return new JsonObject
        {
            ["_source"] = "names DATA 2313 + 9 (id - 1) (180 x 9 characters) [C]; points PHANT.OBJ 1912 + id, requirement " +
                          "TOWN.OBJ 1447 + id (ids 0..100) [C]; value TOWN.OBJ 1548 + id (low byte) / 1729 + id (high byte) " +
                          "[C]; town price index TOWN.OBJ 1955 + town (one byte) [C]",
            ["town_price_index"] = Arr(Enumerable.Range(0, 18).Select(t => (JsonNode)new JsonObject
            {
                ["town"] = t, ["name"] = t == 0 ? null : TownName(t), ["index"] = (int)_town[TownPriceIndex + t],
            })),
            ["items"] = list,
        };
    }

    private string TownName(int t) => Sequence(_phant, PhantTowns, 17)[t - 1];

    public JsonObject Spells()
    {
        var names = SpellNames();
        return new JsonObject
        {
            ["_source"] = "PHANT.OBJ 1417: 56 names, bit 7 ends each, in DOS's order #1-52, 54-57 [C]; #53 empty (as DOS)",
            ["spells"] = Arr(Enumerable.Range(1, 57).Select(n => (JsonNode)new JsonObject
            {
                ["number"] = n, ["name"] = n == 53 ? "" : names[n < 53 ? n - 1 : n - 2],
            })),
        };
    }

    // ---- names --------------------------------------------------------------------------------------------------

    public JsonObject Names()
    {
        var races = Enumerable.Range(0, 17).Select(r => PhantAt(PhantPointer(PhantRaces + 2 * r))).ToList();
        var classes = Enumerable.Range(0, 6).Select(k => Fixed(_phant, PhantClasses + 8 * k, 7)).ToList();
        var social = Enumerable.Range(0, 6).Select(k => Fixed(_phant, PhantSocial + 10 * k, 10)).ToList();
        var status = Enumerable.Range(0, 5).Select(k => Fixed(_phant, PhantStatus + 4 * k, 4)).ToList();
        var origins = Enumerable.Range(0, 6).Select(k => PhantAt(PhantPointer(PhantOrigins + 2 * k))).ToList();
        var skills = SheetSkills();
        var towns = Sequence(_phant, PhantTowns, 17);
        var places = PlaceTexts();
        return new JsonObject
        {
            ["_source"] = "PHANT.OBJ tables [C]: races (pointers at runtime $0852), classes 1235 (8 B each), social classes " +
                          "1283 (10 B), status 1343 (4 B), origins (pointers at $0953), towns 7247, the character sheet's " +
                          "skill words, the dungeon descriptions (pointers after the terrain words); the C64's own wording " +
                          "(upper case); null where the C64 has no such name",
            ["combat_actions"] = Lst(Enumerable.Repeat<string?>(null, 19)),
            ["status_short"] = Lst(Enumerable.Repeat<string?>(null, 5)),
            ["class_short"] = Lst(Enumerable.Repeat<string?>(null, 6)),
            ["rank_labels"] = Lst(Enumerable.Repeat<string?>(null, 3)),
            ["places"] = Lst(DungeonText.Select(k => places[k])),
            ["inns"] = Lst(Enumerable.Repeat<string?>(null, 8)),
            ["terrain"] = Lst(Enumerable.Repeat<string?>(null, 22)),
            ["skills"] = Lst(IiiSkillFromC64.Select(k => skills[k])),
            ["races"] = Lst(IiiRaceFromC64.Select(a => a is { } k ? races[k] : null)),
            ["classes"] = Lst(P3Extract.Classes.Select(c => classes[C64ClassOf(c)])),
            ["towns"] = Lst(towns.Take(14)),
            ["origins"] = Lst(towns.Skip(14).Concat(origins)),
            ["status"] = Lst(IiiStatusFromC64.Select(k => status[k])),
            ["social_classes"] = Lst(IiiSocialFromC64.Select(k => social[k])),
            ["body_states"] = Lst(Enumerable.Repeat<string?>(null, 4)),
            ["transfer_class_map_I_to_III"] = Ints(C64Class.Select(c => Array.IndexOf(P3Extract.Classes, c))),
            ["transfer_race_map_I_to_III"] = Ints(Enumerable.Range(0, 16).Select(a => Array.IndexOf(IiiRaceFromC64, a))),
            ["_maps"] = "the C64's records use Phantasie I's class and race numbers (Core's): the I -> III maps follow from " +
                        "the names; the transfer utility (B, P3COPY.OBJ) was not read [?]",
        };
    }

    /// <summary>The nine skill words of the character sheet (PHANT.OBJ's inline text after "SKILLS:").</summary>
    private List<string> SheetSkills()
    {
        var at = IndexOf(_phant, "SKILLS:\r"u8);
        var text = Bit7(_phant, at + 8);
        var words = text.Split('\r');
        if (words.Length != 9 || words[0] != "ATTACK" || words[^1] != "SWIM")
            throw new InvalidDataException("PHANT.OBJ: the sheet's skill words moved");
        return [.. words];
    }

    private static int IndexOf(byte[] raw, ReadOnlySpan<byte> text)
    {
        var i = raw.AsSpan().IndexOf(text);
        return i >= 0 ? i : throw new InvalidDataException($"PHANT.OBJ: '{Encoding.ASCII.GetString(text)}' not found");
    }

    /// <summary>The 11 dungeon descriptions (pointers after the terrain words' "DESERT", 0), CR read as a space.</summary>
    private List<string> PlaceTexts()
    {
        var at = IndexOf(_phant, "BLACK PAT"u8) + 10; // "BLACK PATH" (H | 0x80), then "DESERT" (T | 0x80), 0
        if (Bit7(_phant, at) != "DESERT" || _phant[at + 6] != 0)
            throw new InvalidDataException("PHANT.OBJ: the dungeon descriptions moved");
        at += 7;
        return Enumerable.Range(0, 11).Select(k => PhantAt(PhantPointer(at + 2 * k)).Replace('\r', ' ').TrimEnd('.')).ToList();
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
            var row = Body($"TOWN {(char)('A' + t - 1)}");
            if (row.Length != 181)
                throw new InvalidDataException($"TOWN {(char)('A' + t - 1)}: {row.Length} bytes, expected 181");
            var stock = new int[181];
            for (var i = 1; i <= 180; i++)
                stock[i] = row[i];
            list.Add(new JsonObject
            {
                ["town"] = t, ["name"] = TownName(t), ["price_index"] = (int)_town[TownPriceIndex + t], ["kinds"] = (int)row[0],
                ["stock"] = Ints(stock),
                ["stocked"] = Arr(Enumerable.Range(0, 181).Where(i => stock[i] != 0)
                    .Select(i => (JsonNode)new JsonObject { ["id"] = i, ["count"] = stock[i] })),
            });
        }
        return new JsonObject
        {
            ["_source"] = "TOWN A..Q (181 B each = DOS INITARMS.DAT rows 1..17: byte 0, then the stock of ids 1..180) [C]; price " +
                          "index TOWN.OBJ 1955 + town [C]; names PHANT.OBJ 7247",
            ["_layout"] = "stock[id] for item id 0..180; kinds = byte 0 (0 on every C64 town)",
            ["towns"] = list,
        };
    }

    // ---- scrolls ------------------------------------------------------------------------------------------------

    /// <summary>A 960-B page of 40 x 24 screen codes as 24 lines (a short file padded with blanks).</summary>
    private static List<string> Page(byte[] raw)
    {
        var page = new byte[960];
        Array.Fill(page, (byte)32);
        raw.AsSpan(0, Math.Min(raw.Length, 960)).CopyTo(page); // SCROLL T is 2 bytes short (its last row blank)
        return Enumerable.Range(0, 24).Select(r => new string(page.AsSpan(40 * r, 40).ToArray().Select(ScreenCode).ToArray())).ToList();
    }

    /// <summary>The 38 columns inside the frame of every framed row ('!' in column 0), trailing blank rows dropped.</summary>
    private static List<string> Inner(List<string> page)
    {
        var inner = page.Where(l => l.StartsWith('!')).Select(l => P3Exe.PyRStrip(l[1..39])).ToList();
        while (inner.Count > 0 && inner[^1].Length == 0)
            inner.RemoveAt(inner.Count - 1);
        while (inner.Count > 0 && inner[0].Length == 0)
            inner.RemoveAt(0);
        return inner;
    }

    public JsonObject Scrolls()
    {
        var records = new JsonArray();
        var cont = new Dictionary<int, bool>();
        var pages = new Dictionary<int, List<string>>();
        for (var n = 1; n <= 20; n++)
        {
            var page = Page(Body($"SCROLL {(char)('A' + n - 1)}"));
            pages[n] = page;
            cont[n] = page[22].StartsWith('!'); // no bottom border: the next page follows
            if (n == 6)
                continue;
            var inner = Inner(page);
            records.Add(new JsonObject
            {
                ["record"] = Array.IndexOf(ScrollFiles, n), ["file"] = $"SCROLL {(char)('A' + n - 1)}", ["length"] = null,
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
            ["_source"] = "SCROLL A..T (side 2): 960-B pages of 40 x 24 screen codes, framed [C]. Records in DOS's numbering: " +
                          "SCROLL F (Scroll IV, the map drawn in characters) is not a text record. items: page i - 120 and " +
                          "the next while row 22 has no bottom border (the Apple's rule; the C64's item -> page map is in " +
                          "the programs, not read [?]). text = the 38 columns inside the frame, joined by newlines",
            ["scroll_iv_lines"] = Strs(pages[6]),
            ["records"] = records,
            ["items"] = items,
        };
    }

    // ---- world --------------------------------------------------------------------------------------------------

    public JsonObject World()
    {
        var map = Body("MATERIALMAP");
        if (map.Length < 3750)
            throw new InvalidDataException("MATERIALMAP: shorter than 50 x 75");
        JsonObject Plane(int id, string key, string source, int w, int h, JsonArray tiles) => new()
        {
            ["plane"] = id, ["key"] = key, ["source"] = source, ["width"] = w, ["height"] = h, ["arrival"] = null, ["tiles"] = tiles,
        };
        var scandor = Arr(Enumerable.Range(0, 75).Select(y => (JsonNode)Bytes(map.AsSpan(50 * y, 50))));
        return new JsonObject
        {
            ["_source"] = "MATERIALMAP (50 x 75 in DOS WORLD.DAT's layout, the C64's own tile ids) [C]; the plane maps " +
                          "(LIGHTMAP, DRKPLN, NETHERMAP), the places and the tile -> terrain table are not decoded [?] (empty " +
                          "here)",
            ["planes"] = new JsonArray
            {
                Plane(0, "scandor", "MATERIALMAP", 50, 75, scandor),
                Plane(1, "light", "LIGHTMAP", 0, 0, []),
                Plane(2, "dark", "DRKPLN", 0, 0, []),
                Plane(4, "netherworld", "NETHERMAP", 0, 0, []),
            },
            ["start"] = null,
            ["places"] = new JsonArray(),
            ["terrain"] = null,
        };
    }

    // ---- dungeons -----------------------------------------------------------------------------------------------

    private const int DngW = 24, DngH = 30;

    /// <summary>DOS record letter → the C64 file: A..I = DNG1..DNG9, J (= K) = DNG0 (FORMAT.md 3.7).</summary>
    private static string DngFile(char letter) => letter is 'J' or 'K' ? "DNG0" : $"DNG{letter - 'A' + 1}";

    public JsonObject Dungeons(JsonObject names)
    {
        var placeNames = names["places"]!.AsArray();
        var list = new JsonArray();
        for (var n = 1; n <= 11; n++)
        {
            var letter = Letters[n - 1];
            var rec = Body(DngFile(letter));
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
            var (slots, texts) = P3Apple2Tables.DungeonTexts(rec);
            list.Add(new JsonObject
            {
                ["dungeon"] = n, ["record"] = letter - 'A', ["letter"] = letter.ToString(), ["file"] = DngFile(letter),
                ["name"] = placeNames[n - 1]!["name"]!.DeepClone(), ["level"] = (int)rec[0x2D0], ["width"] = DngW, ["height"] = DngH,
                ["cells"] = cells, ["forced_fights"] = Bytes(rec.AsSpan(0x2D1, 10)), ["exits"] = exits, ["event_squares"] = squares,
                ["event_program_hex"] = P3Exe.Hex(rec.AsSpan(0x393, 0x200)), ["text_slots"] = slots, ["texts"] = texts,
                ["raw_hex"] = P3Exe.Hex(rec),
            });
        }
        return new JsonObject
        {
            ["_source"] = "DNG1..DNG9, DNG0 (side 2), one record each in DOS DNGS.DAT's layout (map 24 x 30, tables to 0x593, " +
                          "text pointers, CR-ended texts in the Apple's window font) [C]: DNGn = record letter n, DNG0 = record " +
                          "J (= K); dungeon n reads DOS's letter (the C64's dungeon -> file map is in the programs, not read [~])",
            ["dungeons"] = list,
        };
    }

    // ---- rule tables --------------------------------------------------------------------------------------------

    public JsonObject RuleTables()
    {
        JsonObject ByClass(int @base, int n)
        {
            var o = new JsonObject();
            foreach (var c in P3Extract.Classes)
                o[c] = Ints(Enumerable.Range(0, n).Select(lv => (int)_town[@base + 6 * lv + C64ClassOf(c)]));
            return o;
        }
        (string Skill, int K)[] tables =
        [
            ("attack", 0), ("parry", 1), ("swim", 7), ("listen", 5), ("find_trap", 3), ("disarm_trap", 4), ("find_item", 2), ("pick_lock", 6),
        ];
        var skills = new JsonObject();
        var bases = new JsonObject();
        foreach (var (skill, k) in tables)
        {
            skills[skill] = ByClass(TownSkillTables + 96 * k, 15);
            bases[skill] = $"TOWN.OBJ {TownSkillTables + 96 * k}";
        }
        var rows = new JsonObject();
        var levels = new JsonObject();
        var at = TownSpellRows;
        var rowsByC64 = new List<List<int[]>>();
        for (var k = 0; k < 6; k++)
        {
            var n = SpellRowCount[k];
            rowsByC64.Add(Enumerable.Range(0, n).Select(r => Enumerable.Range(0, 15).Select(lv => (int)_town[at + n * lv + r]).ToArray()).ToList());
            at += 15 * n;
        }
        foreach (var c in P3Extract.Classes)
        {
            var rs = rowsByC64[C64ClassOf(c)];
            rows[c] = Arr(rs.Select(r => (JsonNode)Ints(r)));
            var lv = new int[57];
            foreach (var r in rs)
                for (var i = 0; i < r.Length; i++)
                    if (r[i] != 0)
                        lv[r[i] - 1] = i + 1;
            levels[c] = Ints(lv);
        }
        JsonObject None(string address, params (string Key, JsonNode? Value)[] more)
        {
            var o = new JsonObject { ["_address"] = address };
            foreach (var (k, v) in more)
                o[k] = v;
            return o;
        }
        return new JsonObject
        {
            ["_source"] = "TOWN.OBJ / PHANT.OBJ tables, level-major in Phantasie I's class order (byte = base + 6 (level - 1) + " +
                          "class) [C]",
            ["classes"] = Strs(P3Extract.Classes),
            ["hp_dice"] = new JsonObject
            {
                ["_address"] = $"TOWN.OBJ {TownHpDice}", ["_note"] = "levels 1..15 (no level 16+ row on the C64)",
                ["by_class"] = ByClass(TownHpDice, 15),
            },
            ["training_xp_curve"] = None("not found on the C64 [?]", ("values", null)),
            ["training_gold_curve"] = None("not found on the C64 [?]", ("values", null)),
            ["magic_ceiling"] = new JsonObject { ["_address"] = $"TOWN.OBJ {TownMagicCeiling}", ["by_class"] = ByClass(TownMagicCeiling, 15) },
            ["skill_tables"] = new JsonObject
            {
                ["_address"] = $"TOWN.OBJ {TownSkillTables} + 96 k (16 levels each; levels 1..15 read)", ["_bases"] = bases,
                ["by_skill"] = skills,
            },
            ["spell_rows"] = new JsonObject
            {
                ["_address"] = $"TOWN.OBJ {TownSpellRows}: per class (C64 order) r rows, byte = base + r (level - 1) + row",
                ["by_class"] = rows,
            },
            ["spell_levels"] = new JsonObject { ["_note"] = "derived from spell_rows", ["by_class"] = levels },
            ["score_weights"] = None("not found on the C64 [?]", ("values", null)),
            ["hit_location"] = None("not found on the C64 [?]", ("_columns", null), ("_rows", null), ("rows", null)),
            ["air_elemental_spells"] = None("not read (ELEMENTALS' AIR record) [?]", ("values", null)),
            ["wilderness_area_levels"] = new JsonObject
            {
                ["_address"] = $"PHANT.OBJ {Version.AreaLevels} s8 x 25 ({Version.Name})",
                ["values"] = Ints(Enumerable.Range(0, 25).Select(i => (int)(sbyte)_phant[Version.AreaLevels + i])),
            },
            ["wilderness_terrain_offsets"] = None("not found on the C64 [?]", ("values", null)),
            ["dungeon_letters"] = new JsonObject
            {
                ["_address"] = "DOS's letters (the C64's dungeon -> file map is in the programs, not read) [~]",
                ["values"] = Strs(Letters.Select(ch => ch.ToString())),
            },
            ["dungeon_entrance_x"] = new JsonObject
            {
                ["_address"] = "not read on the C64 [?] (0 here)",
                ["by_letter"] = new JsonObject(Letters.Distinct().Order().Select(ch => KeyValuePair.Create(ch.ToString(), (JsonNode?)0))),
            },
        };
    }

    // ---- GameData -----------------------------------------------------------------------------------------------

    /// <summary>True when this C64 copy gives III's tables: none imported yet, or the ones there are its own.</summary>
    public static bool OwnsTables(ImportContext ctx) =>
        ctx.ReadImportedGameData(Game, "monsters.json") is not JsonObject existing
        || ((string?)existing["_source"])?.StartsWith(Label, StringComparison.Ordinal) == true;

    /// <summary>Write III's tables from the C64 with DOS's values (<see cref="BuildWithDosValues"/>) and the starter party;
    /// false when DOS III's, the Amiga's or the Apple's tables (this run or earlier) win.</summary>
    public static bool Write(ImportContext ctx)
    {
        if (!OwnsTables(ctx))
        {
            ctx.Warn("Phantasie III's game data comes from another copy imported as well (DOS, Amiga or Apple II); the C64 " +
                     "copy adds nothing to it (and no starter party).");
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
        P3C64StarterGuild.Write(ctx);
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
    /// The ten class (b) / (c) files <see cref="P3DosPipeline"/> writes, built by <see cref="P3Build"/> from the C64's
    /// tables (provenance relabelled), with the C64's own values (the import uses <see cref="BuildWithDosValues"/>).
    /// What the C64 does not hold stays null or empty here.
    /// </summary>
    public static List<(string File, JsonObject Node)> Build(ImportContext context)
    {
        var x = new P3C64Tables(context.Files).Extract();
        var p1Spells = context.ReadShipped("phantasie1", "spells.json")?["spells"]?.AsArray()
                       ?? throw new InvalidOperationException("shipped phantasie1/spells.json is missing");
        var p1Creation = context.ReadShipped("phantasie1", "creation-tables.json")?.AsObject()
                         ?? throw new InvalidOperationException("shipped phantasie1/creation-tables.json is missing");
        JsonObject Shipped(string file) => context.ReadShipped(Game, file)?.AsObject()
                                           ?? throw new InvalidOperationException($"shipped phantasie3/{file} is missing");

        // P3Build's DOS shapes: names the C64 lacks go in as "" (null again after), lists it lacks go in empty.
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
        foreach (var key in new[] { "training_xp_curve", "training_gold_curve", "score_weights" })
            rtIn[key]!["_address"] = (string)rt[key]!["_address"]!;

        var builtMonsters = P3Build.Monsters(x["monsters.json"]);
        var items = P3Build.Items(x["items.json"]);
        var spells = P3Build.Spells(x["spells.json"], p1Spells);
        var builtNames = P3Build.Names(namesIn, builtMonsters["monsters"]!.AsArray(), items["items"]!.AsArray(),
            spells["spells"]!.AsArray());
        foreach (var (key, list) in builtNames)
            if (list is JsonArray a && key is not ("spells" or "items" or "monsters"))
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

    /// <summary>P3Build prefixes provenance with DOS III's label; these files come from the C64.</summary>
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

    /// <summary>The nine extract tables, keyed as DOS III's <c>extracted/</c> files.</summary>
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
