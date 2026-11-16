using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Pipelines.P1C64;
using OpenPhantasie.Importer.Pipelines.P1Dos;
using OpenPhantasie.Importer.Pipelines.P2Apple2;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P1Apple2;

/// <summary>
/// Phantasie I's tables from the Apple II disks alone (s222; every class (b) / (c) file <see cref="P1DosPipeline"/>
/// writes), so an Apple-only import is a playable I. Layouts: <c>reverse-engineering/apple2/phantasie1/FORMAT.md</c>.
/// As for the C64 (<see cref="P1C64Tables"/>), each Apple table is put into DOS layout and goes through DOS's builders,
/// so the files have DOS I's schema; the Apple's text and names are kept, and every number where the Apple differs (or
/// holds none) takes DOS I's value from the shipped remaster data <c>phantasie1/apple2-import.json</c> (DOS wins on
/// numbers; made by <c>tools/scripts/diff_a2_p1_vs_dos.py</c>): its <c>dos_cells</c>, the monster sprite sizes.
/// <para>The Apple draws the wilderness turned a quarter (FORMAT.md section 3.6): the grid bytes are DOS's, so the
/// region texts and scrolls get their compass words turned to DOS's orientation (as II's Apple import, D2-046).</para>
/// <para>Played copies: every known Apple copy is played, and the Apple's new game resets only the roster and the towns,
/// so its regions keep the fog lifted and the fixed fights spent. The towns come from side B's <c>TF</c> (the new-game
/// template); each region gets DOS's start state (DOS I's Reset rule, <see cref="P1DosPipeline.RestoredRegion"/>; OUT1 /
/// OUT5 open); the elementals come from <c>CHAR</c>'s last three records, which a new game keeps.</para>
/// <para>When DOS I's, the Amiga's or the C64's tables are imported too (this run or earlier), they win and this writes
/// nothing.</para>
/// </summary>
internal static class P1Apple2Tables
{
    private const string Game = P1DosPipeline.Game;
    public const string CellsFile = "apple2-import.json";

    /// <summary>Starts every <c>_source</c> this writes, so a later run can tell its tables from another version's.</summary>
    public const string SourcePrefix = "Phantasie I Apple II v1.0";

    private static readonly CellPatches DosCells =
        new("apple", "dos", "the Apple copy's data", "the Apple's", "DOS I's", "DOS I's value");

    private static readonly CellPatches NameFixes =
        new("apple", "fixed", "the Apple copy's data", "the Apple's", "the fixed", "the fixed name");

    private const int Monsters = 80, MnFields = 25, MnPerFile = 8, ItemNames = 180, CharRecord = 170, ElementalFirst = 37;
    private const int TownBody = 250, GridBytes = 520, FeaturesAt = 520, TextAt = 595, Page = 40, DosOutSize = 1024;

    /// <summary>The level part of a <c>DNGn</c> the import keeps: the level format (0–1699), then the first message
    /// slots, as far as DOS's level files hold the same bytes (DOS adds a 20-byte tail; the C64's files stop here too).</summary>
    public const int LevelBytes = 1962;

    /// <summary>The message slots of a <c>DNGn</c> (DUNGON 8650: slot M at <c>3750 + 40 M</c> = file offset 1700 + 40 M).</summary>
    private const int MsgBase = 1700;

    /// <summary>The town files in DOS record order (Pelnor .. Starville, then Olympia = <c>TWN99</c>); <c>TF</c> holds them
    /// in this order, 250 bytes each (BACKUP 310 / 450: TWN X at 4750 + 250 X, TWN99 at 7750, TF loaded at 5000).</summary>
    public static readonly string[] TownFiles = [.. Enumerable.Range(1, 11).Select(t => $"TWN{t}"), "TWN99"];

    /// <summary>True when this Apple copy gives I's tables: none imported yet, or the ones there are its own.</summary>
    public static bool OwnsTables(ImportContext ctx) =>
        ctx.ReadImportedGameData(Game, "monsters.json") is not JsonObject existing
        || ((string?)existing["_source"])?.StartsWith(SourcePrefix, StringComparison.Ordinal) == true;

