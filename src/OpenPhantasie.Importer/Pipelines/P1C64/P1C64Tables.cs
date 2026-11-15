using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1Amiga;
using OpenPhantasie.Importer.Pipelines.P1Dos;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// Phantasie I's tables from the C64 disks alone (s216; every class (b) / (c) file <see cref="P1DosPipeline"/> writes), so
/// a C64-only import is a playable I. Layouts: <c>reverse-engineering/c64/phantasie1/FORMAT.md</c> section 3. Each C64
/// table is put into DOS layout and goes through DOS's builders, so the files have DOS I's schema; the C64's text, maps
/// and names are kept, and every number where the C64 differs (or holds none) takes DOS I's value from the shipped
/// remaster data <c>phantasie1/c64-import.json</c> (DOS wins on numbers; made by <c>tools/scripts/build_c64_p1_import.py</c>):
/// its <c>dos_cells</c>, the monster sprite sizes, the D-084 bleeb lines.
/// <para>Played copies: the towns, regions 2–19 and elementals come from side 2's <c>PATTERN</c>, the new-game template
/// (FORMAT.md 2; never written by play, the same on every copy), so a played copy imports as a fresh one. Region 1 is not
/// in it: side 1's OUT1, play-written but byte-identical on every copy, with DOS's values by <c>dos_cells</c>.</para>
/// <para>When DOS I's or the Amiga's tables are imported too (this run or earlier), they win and this writes nothing.</para>
/// </summary>
internal static class P1C64Tables
{
    private const string Game = P1DosPipeline.Game;
    public const string CellsFile = "c64-import.json";

    /// <summary>Starts every <c>_source</c> this writes, so a later run can tell its tables from another version's.</summary>
    public const string SourcePrefix = "Phantasie I C64 v1.4";

    private static readonly CellPatches DosCells =
        new("c64", "dos", "the C64 copy's data", "the C64's", "DOS I's", "DOS I's value");

    private static readonly CellPatches NameFixes =
        new("c64", "fixed", "the C64 copy's data", "the C64's", "the fixed", "the fixed name");

    private const int Monsters = 80, MnRecord = 100, ItemNames = 180, CharRecord = 170, ElementalFirst = 21;
    private const int TownBody = 250;

    /// <summary>
    /// Side 2's <c>PATTERN</c> (21,707 B, loads at $2000): the new-game template (FORMAT.md 2), never written by play.
    /// CHAR2 at 0, TWN 99 at 4260, TWN t at 4260 + 252 t, regions 2–19 at their slot address − $2000 (no region 1).
    /// </summary>
    private const int PatternSize = 21705, PatternTownsAt = 4260, PatternTownStride = 252, PatternBase = 0x2000;

    /// <summary>Each region's slot address (FORMAT.md 2.1): 2–4 from $3C74 by 1012, 5 at $4850, 6–16 from $4C44 by 759.</summary>
    private static int RegionSlot(int n) => n switch
    {
        >= 2 and <= 4 => 0x3C74 + 1012 * (n - 2),
        5 => 0x4850,
        >= 6 and <= 16 => 0x4C44 + 759 * (n - 6),
        18 => 0x6CE1,
        19 => 0x70D5,
        _ => throw new ArgumentOutOfRangeException(nameof(n), $"region {n} is not in PATTERN"),
    };

    private static byte[] Pattern(FileSet files)
    {
        var prg = files.Read("PATTERN");
        var body = Body(prg);
        if (Load(prg) != PatternBase || body.Length != PatternSize)
            throw Py.Bad($"PATTERN: expected {PatternSize} bytes at ${PatternBase:X4}");
        return body;
    }
    private const int GridBytes = 520, FeaturesAt = 520, TextAt = 595, Page = 40, DosOutSize = 1024;