    /// <summary>Write I's tables from the Apple; false when DOS I's, the Amiga's or the C64's tables (this run or earlier) win.</summary>
    public static bool Write(ImportContext ctx)
    {
        if (!OwnsTables(ctx))
        {
            ctx.Warn("Phantasie I's game data comes from another copy imported as well (DOS, Amiga or C64); the Apple II " +
                     "copy adds nothing to it.");
            return false;
        }
        var warnings = new List<string>();
        foreach (var (file, (node, ensureAscii)) in Build(ctx, warnings, withDosValues: true))
            ctx.WriteGameData(Game, file, node, ensureAscii: ensureAscii);
        foreach (var w in warnings)
            ctx.Warn(w);
        return true;
    }

    /// <summary>The two sides: side A holds <c>TOWN</c>, side B <c>DUNGON</c> (both carry <c>MN0</c>..<c>MN9</c>,
    /// <c>HELLO</c>, <c>QWERTY</c>).</summary>
    private sealed class Sides(FileSet files)
    {
        private readonly Container _a = files.Containers.FirstOrDefault(c => Has(c, "TOWN"))
                                        ?? throw new FileNotFoundException("side A (TOWN) not found");
        private readonly Container _b = files.Containers.FirstOrDefault(c => Has(c, "DUNGON"))
                                        ?? throw new FileNotFoundException("side B (DUNGON) not found");

        private static bool Has(Container c, string name) => c.Files.Any(f => f.Path == name);

        public byte[] A(string name) => Read(_a, name);

        public byte[] B(string name) => Read(_b, name);

        private static byte[] Read(Container c, string name) =>
            (c.Files.FirstOrDefault(f => f.Path == name) ?? throw new FileNotFoundException($"{name} not found on {c.Description}", name)).Data;
    }

    /// <summary>
    /// The eleven files (name → JSON, whether DOS writes it ASCII-escaped). <paramref name="withDosValues"/>: DOS I's
    /// numbers applied (the shipped cells and name fixes); false gives the Apple's raw build (what
    /// <c>diff_a2_p1_vs_dos.py --built</c> compares).
    /// </summary>
    public static Dictionary<string, (JsonObject Node, bool EnsureAscii)> Build(ImportContext ctx, List<string> warnings,
        bool withDosValues)
    {
        var s = new Sides(ctx.Files);
        var shipped = ctx.ReadShipped(Game, CellsFile) as JsonObject
                      ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile} missing");
        var compass = A2StValues.Parse(shipped);
        var town = s.A("TOWN");
        var classes = P1C64Tables.DataList(town, "THIEF,");
        var races = P1C64Tables.DataList(town, "HUMAN,");
        if (classes.Count < 6 || races.Count < 15)
            throw Py.Bad("TOWN: the class / race DATA lines (260 / 270) were not found");