    /// <summary>The town files in DOS record order (Pelnor .. Starville, then Olympia = <c>TWN 99</c>).</summary>
    public static readonly string[] TownFiles = [.. Enumerable.Range(1, 11).Select(t => $"TWN {t}"), "TWN 99"];

    /// <summary>True when this C64 copy gives I's tables: none imported yet, or the ones there are its own.</summary>
    public static bool OwnsTables(ImportContext ctx) =>
        ctx.ReadImportedGameData(Game, "monsters.json") is not JsonObject existing
        || ((string?)existing["_source"])?.StartsWith(SourcePrefix, StringComparison.Ordinal) == true;

    /// <summary>Write I's tables from the C64; false when DOS I's or the Amiga's tables (this run or earlier) win.</summary>
    public static bool Write(ImportContext ctx)
    {
        if (!OwnsTables(ctx))
        {
            ctx.Warn("Phantasie I's game data comes from another copy imported as well (DOS or Amiga); the C64 copy adds " +
                     "nothing to it.");
            return false;
        }
        var warnings = new List<string>();
        foreach (var (file, (node, ensureAscii)) in Build(ctx, warnings))
            ctx.WriteGameData(Game, file, node, ensureAscii: ensureAscii);
        foreach (var w in warnings)
            ctx.Warn(w);
        return true;
    }

    /// <summary>The C64 file's bytes after its 2-byte PRG load address.</summary>
    private static byte[] Body(byte[] prg) => prg.Length < 2 ? throw Py.Bad("a PRG file without a load address") : prg[2..];

    private static int Load(byte[] prg) => prg[0] | prg[1] << 8;

    /// <summary>The eleven files (name → JSON, whether DOS writes it ASCII-escaped), DOS's numbers applied.</summary>
    public static Dictionary<string, (JsonObject Node, bool EnsureAscii)> Build(ImportContext ctx, List<string> warnings)
    {
        var files = ctx.Files;
        var shipped = ctx.ReadShipped(Game, CellsFile) as JsonObject
                      ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile} missing");
        var townr = files.Read("TOWNR");
        var dataList = DataList(townr, "THIEF,");
        if (dataList.Count < 21)
            throw Py.Bad("TOWNR: the class / race DATA line was not found");
        var textPatches = shipped["text_patches"]?.AsArray() ?? [];
        byte[] Patched(string name)
        {
            var data = Body(files.Read(name));
            foreach (var p in textPatches.Where(p => (string?)p!["file"] == name))
                ApplyTextPatch(data, p!, warnings);
            return data;
        }