        var tf = s.B("TF");
        if (tf.Length < TownBody * TownFiles.Length)
            throw Py.Bad($"TF: {tf.Length} bytes, expected {TownBody * TownFiles.Length}");
        var charFile = s.A("CHAR");
        var monsterTable = MonstersInDosLayout(s);
        var itemNames = ItemNameList(s.A("ITEM"));
        var names = NameTables(classes, races, itemNames, charFile, monsterTable,
            shipped["spell_names"]?.AsArray() ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile}: no spell_names"),
            tf);

        var tinfo = s.A("TINFO");
        if (tinfo.Length < 89 + ItemNames + 1)
            throw Py.Bad($"TINFO: {tinfo.Length} bytes, too short");
        // TINFO 89 + id is the value of id 1..180 (TOWN 1117's PEEK(XA + 399 + id)); id 0 (no item) has none, DOS's V[0] is 0.
        var items = ComTables.Items([.. itemNames.Select((n, id) => (n.Name, n.Offset, id == 0 ? 0 : (int)tinfo[89 + id]))],
            $"{SourcePrefix}: ITEM (names, ids 1-180; id 0 the remaster's NOTHING) + TINFO 89 + id (values)",
            "name_ptr_a2", ("value_table_tinfo", 89), ("names_item", 0),
            "Combat points / min-STR / kind are formula-derived from index, as on DOS (FUN_0000_3053). value_factor V " +
            "is TINFO[89 + id] (FORMAT.md 3.2). name_ptr_a2 = offset in ITEM (id 0: none on the Apple).");
        var namesJson = ComTables.Names(names.Tables, names.TownLabels,
            $"{SourcePrefix}: TOWN DATA 260 / 270 (classes, races), ITEM, MN0..MN9, TF / CHAR (labels); spell names: the " +
            "manual's spell table (apple2-import.json spell_names; the Apple has none)",
            "offset = offset in the file: TOWN + 0x10000, ITEM, monster = its index (MN<index div 8>, record index mod 8), " +
            "spells = 0x20000 + slot (the manual's list). town_labels: TF offsets (towns +230), CHAR records 37-39 +131 " +
            "+ 0x30000. spell_level_names are really the potion item names, ids 101..110. Monster stats: monsters.json.");
        var scrolls = Scrolls(s, compass);

        var built = new Dictionary<string, (JsonObject, bool)>
        {
            ["monsters.json"] = (ComTables.Monsters(monsterTable, SpriteSizes(shipped),
                $"{SourcePrefix}: MN0..MN9 (text, 8 records of 25 CR-ended fields; put in DOS layout, 0x39); sprite " +
                "sizes: DOS's (apple2-import.json)"), false),
            ["items.json"] = (items, false),
            ["names.json"] = (namesJson, false),
            ["towns.json"] = (DataTables.Towns(TownsInDosLayout(tf),
                $"{SourcePrefix}: TF (side B, the new-game template) TWN1..11, TWN99, 250 bytes each (put in DOS layout, 291)"), false),
            ["elementals.json"] = (DataTables.Elementals(ElementalsInDosLayout(charFile),
                $"{SourcePrefix}: CHAR records 37-39 (170 bytes each, put in DOS layout)"), true),
            ["scrolls.json"] = (scrolls, false),
            ["messages.json"] = (Messages(s), false),
            ["overworld.json"] = (MapTables.Overworld(P1DosPipeline.RestoredRegion(RegionReader(s, compass)),
                $"{SourcePrefix}: OUT1..OUT19 (grid, feature records, CR-ended text slots; put in DOS's OUT*.DAT layout; " +
                "DOS I's start state: DOS's Reset rule on every region but OUT1 / OUT5)"), false),
            ["dungeons.json"] = (Dungeons(s), true),
        };
        built["item-descriptions.json"] = (Derived.ItemDescriptions(items, scrolls), false);
        if (Derived.Spells(ctx.ReadShipped(Game, "spells.json") as JsonObject, ComTables.SpellNames(namesJson)) is { } spells)
            built["spells.json"] = (spells, true);
        else
            warnings.Add("spells.json is not shipped: spell names were not written");

        if (withDosValues)
        {
            // DOS wins on numbers: DOS I's value in every cell where the Apple's differs.
            var tables = built.ToDictionary(kv => kv.Key, kv => kv.Value.Item1);
            DosCells.Apply(shipped["dos_cells"]?.AsArray() ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile}: no dos_cells"),
                tables, warnings);
            if (shipped["name_fixes"]?.AsArray() is { } fixes)
                NameFixes.Apply(fixes, tables, warnings);
        }
        return built;
    }

    // ---- text ---------------------------------------------------------------------------------------------------

    /// <summary>The file's bytes with bit 7 cleared and control characters other than CR and backspace dropped (one
    /// monster name rings the bell: <c>BEE^G</c>).</summary>
    private static byte[] Low7(byte[] raw) =>
        [.. raw.Select(b => (byte)(b & 0x7F)).Where(b => b is >= 0x20 or 0x0D or 0x08 or 0x00)];

    // ---- monsters -----------------------------------------------------------------------------------------------

    /// <summary>
    /// MN0..MN9's 80 records in DOS layout (0x39 bytes): monster m is record m mod 8 of <c>MN&lt;m div 8&gt;</c>, 25
    /// CR-ended fields read by OUTDOORS 910 (FORMAT.md 3.1): field 0 the name, fields 1–12 and 15–23 DOS's numbers in
    /// DOS order; 13 (SCALE), 14 (HCOLOR) and 24 (shape) are the Apple's drawing and not used.
    /// </summary>
    private static byte[] MonstersInDosLayout(Sides s)
    {
        var stride = ComTables.MonsterRecordSize;
        var output = new byte[stride * Monsters];
        for (var f = 0; f < Monsters / MnPerFile; f++)
        {
            var data = Low7(s.A($"MN{f}"));
            var nul = Array.IndexOf(data, (byte)0);
            var fields = Encoding.Latin1.GetString(data, 0, nul < 0 ? data.Length : nul).Split('\r');
            if (fields.Length < MnPerFile * MnFields)
                throw Py.Bad($"MN{f}: {fields.Length} fields, expected {MnPerFile * MnFields}");
            for (var r = 0; r < MnPerFile; r++)
            {
                var i = f * MnPerFile + r;
                var rec = fields.AsSpan(r * MnFields, MnFields).ToArray();
                int F(int k) => int.TryParse(rec[k], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)
                    ? v : throw Py.Bad($"MN{f}: record {r} field {k} '{rec[k]}' is not a number");
                var o = i * stride;
                var name = Encoding.Latin1.GetBytes(rec[0].Replace("\b", "", StringComparison.Ordinal));
                if (name.Length > 0x13)
                    throw Py.Bad($"MN{f}: record {r}: name longer than 19");
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
        }
        return output;
    }

    /// <summary>DOS's 80 (width, height) pairs as u16 LE (the Apple has no such table): <c>apple2-import.json</c> <c>sprite_sizes</c>.</summary>
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

    private const int TownBase = 0x10000, SpellBase = 0x20000, CharBase = 0x30000;

    /// <summary>Item 0's name: the Apple has no record for "no item" (TOWN 230 reads ids 1–180); the remaster's word.</summary>
    private const string NoItem = "NOTHING";

    private sealed record Names(ComTables.NameTables Tables, JsonArray TownLabels);

    /// <summary>The 181 item names: id 0 the remaster's <see cref="NoItem"/>, ids 1–180 ITEM's CR-ended names (TOWN 230).</summary>
    private static List<(string Name, int Offset)> ItemNameList(byte[] item)
    {
        var data = Low7(item);
        var list = new List<(string, int)> { (NoItem, 0) };
        var at = 0;
        for (var id = 1; id <= ItemNames; id++)
        {
            var end = Array.IndexOf(data, (byte)'\r', at);
            if (end < 0)
                throw Py.Bad($"ITEM: {id - 1} names, expected {ItemNames}");
            list.Add((Encoding.Latin1.GetString(data, at, end - at), at));
            at = end + 1;
        }
        return list;
    }

    private static Names NameTables(List<(string Text, int Offset)> classes, List<(string Text, int Offset)> races,
        List<(string Name, int Offset)> itemNames, byte[] charFile, byte[] monsterTable, JsonArray spellNames, byte[] tf)
    {
        var strings = new Dictionary<int, string>();
        int Add(string s, int ptr)
        {
            strings[ptr] = s;
            return ptr;
        }
        var classPtrs = classes.Take(6).Select(d => Add(d.Text, TownBase + d.Offset)).ToArray();
        // TOWN 270 lists 17 races (LIZARD MAN, MINOTAUR, UNDEAD); DOS's table holds the first 15.
        var racePtrs = races.Take(15).Select(d => Add(d.Text, TownBase + d.Offset)).ToArray();
        var spells = Enumerable.Range(0, 60).Select(s => SpellBase + s).ToArray();
        foreach (var sp in spellNames)
            Add((string)sp!["name"]!, SpellBase + (int)sp["slot"]!);
        // Item 0 has no offset on the Apple; it is listed under the remaster's name at pointer -1.
        var items = itemNames.Select((n, id) => Add(n.Name, id == 0 ? -1 : n.Offset)).ToArray();
        var tables = new ComTables.NameTables("TOWN", p => strings.GetValueOrDefault(p, ""), classPtrs, racePtrs, spells, items,
            monsterTable, 0, 1);

        // As DOS's town_labels (its TWNS.INT scan finds the 12 town names and the 3 elementals'): TF's names (+230) and
        // the elementals' (CHAR +131 of records 37-39), as printed.
        var labels = new JsonArray();
        for (var t = 0; t < TownFiles.Length; t++)
            labels.Add(ComTables.TownLabel(TownBody * t + 230, TownName(tf.AsSpan(TownBody * t, TownBody))));
        for (var i = 0; i < DataTables.ElementalRecords; i++)
            labels.Add(ComTables.TownLabel(CharBase + (ElementalFirst + i) * CharRecord + 131,
                Py.Strip(P1C64Tables.Printed(charFile.AsSpan((ElementalFirst + i) * CharRecord + 131, 12)))));
        return new Names(tables, labels);
    }

    // ---- towns --------------------------------------------------------------------------------------------------

    /// <summary>
    /// The 12 towns in DOS layout (291 bytes), from side B's <c>TF</c> (the new-game template, never written by play):
    /// stock of ids 1..180 (DOS's slot 0 = 0), the 45 restock ids as words, the price index, the region, the cell (DOS
    /// x = 25 − byte 229, y = byte 228), the name (10 characters blank-padded, two NULs, as DOS). FORMAT.md 3.5.
    /// </summary>
    private static byte[] TownsInDosLayout(byte[] tf)
    {
        var size = DataTables.TownRecordSize;
        var output = new byte[size * DataTables.TownRecords];
        for (var t = 0; t < TownFiles.Length; t++)
        {
            var a = tf.AsSpan(TownBody * t, TownBody);
            var d = output.AsSpan(t * size, size);
            a[..180].CopyTo(d[1..]);
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

    /// <summary>A town record's name (bytes 230–249, 0x15-padded, backspaces applied: <c>PELOR⌫⌫NOR</c>), trimmed.</summary>
    private static string TownName(ReadOnlySpan<byte> body) => Py.Strip(P1C64Tables.Printed(body.Slice(230, 20)));

    // ---- elementals ---------------------------------------------------------------------------------------------

    /// <summary>
    /// CHAR records 37–39 in DOS's 282-byte layout: the C64's record layout (both keep the Apple's 170-byte record;
    /// <c>reverse-engineering/c64/phantasie1/FORMAT.md</c> 3.5). A new game (BACKUP 502) clears records 0–36 only, so
    /// these are the shipped elementals on a played copy too. DOS's race and the fields the Apple holds elsewhere come
    /// from <c>dos_cells</c>.
    /// </summary>
    private static byte[] ElementalsInDosLayout(byte[] charFile)
    {
        var size = DataTables.ElementalRecordSize;
        if (charFile.Length < (ElementalFirst + 3) * CharRecord)
            throw Py.Bad($"CHAR: {charFile.Length} bytes, too short");
        var output = new byte[size * DataTables.ElementalRecords];
        for (var i = 0; i < DataTables.ElementalRecords; i++)
        {
            var p = charFile.AsSpan((ElementalFirst + i) * CharRecord, CharRecord);
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
            var name = Encoding.ASCII.GetBytes(P1C64Tables.Printed(p.Slice(131, 12)));
            name.AsSpan(0, Math.Min(12, name.Length)).CopyTo(d[270..]);
        }
        return output;
    }

    // ---- scrolls ------------------------------------------------------------------------------------------------

    /// <summary>A row that draws a frame edge (<c>+----+</c>).</summary>
    private static bool Border(string row) => row.Contains("+--", StringComparison.Ordinal);

    /// <summary>
    /// scrolls.json from SCROLL1..SCROLL20 (side A): 1000-byte dumps of the 40 × 24 text page (TOWN 8010 loads item
    /// 120 + k's onto the screen; SCROLL20 is 768 bytes, its last rows blank). The lines are the rows between the
    /// frame's two <c>+---+</c> borders, inside the <c>! … !</c> sides where a scroll has them; blank first / last rows
    /// dropped; compass words turned to DOS's orientation (FORMAT.md 3.8).
    /// </summary>
    private static JsonObject Scrolls(Sides s, A2StValues compass)
    {
        var scrolls = new JsonArray();
        for (var k = 1; k <= 20; k++)
        {
            var page = A2Text.TextPage(s.A($"SCROLL{k}"));
            var first = page.FindIndex(Border);
            var last = page.FindLastIndex(Border);
            if (first < 0 || last <= first)
                throw Py.Bad($"SCROLL{k}: no frame");
            var rows = new List<string>();
            foreach (var row in page[(first + 1)..last])
            {
                var a = row.IndexOf('!', StringComparison.Ordinal);
                var b = row.LastIndexOf('!');
                var inside = a >= 0 && b > a ? row[(a + 1)..b] : row;
                rows.Add(Py.RStrip(compass.TurnCompass(inside)));
            }
            while (rows.Count > 0 && rows[0].Length == 0)
                rows.RemoveAt(0);
            while (rows.Count > 0 && Py.Strip(rows[^1]).Length == 0)
                rows.RemoveAt(rows.Count - 1);
            if (rows.Count == 0)
                throw Py.Bad($"SCROLL{k}: empty");
            scrolls.Add(new JsonObject
            {
                ["id"] = k,
                ["lines"] = Py.Strings(rows),
                ["text"] = string.Join("\n", rows.Where(r => Py.Strip(r).Length != 0)),
            });
        }
        return new JsonObject
        {
            ["_source"] = $"{SourcePrefix}: SCROLL1..SCROLL20 (side A, text-page dumps)",
            ["_format"] = "40 x 24 text page ($400, interleaved rows, high-bit ASCII); lines = the rows inside the frame, " +
                          "compass words turned to DOS's orientation (reverse-engineering/apple2/phantasie1/FORMAT.md 3.8).",
            ["scrolls"] = scrolls,
        };
    }

    // ---- dungeon messages ---------------------------------------------------------------------------------------

    /// <summary>A level's messages by slot id: the Apple prints slot M (1700 + 40 M) to its CR (CALL 800), so a long
    /// message runs on into the next slots; a slot inside another message's text is not listed (II's rule).</summary>
    private static List<(int Id, int Start, List<string> Lines)> LevelMessages(byte[] d)
    {
        var low = A2Text.Low7(d);
        var output = new List<(int, int, List<string>)>();
        var coveredTo = 0;
        for (var mid = 1; mid < (d.Length - MsgBase) / 40; mid++)
        {
            var start = MsgBase + 40 * mid;
            var slot = low.AsSpan(start, Math.Min(40, d.Length - start));
            if (start < coveredTo || A2Text.BlankSlot(slot))
                continue;
            var cr = low.AsSpan(start).IndexOf((byte)0x0D);
            var end = cr < 0 ? d.Length : start + cr;
            coveredTo = end + 1;
            output.Add((mid, start, A2Text.ScreenText(d.AsSpan(start, end - start), stopAtCr: true)));
        }
        return output;
    }

    /// <summary>"SELECT 1 OR 2" after a menu's last choice: the Apple's prompt, not part of the choice.</summary>
    private static readonly Regex SelectPrompt = new(@"\s*SELECT\b.*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// dungeons.json: each level's bytes 0–1961 (<see cref="LevelBytes"/>; the Phantasie I level format, DOS's layout:
    /// grid 33 × 38, events at 1270, scripts at 1450, level number 1255) and its messages in DOS I's shape
    /// (<c>lines</c>, <c>options</c>): a menu written in the text ("1-OPEN IT ... 2-LEAVE IT  SELECT 1 OR 2") becomes
    /// options when an event record asks for that many choices (record byte 1), as II's Apple import does.
    /// </summary>
    private static JsonObject Dungeons(Sides s)
    {
        var levels = new JsonObject();
        for (var n = 1; n <= 10; n++)
        {
            var d = s.B($"DNG{n}");
            if (d.Length < LevelBytes)
                throw Py.Bad($"DNG{n}: {d.Length} bytes, expected at least {LevelBytes}");
            var raw = d[..LevelBytes];
            var counts = A2GameData.OptionCounts(raw);
            var msgs = new JsonObject();
            foreach (var (mid, _, lines) in LevelMessages(d))
            {
                var count = counts.GetValueOrDefault(mid);
                if (count < 2)
                    count = A2GameData.MarkedOptions(lines); // a menu a script asks for (no event record names it)
                var (l, o) = count >= 2 ? A2GameData.SplitMenu(lines, count) : (lines, []);
                if (o.Count > 0)
                    o[^1] = SelectPrompt.Replace(o[^1], "");
                msgs[mid.ToString(CultureInfo.InvariantCulture)] = new JsonObject
                {
                    ["lines"] = Py.Strings(l.Count == 0 ? [""] : l),
                    ["options"] = Py.Strings(o),
                };
            }
            levels[$"DNG{n}"] = new JsonObject
            {
                ["id"] = $"p1:{n}",
                ["size"] = raw.Length,
                ["raw_hex"] = Py.Hex(raw),
                ["embedded_text"] = MapTables.TextRuns(raw),
                ["messages"] = msgs,
            };
        }
        return new JsonObject
        {
            ["_source"] = $"{SourcePrefix}: DNG1..DNG10 (side B; 6140 B, DNG3 / DNG10 5120 B): bytes 0-{LevelBytes - 1}",
            ["_note"] = MapTables.DungeonNote,
            ["levels"] = levels,
            ["_grid_byte_length"] = MapTables.GridByteLength,
            ["_grid_note"] = MapTables.GridNote,
            ["_messages_note"] = "levels.*.messages: the Apple's text (DUNGON 8650: slot 1700 + 40 * id, printed by CALL 800 " +
                                 "to its CR, so a long message runs on into the next slots; those are not listed). A menu " +
                                 "written in the text becomes options when an event record asks for that many choices " +
                                 "(reverse-engineering/apple2/phantasie1/FORMAT.md 3.7).",
        };
    }

    /// <summary>
    /// messages.json: the ten levels' message slots in DOS I's area shape (the loader wants ten areas; the game reads its
    /// dungeon text from dungeons.json). One record per 40-byte slot from id 1 on, as stored (CR and what follows it in
    /// the slot as blanks).
    /// </summary>
    private static JsonObject Messages(Sides s)
    {
        var areas = new JsonArray();
        for (var n = 1; n <= 10; n++)
        {
            var d = s.B($"DNG{n}");
            var low = A2Text.Low7(d);
            var slots = new JsonArray();
            var records = new JsonArray();
            var count = (d.Length - MsgBase) / 40;
            for (var mid = 1; mid < count; mid++)
            {
                var at = MsgBase + 40 * mid;
                var chars = new char[40];
                var ended = false;
                for (var i = 0; i < 40; i++)
                {
                    var c = low[at + i];
                    ended |= c == 0x0D;
                    chars[i] = ended || c < 0x20 ? ' ' : (char)c;
                }
                var raw = new string(chars);
                var text = Py.RStrip(raw);
                records.Add(new JsonObject
                {
                    ["offset"] = at, ["record_index"] = mid - 1, ["text"] = text, ["raw_line"] = raw,
                });
                slots.Add(new JsonObject { ["slot"] = mid, ["offset"] = at, ["text"] = text.Length == 0 ? null : text });
            }
            areas.Add(new JsonObject
            {
                ["id"] = n, ["source"] = $"DNG{n}", ["size"] = d.Length, ["text_start"] = MsgBase + 40,
                ["slot_count"] = count - 1, ["record_count"] = records.Count, ["slots"] = slots, ["records"] = records,
            });
        }
        return new JsonObject
        {
            ["_source"] = $"{SourcePrefix}: DNG1..DNG10 message slots (side B)",
            ["_format"] = "40-byte slots from file offset 1740 (slot 1; DUNGON 8650), high-bit ASCII, CR-ended; the Apple keeps " +
                          "its dungeon text in the level files (reverse-engineering/apple2/phantasie1/FORMAT.md 3.7)",
            ["areas"] = areas,
        };
    }

    // ---- the overworld ------------------------------------------------------------------------------------------

    /// <summary>
    /// Each region in DOS's OUT*.DAT layout (1024 bytes, FORMAT.md 3.6): grid cell (x, y) = Apple byte (25 − x)·20 + y
    /// (OUTDOORS 390: cell (CX, CY) at <c>LA + CY·20 + CX</c>, CX = DOS y, CY = 25 − DOS x); the 15 feature records (DOS
    /// x, y = 25 − byte 1, byte 0). A text cell (tile mod 10 = 6) prints the CR-ended string from slot <c>595 + 40·arg</c>
    /// (OUTDOORS 6020); its screen lines, compass words turned, become DOS's pages arg, arg + 1, … and its arg
    /// <c>arg + 10·(lines − 1)</c>, DOS's page form (as II's Apple import).
    /// </summary>
    private static Func<string, byte[]> RegionReader(Sides s, A2StValues compass) => name =>
    {
        var n = int.Parse(name[3..name.IndexOf('.', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
        var a = s.A($"OUT{n}");
        if (a.Length is not (1024 or 768))
            throw Py.Bad($"OUT{n}: expected 1024 or 768 bytes, got {a.Length}");
        var low = A2Text.Low7(a);
        var d = new byte[DosOutSize];
        for (var x = 0; x < 26; x++)
            for (var y = 0; y < 20; y++)
                d[y * 26 + x] = a[(25 - x) * 20 + y];
        var pages = new SortedDictionary<int, string>();
        for (var i = 0; i < 15; i++)
        {
            var f = a.AsSpan(FeaturesAt + 5 * i, 5);
            var o = FeaturesAt + 5 * i;
            d[o] = (byte)(25 - f[1]);
            d[o + 1] = f[0];
            f[2..].CopyTo(d.AsSpan(o + 2));
            var tile = a[f[1] * 20 + f[0]];
            if (tile is >= 120 and < 255)
                tile -= 120;
            if (f[0] == 0 && f[1] == 0 || tile % 10 != 6)
                continue;
            var arg = f[2];
            var at = TextAt + Page * arg;
            if (at >= a.Length)
                throw Py.Bad($"OUT{n}: text slot {arg} past the file");
            var cr = low.AsSpan(at).IndexOf((byte)0x0D);
            var lines = A2Text.ScreenText(a.AsSpan(at, cr < 0 ? a.Length - at : cr), stopAtCr: true)
                .Select(compass.TurnCompass).ToList();
            var count = Math.Clamp(lines.Count, 1, 3);
            for (var k = 0; k < count; k++)
                pages.TryAdd(arg + k, k < lines.Count ? lines[k] : "");
            d[o + 2] = (byte)(arg + 10 * (count - 1));
        }
        foreach (var (p, text) in pages)
        {
            var at = TextAt + Page * p;
            if (at + Page > DosOutSize)
                throw Py.Bad($"OUT{n}: text page {p} past DOS's layout");
            Encoding.Latin1.GetBytes(text.PadRight(Page)[..Page]).CopyTo(d, at);
        }
        return d;
    };
}