        var pattern = Pattern(files);
        var monsterTable = MonstersInDosLayout(files.Read("MN"));
        var names = NameTables(townr, dataList, pattern, files.Read("ITEM"), monsterTable,
            shipped["spell_names"]?.AsArray() ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile}: no spell_names"),
            TownFiles.Select((_, t) => TownName(TownBodyOf(pattern, t))));

        var items = ComTables.Items(ItemEntries(files, townr), $"{SourcePrefix}: ITEM (names, ids 1-180; id 0 TOWNR's) + TINFO 89 + id (values)",
            "name_ptr_c64", ("value_table_tinfo", 89), ("names_item", 0),
            "Combat points / min-STR / kind are formula-derived from index, as on DOS (FUN_0000_3053). value_factor V " +
            "is TINFO[89 + id] (= DOS's V[], all 180, FORMAT.md 3.2). name_ptr_c64 = offset in ITEM (id 0: in TOWNR).");
        var namesJson = ComTables.Names(names.Tables, names.TownLabels,
            $"{SourcePrefix}: TOWNR DATA (classes, races), ITEM, MN, PATTERN (labels); spell names: the manual's " +
            "spell table (c64-import.json spell_names; the C64 has none)",
            "offset = offset in the file: TOWNR + 0x10000, ITEM, MN (monster record), spells = 0x20000 + slot (the " +
            "manual's list). town_labels: PATTERN offsets (towns +230, CHAR2 records 21-23 +131). spell_level_names are really " +
            "the potion item names, ids 101..110. Monster stats: monsters.json.");
        var scrolls = Scrolls(files.Read("SCROLLS"));

        var built = new Dictionary<string, (JsonObject, bool)>
        {
            ["monsters.json"] = (ComTables.Monsters(monsterTable, SpriteSizes(shipped),
                $"{SourcePrefix}: MN (REL, 100-byte records of 24 CR-ended decimal fields; put in DOS layout, 0x39); " +
                "sprite sizes: DOS's (c64-import.json)"), false),
            ["items.json"] = (items, false),
            ["names.json"] = (namesJson, false),
            ["towns.json"] = (DataTables.Towns(TownsInDosLayout(pattern),
                $"{SourcePrefix}: PATTERN (the new-game template) TWN 1..11, TWN 99, 250 bytes each (put in DOS layout, 291)"), false),
            ["elementals.json"] = (DataTables.Elementals(ElementalsInDosLayout(pattern),
                $"{SourcePrefix}: PATTERN's CHAR2 records 21-23 (170 bytes each, put in DOS layout)"), true),
            ["scrolls.json"] = (scrolls, false),
            ["messages.json"] = (Messages(Patched), false),
            ["overworld.json"] = (MapTables.Overworld(RegionReader(files, pattern),
                $"{SourcePrefix}: PATTERN OUT2..OUT19 + side 1 OUT1 (grid, feature records, screen-code text pages; put in DOS's OUT*.DAT layout)"), false),
            ["dungeons.json"] = (MapTables.Dungeons(n => Body(files.Read($"DNG {n}")), n => Patched($"PMES {n}"),
                $"{SourcePrefix}: DNG 1..DNG 10 (DOS I's DNG1..DNG10 layout) + PMES 1..10"), true),
        };
        built["item-descriptions.json"] = (Derived.ItemDescriptions(items, scrolls), false);
        if (Derived.Spells(ctx.ReadShipped(Game, "spells.json") as JsonObject, ComTables.SpellNames(namesJson)) is { } spells)
            built["spells.json"] = (spells, true);
        else
            warnings.Add("spells.json is not shipped: spell names were not written");

        // DOS wins on numbers: DOS I's value in every cell where the C64's differs.
        var tables = built.ToDictionary(kv => kv.Key, kv => kv.Value.Item1);
        DosCells.Apply(shipped["dos_cells"]?.AsArray() ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile}: no dos_cells"),
            tables, warnings);
        if (shipped["name_fixes"]?.AsArray() is { } fixes)
            NameFixes.Apply(fixes, tables, warnings);
        return built;
    }

    // ---- text ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// A shipped text patch (D-084): the bytes at <c>offset</c> take the <c>dos</c> text where they hold the <c>c64</c>
    /// text (Latin-1); otherwise kept and warned.
    /// </summary>
    private static void ApplyTextPatch(byte[] data, JsonNode p, List<string> warnings)
    {
        var at = (int)p["offset"]!;
        var from = Encoding.Latin1.GetBytes((string)p["c64"]!);
        var to = Encoding.Latin1.GetBytes((string)p["dos"]!);
        if (from.Length != to.Length || at < 0 || at + to.Length > data.Length)
            throw new InvalidOperationException($"{CellsFile}: bad text patch at {p["file"]} {at}");
        var now = data.AsSpan(at, from.Length);
        if (now.SequenceEqual(to))
            return;
        if (!now.SequenceEqual(from))
        {
            warnings.Add($"{p["file"]} {at}: the text is neither the C64's nor DOS I's; kept.");
            return;
        }
        to.CopyTo(data, at);
    }

    /// <summary>
    /// A compiled program's DATA list from <paramref name="first"/> to its NUL: items split at commas, a quoted item
    /// kept whole, blanks around an item dropped; each with its file offset.
    /// </summary>
    internal static List<(string Text, int Offset)> DataList(byte[] program, string first)
    {
        var at = program.AsSpan().IndexOf(Encoding.ASCII.GetBytes(first));
        if (at < 0)
            return [];
        var items = new List<(string, int)>();
        var i = at;
        while (i < program.Length && program[i] != 0)
        {
            while (i < program.Length && program[i] == ' ')
                i++;
            var start = i;
            string text;
            if (program[i] == '"')
            {
                var end = Array.IndexOf(program, (byte)'"', i + 1);
                if (end < 0)
                    break;
                text = Encoding.Latin1.GetString(program, i + 1, end - i - 1);
                i = end + 1;
                start++;
            }
            else
            {
                var end = i;
                while (end < program.Length && program[end] is not (byte)',' and not 0)
                    end++;
                text = Encoding.Latin1.GetString(program, i, end - i).TrimEnd(' ');
                i = end;
            }
            items.Add((text, start));
            if (i < program.Length && program[i] == ',')
                i++;
        }
        return items;
    }

    /// <summary>The C64's names with its backspaces (0x08) applied and its 0x15 padding dropped (TWN 1 "PELOR⌫⌫NOR").</summary>
    internal static string Printed(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder();
        foreach (var b in raw)
        {
            if (b == 0)
                break;
            if (b == 0x08)
            {
                if (sb.Length > 0)
                    sb.Length--;
            }
            else if (b != 0x15)
                sb.Append((char)b);
        }
        return sb.ToString();
    }

    // ---- monsters -----------------------------------------------------------------------------------------------

    /// <summary>
    /// MN's 80 records in DOS layout (0x39 bytes): field 0 the name, fields 1–12 and 15–23 DOS's numbers in DOS order
    /// (FORMAT.md 3.1); fields 13, 14 and 24 are the C64's own (sprite blocks, <c>[?]</c>) and not used.
    /// </summary>
    private static byte[] MonstersInDosLayout(byte[] mn)
    {
        if (mn.Length < Monsters * MnRecord)
            throw Py.Bad($"MN: expected at least {Monsters * MnRecord} bytes, got {mn.Length}");
        var stride = ComTables.MonsterRecordSize;
        var output = new byte[stride * Monsters];
        for (var i = 0; i < Monsters; i++)
        {
            var fields = Encoding.Latin1.GetString(mn, i * MnRecord, MnRecord).Split('\r');
            if (fields.Length < 25)
                throw Py.Bad($"MN: record {i + 1} has {fields.Length - 1} fields");
            int F(int k) => int.TryParse(fields[k], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)
                ? v : throw Py.Bad($"MN: record {i + 1} field {k} '{fields[k]}' is not a number");
            var o = i * stride;
            var name = Encoding.Latin1.GetBytes(fields[0]);
            if (name.Length > 0x13)
                throw Py.Bad($"MN: record {i + 1}: name longer than 19");
            name.CopyTo(output, o);
            void W16(int off, int v) => BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(o + off), (short)v);
            void W32(int off, int v) => BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(o + off), v);
            void B(int off, int v) => output[o + off] = (byte)v;
            W16(0x14, F(1)); W16(0x16, F(2)); W16(0x18, F(3)); W32(0x1A, F(4)); W16(0x1E, F(5));
            B(0x20, F(6)); B(0x21, F(7)); B(0x22, F(8));
            W16(0x23, F(9)); W16(0x25, F(10)); B(0x27, F(11)); B(0x28, F(12));
            W16(0x29, F(15)); W16(0x2B, F(16)); W16(0x2D, F(17)); W32(0x2F, F(18));
            B(0x33, F(19)); B(0x34, F(20)); B(0x35, F(21)); B(0x36, F(22));
            W16(0x37, F(23));
        }
        return output;
    }

    /// <summary>DOS's 80 (width, height) pairs as u16 LE (the C64 has no such table): <c>c64-import.json</c> <c>sprite_sizes</c>.</summary>
    private static byte[] SpriteSizes(JsonObject shipped)
    {
        var sizes = shipped["sprite_sizes"]?.AsArray() ?? throw new InvalidOperationException($"{CellsFile}: no sprite_sizes");
        if (sizes.Count != Monsters)
            throw new InvalidOperationException($"{CellsFile}: {sizes.Count} sprite sizes, expected {Monsters}");
        var output = new byte[4 * Monsters];
        for (var i = 0; i < Monsters; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(4 * i), (ushort)(int)sizes[i]![0]!);
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(4 * i + 2), (ushort)(int)sizes[i]![1]!);
        }
        return output;
    }

    // ---- names and items ----------------------------------------------------------------------------------------

    private const int TownrBase = 0x10000, SpellBase = 0x20000;

    private sealed record Names(ComTables.NameTables Tables, JsonArray TownLabels);

    /// <summary>The 181 item names: id 0 TOWNR's literal (its <c>I$(0)</c>), ids 1–180 ITEM's CR-ended names.</summary>
    private static List<(string Name, int Offset)> ItemNameList(byte[] item, byte[] townr)
    {
        var nothing = townr.AsSpan().IndexOf("NOTHING"u8);
        if (nothing < 0)
            throw Py.Bad("TOWNR: item 0's name (NOTHING) not found");
        var list = new List<(string, int)> { ("NOTHING", TownrBase + nothing) };
        var at = 0;
        for (var id = 1; id <= ItemNames; id++)
        {
            var end = Array.IndexOf(item, (byte)'\r', at);
            if (end < 0)
                throw Py.Bad($"ITEM: {id - 1} names, expected {ItemNames}");
            list.Add((Encoding.Latin1.GetString(item, at, end - at), at));
            at = end + 1;
        }
        return list;
    }

    private static List<(string Name, int Ptr, int Value)> ItemEntries(FileSet files, byte[] townr)
    {
        var tinfo = Body(files.Read("TINFO"));
        if (tinfo.Length < 89 + ItemNames + 1)
            throw Py.Bad($"TINFO: {tinfo.Length} bytes, too short");
        // TINFO 89 + id is the value of id 1..180; id 0 (no item) has none on the C64, DOS's V[0] is 0.
        return [.. ItemNameList(files.Read("ITEM"), townr).Select((n, id) => (n.Name, n.Offset, id == 0 ? 0 : tinfo[89 + id]))];
    }

    private static Names NameTables(byte[] townr, List<(string Text, int Offset)> dataList, byte[] char2, byte[] item,
        byte[] monsterTable, JsonArray spellNames, IEnumerable<string> townNames)
    {
        var strings = new Dictionary<int, string>();
        int Add(string s, int ptr)
        {
            strings[ptr] = s;
            return ptr;
        }
        var classes = dataList.Take(6).Select(d => Add(d.Text, TownrBase + d.Offset)).ToArray();
        var races = dataList.Skip(6).Take(15).Select(d => Add(d.Text, TownrBase + d.Offset)).ToArray();
        var spells = Enumerable.Range(0, 60).Select(s => SpellBase + s).ToArray();
        foreach (var s in spellNames)
            Add((string)s!["name"]!, SpellBase + (int)s["slot"]!);
        var items = ItemNameList(item, townr).Select(n => Add(n.Name, n.Offset)).ToArray();
        var tables = new ComTables.NameTables("TOWNR", p => strings.GetValueOrDefault(p, ""), classes, races, spells, items,
            monsterTable, 0, MnRecord);

        // As DOS's town_labels (its TWNS.INT scan finds the 12 town names and the 3 elementals'): the TWN files' names
        // (+230) and the elementals' (CHAR2 +131 of records 21-23), as printed.
        var labels = new JsonArray();
        foreach (var name in townNames)
            labels.Add(ComTables.TownLabel(TownAt(labels.Count) + 230, name));
        for (var i = 0; i < DataTables.ElementalRecords; i++)
            labels.Add(ComTables.TownLabel((ElementalFirst + i) * CharRecord + 131,
                Py.Strip(Printed(char2.AsSpan((ElementalFirst + i) * CharRecord + 131, 12)))));
        return new Names(tables, labels);
    }

    // ---- towns --------------------------------------------------------------------------------------------------

    /// <summary>
    /// The 12 towns in DOS layout (291 bytes): stock of ids 1..180 (DOS's slot 0 = 0), the 45 restock ids as words, the
    /// price index, the region, the cell (DOS x = 25 − byte 229, y = byte 228), the name (10 characters blank-padded,
    /// two NULs, as DOS). Read from side 2's <c>PATTERN</c> (the new-game template, never written by play): town t
    /// (Olympia t = 0) at <see cref="PatternTownsAt"/> + 252 t, so a played copy's towns import as mastered.
    /// </summary>
    private static byte[] TownsInDosLayout(byte[] pattern)
    {
        var size = DataTables.TownRecordSize;
        var output = new byte[size * DataTables.TownRecords];
        for (var t = 0; t < TownFiles.Length; t++)
        {
            var a = TownBodyOf(pattern, t);
            var d = output.AsSpan(t * size, size);
            a.AsSpan(0, 180).CopyTo(d[1..]);
            for (var i = 0; i < 45; i++)
                BinaryPrimitives.WriteUInt16LittleEndian(d[(181 + 2 * i)..], a[180 + i]);
            BinaryPrimitives.WriteUInt16LittleEndian(d[0x10F..], a[226]);
            BinaryPrimitives.WriteUInt16LittleEndian(d[0x111..], a[227]);
            BinaryPrimitives.WriteUInt16LittleEndian(d[0x113..], (ushort)(25 - a[229]));
            BinaryPrimitives.WriteUInt16LittleEndian(d[0x115..], a[228]);
            Encoding.ASCII.GetBytes(TownName(a).PadRight(10)[..10]).CopyTo(d[279..]);
        }
        return output;
    }

    /// <summary>PATTERN's offset of town record t (DOS order; Olympia, <c>TWN 99</c>, comes first in PATTERN).</summary>
    private static int TownAt(int t) => PatternTownsAt + PatternTownStride * (t == TownFiles.Length - 1 ? 0 : t + 1);

    private static byte[] TownBodyOf(byte[] pattern, int t) => pattern.AsSpan(TownAt(t), TownBody).ToArray();

    /// <summary>A TWN file's name (bytes 230–249, 0x15-padded, backspaces applied), trimmed.</summary>
    private static string TownName(byte[] body) => Py.Strip(Printed(body.AsSpan(230, 20)));

    // ---- elementals ---------------------------------------------------------------------------------------------

    /// <summary>
    /// CHAR2 records 21–23 in DOS's 282-byte layout (FORMAT.md 3.5): INT, MP ceiling / max / current, HP, status,
    /// attack, parry, level, creation number, the points, race, class, the 60 spell flags, the 12-byte name (backspaces
    /// applied). DOS's race (UNDEAD shown) and the other fields DOS holds come from <c>dos_cells</c>.
    /// </summary>
    private static byte[] ElementalsInDosLayout(byte[] char2)
    {
        var size = DataTables.ElementalRecordSize;
        if (char2.Length < (ElementalFirst + 3) * CharRecord)
            throw Py.Bad($"CHAR2: {char2.Length} bytes, too short");
        var output = new byte[size * DataTables.ElementalRecords];
        for (var i = 0; i < DataTables.ElementalRecords; i++)
        {
            var p = char2.AsSpan((ElementalFirst + i) * CharRecord, CharRecord);
            var o = i * size;
            void W(int off, int v) => BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(o + off), (ushort)v);
            var d = output.AsSpan(o, size);
            W(2, p[2]);
            W(14, p[9]);
            W(16, p[10]);
            W(18, p[11]);
            W(20, p[12]);
            W(22, p[13]);
            W(24, p[18]);
            W(30, p[20]);
            W(32, p[22]);
            W(88, p[54]);
            W(92, p[59]);
            for (var k = 0; k < 60; k++)
                W(0x60 + 2 * k, p[61 + k]);
            d[216] = p[161];
            d[220] = p[151];
            W(240, p[162]);
            W(242, p[163]);
            W(244, p[164]);
            W(256, p[161]);
            var name = Encoding.ASCII.GetBytes(Printed(p.Slice(131, 12)));
            name.AsSpan(0, Math.Min(12, name.Length)).CopyTo(d[270..]);
        }
        return output;
    }

    // ---- scrolls ------------------------------------------------------------------------------------------------

    /// <summary>PETSCII of the lower / upper-case set to ASCII: 0xC1..0xDA capitals, 0x41..0x5A small letters.</summary>
    private static char Petscii(byte b) => b switch
    {
        >= 0xC1 and <= 0xDA => (char)(b - 0x80),
        >= 0x41 and <= 0x5A => (char)(b + 0x20),
        >= 0x20 and < 0x41 or 0x5B or 0x5D => (char)b,
        _ => throw Py.Bad($"SCROLLS: PETSCII 0x{b:x2} outside the text"),
    };

    /// <summary>
    /// scrolls.json from SCROLLS (REL, 40-byte records, FORMAT.md 3.9): records 1–4 the index (scroll k = records
    /// idx[k]..idx[k+1] − 1); each scroll is its page inside a <c>+---+</c> / <c>! … !</c> frame. The lines are the
    /// rows inside the frame (between the two '!'), with the frame's blank first and last rows dropped.
    /// </summary>
    internal static JsonObject Scrolls(byte[] data)
    {
        const int rec = 40;
        var idx = new List<int>();
        for (var n = 1; n <= 4; n++)
        {
            var r = data.AsSpan((n - 1) * rec, rec);
            foreach (var tok in Encoding.ASCII.GetString(r).Split('\r'))
                if (int.TryParse(tok.Trim('\0', ' '), NumberStyles.None, CultureInfo.InvariantCulture, out var v))
                    idx.Add(v);
        }
        if (idx.Count != 21 || data.Length < (idx[^1] - 1) * rec)
            throw Py.Bad($"SCROLLS: index of {idx.Count} entries (expected 21)");
        var scrolls = new JsonArray();
        for (var k = 0; k < 20; k++)
        {
            var rows = new List<string>();
            for (var n = idx[k]; n < idx[k + 1]; n++)
            {
                var r = data.AsSpan((n - 1) * rec, rec);
                var cr = r.IndexOf((byte)'\r');
                var row = cr < 0 ? r : r[..cr];
                var first = row.IndexOf((byte)'!');
                var last = row.LastIndexOf((byte)'!');
                if (first < 0 || last <= first)
                    continue; // the +---+ borders and the short blank rows before some frames
                rows.Add(Py.RStrip(string.Concat(row[(first + 1)..last].ToArray().Select(Petscii))));
            }
            while (rows.Count > 0 && rows[0].Length == 0)
                rows.RemoveAt(0);
            while (rows.Count > 0 && rows[^1].Length == 0)
                rows.RemoveAt(rows.Count - 1);
            if (rows.Count == 0)
                throw Py.Bad($"SCROLLS: scroll {k + 1} is empty");
            scrolls.Add(new JsonObject
            {
                ["id"] = k + 1,
                ["lines"] = Py.Strings(rows),
                ["text"] = string.Join("\n", rows.Where(r => Py.Strip(r).Length != 0)),
            });
        }
        return new JsonObject
        {
            ["_source"] = $"{SourcePrefix}: SCROLLS (REL, 40-byte records)",
            ["_format"] = "PETSCII (lower / upper-case set) pages inside a +---+ / ! ... ! frame; records 1-4 the index " +
                          "(reverse-engineering/c64/phantasie1/FORMAT.md 3.9). lines = the rows inside the frame.",
            ["scrolls"] = scrolls,
        };
    }

    // ---- messages -----------------------------------------------------------------------------------------------

    /// <summary>messages.json from PMES 1..10: DOS's decrypted MESSn layout, plain ASCII (FORMAT.md 3.8).</summary>
    private static JsonObject Messages(Func<string, byte[]> patched)
    {
        var doc = DataTables.Messages(n => patched($"PMES {n}"), encrypted: false);
        doc["_source"] = $"{SourcePrefix}: PMES 1..PMES 10";
        doc["_format"] = "plain ASCII after the 2-byte load address: the bytes DOS's MESS1..10 decrypt to (18 bytes shorter); " +
                         "records=41 bytes (1 prefix + 40 text) (reverse-engineering/c64/phantasie1/FORMAT.md 3.8)";
        var n = 1;
        foreach (var area in doc["areas"]!.AsArray())
            area!["source"] = $"PMES {n++}";
        return doc;
    }

    // ---- the overworld ------------------------------------------------------------------------------------------

    /// <summary>
    /// Each region in DOS's OUT*.DAT layout (1024 bytes, FORMAT.md 3.6): grid cell (x, y) = C64 byte (25 − x)·20 + y;
    /// the 15 feature records (DOS x, y = 25 − byte 1, byte 0); the full 40-byte text pages from 595, screen codes made
    /// ASCII (a page of CR padding is no page; a page with bytes 64 and up is DOS's leftover ASCII, kept as it is).
    /// </summary>
    private static Func<string, byte[]> RegionReader(FileSet files, byte[] pattern) => name =>
    {
        var n = int.Parse(name[3..name.IndexOf('.')], CultureInfo.InvariantCulture);
        // Regions 2-19 from PATTERN (mastered); region 1 is not in it: side 1's OUT1, the same bytes on every copy (2.1).
        var a = n == 1 ? Body(files.Read("OUT1"))
            : pattern.AsSpan(RegionSlot(n) - PatternBase, n is >= 6 and <= 16 ? 759 : 1012).ToArray();
        if (a.Length is not (1012 or 759))
            throw Py.Bad($"OUT{n}: expected 1012 or 759 bytes, got {a.Length}");
        var d = new byte[DosOutSize];
        for (var x = 0; x < 26; x++)
            for (var y = 0; y < 20; y++)
                d[y * 26 + x] = a[(25 - x) * 20 + y];
        for (var i = 0; i < 15; i++)
        {
            var f = a.AsSpan(FeaturesAt + 5 * i, 5);
            d[FeaturesAt + 5 * i] = (byte)(25 - f[1]);
            d[FeaturesAt + 5 * i + 1] = f[0];
            f[2..].CopyTo(d.AsSpan(FeaturesAt + 5 * i + 2));
        }
        for (var p = 0; p < (a.Length - TextAt) / Page; p++)
            ScreenPage(a.AsSpan(TextAt + p * Page, Page)).CopyTo(d, TextAt + p * Page);
        return d;
    };

    internal static byte[] ScreenPage(ReadOnlySpan<byte> page)
    {
        var output = new byte[page.Length];
        if (page.IndexOfAnyExcept((byte)0x0D) < 0)
            return output;
        if (page.IndexOfAnyInRange((byte)0x40, (byte)0xFF) >= 0)
        {
            page.CopyTo(output);
            return output;
        }
        for (var i = 0; i < page.Length; i++)
            output[i] = page[i] switch
            {
                0 => (byte)'@',
                >= 1 and <= 26 => (byte)(page[i] + 0x40),
                var c => c,
            };
        return output;
    }
}
