using System.Buffers.Binary;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P1Dos;
using OpenPhantasie.Importer.Pipelines.P3Amiga;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Phantasie I's tables from the Amiga disk alone (every class (b) / (c) file <see cref="P1DosPipeline"/> writes), so an
/// Amiga-only import is a playable I. Layouts: <c>reverse-engineering/amiga/GAME-TABLES.md</c>. Each Amiga table is put
/// into DOS layout and goes through the DOS builders, so the files have DOS I's schema; the Amiga's text, maps and names
/// are kept, and every number where the Amiga differs takes DOS I's value from the shipped remaster data
/// <c>phantasie1/amiga-import.json</c> (DOS wins over Amiga on numbers). Only key files and the never-rewritten parts of
/// the region files are read, so a played copy imports the same as a fresh one.
/// <para>When DOS I's tables are imported too (this run or earlier), they win and this writes nothing.</para>
/// </summary>
internal static class P1AmigaTables
{
    private const string Game = "phantasie1";
    public const string CellsFile = "amiga-import.json";

    /// <summary>Starts every <c>_source</c> this writes, so a later run can tell its tables from DOS I's.</summary>
    public const string SourcePrefix = "Phantasie I Amiga v1.0";

    private static readonly CellPatches DosCells =
        new("amiga", "dos", "the Amiga copy's data", "the Amiga's", "DOS I's", "DOS I's value");

    private static readonly CellPatches NameFixes =
        new("amiga", "fixed", "the Amiga copy's data", "the Amiga's", "the fixed", "the fixed name");

    // ---- the program: hunk 3 (monsters) and hunk 12 (names, values) of `game` ---------------------------------------

    private const int MonsterHunk = 3;
    private const int MonsterTable = 0x14;   // 80 x 0x3a
    private const int AmigaMonsterStride = 0x3A;
    private const int SpriteDims = 0x1236;   // 80 x (u16 width, u16 height), big-endian
    private const int MonsterCount = 80;

    private const int NameHunk = 12;
    private const int ItemPtrs = 0x7AA;      // 181 x u32
    private const int ClassPtrs = 0xAAA;     // 6
    private const int RacePtrs = 0xB2A;      // 16 (15 used, as DOS; the 16th is UNDEAD)
    private const int SpellPtrs = 0xE18;     // 60
    private const int ValueTable = 0x63E;    // 182 x u16 (the last unused)

    /// <summary>(DOS offset, Amiga offset, bytes) of a monster record; 2 and 4 are big-endian words / longs.</summary>
    private static readonly (int Dos, int Amiga, int Size)[] MonsterFields =
    [
        (0x00, 0x00, 0), // name, 20 bytes (copied)
        (0x14, 0x14, 2), (0x16, 0x16, 2), (0x18, 0x18, 2), (0x1A, 0x1A, 4), (0x1E, 0x1E, 2),
        (0x20, 0x20, 1), (0x21, 0x21, 1), (0x22, 0x22, 1), // specials; Amiga +0x23 is a pad byte (0)
        (0x23, 0x24, 2), (0x25, 0x26, 2), (0x27, 0x28, 1), (0x28, 0x29, 1), (0x29, 0x2A, 2), (0x2B, 0x2C, 2),
        (0x2D, 0x2E, 2), (0x2F, 0x30, 4), (0x33, 0x34, 1), (0x34, 0x35, 1), (0x35, 0x36, 1), (0x36, 0x37, 1),
        (0x37, 0x38, 2),
    ];

    // ---- twns.int ---------------------------------------------------------------------------------------------------

    private const int TwnsIntSize = 13000;
    private const int TownsAt = 2446;        // 12 x 292 (DOS TWNS.INT 2445, 12 x 291)
    private const int AmigaTownStride = 292;
    private const int ElementalsAt = 12154;  // 3 x 282 (DOS 12141)
    private const int RosterRecords = 22;

    // ---- the region files -------------------------------------------------------------------------------------------

    private const int AmigaOutSize = 2500;
    private const int MapsIntGrid = 520;     // one region's 26 x 20 cells in maps.int, cell (x, y) at (25 - x) * 20 + y
    private const int DosOutSize = 1024;
    private const int FeaturesAt = 0x208;    // 15 x 5 in both
    private const int TextAt = 0x253;        // the 40-byte pages, to 1000 on the Amiga (polygons follow)
    private const int AmigaTextEnd = 1000;

    /// <summary>True when this Amiga copy gives I's tables: none imported yet, or the ones there are its own.</summary>
    public static bool OwnsTables(ImportContext ctx) =>
        ctx.ReadImportedGameData(Game, "monsters.json") is not JsonObject existing
        || ((string?)existing["_source"])?.StartsWith(SourcePrefix, StringComparison.Ordinal) == true;

    /// <summary>Write I's tables from the Amiga; false when DOS I's tables (this run or earlier) win.</summary>
    public static bool Write(ImportContext ctx)
    {
        if (!OwnsTables(ctx))
        {
            ctx.Warn("Phantasie I's game data comes from the DOS copy (imported as well); the Amiga copy adds its art, " +
                     "fonts and sounds.");
            return false;
        }
        var warnings = new List<string>();
        foreach (var (file, (node, ensureAscii)) in Build(ctx, warnings))
            ctx.WriteGameData(Game, file, node, ensureAscii: ensureAscii);
        foreach (var w in warnings)
            ctx.Warn(w);
        return true;
    }

    /// <summary>The eleven files (name → JSON, whether DOS writes it ASCII-escaped), DOS's numbers applied.</summary>
    public static Dictionary<string, (JsonObject Node, bool EnsureAscii)> Build(ImportContext ctx, List<string> warnings)
    {
        var files = ctx.Files;
        var hunks = AmigaHunks.Parse(files.Read("game"));
        var monsterHunk = hunks[MonsterHunk];
        var nameHunk = hunks[NameHunk];
        if (monsterHunk.Type != "DATA" || nameHunk.Type != "DATA")
            throw Py.Bad("game: hunks 3 / 12 are not the data hunks the Amiga I tables live in");
        var twnsInt = files.Read("twns.int");
        if (twnsInt.Length != TwnsIntSize)
            throw Py.Bad($"twns.int: expected {TwnsIntSize} bytes, got {twnsInt.Length}");

        var monsterTable = MonstersInDosLayout(monsterHunk.Data);
        var dims = SwapWords(monsterHunk.Data.AsSpan(SpriteDims, 4 * MonsterCount));
        var names = NameTables(nameHunk, monsterTable);

        var items = ComTables.Items(ItemEntries(nameHunk, names), $"{SourcePrefix}: game hunk 12 (names +0x7aa, values +0x63e)",
            "name_ptr_h12", ("value_table_h12", ValueTable), ("name_ptrs_h12", ItemPtrs),
            "Combat points / min-STR / kind are formula-derived from index, as on DOS (FUN_0000_3053). value_factor V " +
            "is the only parallel numeric table (hunk 12 +0x63e, big-endian; DOS DS:0xd54).");
        var namesJson = ComTables.Names(names, TownLabels(twnsInt),
            $"{SourcePrefix}: game hunk 12 pointer tables (items +0x7aa, classes +0xaaa, races +0xb2a, spells +0xe18), " +
            "hunk 3 monster table + twns.int",
            "offset = offset in game hunk 12 (monsters: hunk 3; twns.int labels: file offset). spell_level_names are " +
            "really the potion item names, ids 101..110 (only referenced from the item pointer table). " +
            "Monster stats: monsters.json.");
        var scrolls = DataTables.Scrolls(files.Read("scrolls.dtx"), encrypted: false);

        var built = new Dictionary<string, (JsonObject, bool)>
        {
            ["monsters.json"] = (ComTables.Monsters(monsterTable, dims,
                $"{SourcePrefix}: game hunk 3 +0x14, 80 x 0x3a big-endian (put in DOS layout, 0x39); sizes hunk 3 +0x1236"), false),
            ["items.json"] = (items, false),
            ["names.json"] = (namesJson, false),
            ["towns.json"] = (DataTables.Towns(TownsInDosLayout(twnsInt),
                $"{SourcePrefix}: twns.int 2446, 12 x 292 big-endian (put in DOS layout, 291)"), false),
            ["elementals.json"] = (DataTables.Elementals(ElementalsInDosLayout(twnsInt),
                $"{SourcePrefix}: twns.int tail, 3 x 282-byte character records at file 12154/12436/12718, big-endian " +
                "(put in DOS layout). Roster records 22-24."), true),
            ["scrolls.json"] = (scrolls, false),
            ["messages.json"] = (DataTables.Messages(n => files.Read($"mess{n}"), encrypted: false), false),
            ["overworld.json"] = (MapTables.Overworld(RegionReader(files),
                $"{SourcePrefix}: maps.int (the 18 grids as mastered) + out*.dat features and text pages, put in DOS's " +
                "OUT*.DAT layout"), false),
            ["dungeons.json"] = (MapTables.Dungeons(n => files.Read($"dng{n}"), n => files.Read($"mess{n}"),
                $"{SourcePrefix}: dng1..dng10 (DOS I's DNG1..DNG10 layout)"), true),
        };
        built["item-descriptions.json"] = (Derived.ItemDescriptions(items, scrolls), false);
        if (Derived.Spells(ctx.ReadShipped(Game, "spells.json") as JsonObject, ComTables.SpellNames(namesJson)) is { } spells)
            built["spells.json"] = (spells, true);
        else
            warnings.Add("spells.json is not shipped: spell names were not written");

        // DOS wins over Amiga on numbers: DOS I's value in every cell where the Amiga's differs.
        var cells = ctx.ReadShipped(Game, CellsFile)?["dos_cells"]?.AsArray()
                    ?? throw new InvalidOperationException($"shipped {Game}/{CellsFile} missing");
        var tables = built.ToDictionary(kv => kv.Key, kv => kv.Value.Item1);
        DosCells.Apply(cells, tables, warnings);
        // User s202 (DIFFERENCES.md D-082 / D-083): the Amiga's wording is kept, its two name typos are fixed.
        if (ctx.ReadShipped(Game, CellsFile)?["name_fixes"]?.AsArray() is { } fixes)
            NameFixes.Apply(fixes, tables, warnings);
        else
            warnings.Add($"shipped {Game}/{CellsFile} has no name_fixes: the Amiga's name typos are kept");
        return built;
    }

    // ---- monsters -----------------------------------------------------------------------------------------------

    private static byte[] MonstersInDosLayout(byte[] hunk)
    {
        var stride = ComTables.MonsterRecordSize;
        var output = new byte[stride * MonsterCount];
        for (var i = 0; i < MonsterCount; i++)
        {
            var a = hunk.AsSpan(MonsterTable + i * AmigaMonsterStride, AmigaMonsterStride);
            var d = output.AsSpan(i * stride, stride);
            if (a[0x23] != 0)
                throw Py.Bad($"game: monster {i}: pad byte +0x23 is {a[0x23]}");
            foreach (var (dos, amiga, size) in MonsterFields)
                switch (size)
                {
                    case 0:
                        a[..0x14].CopyTo(d);
                        break;
                    case 1:
                        d[dos] = a[amiga];
                        break;
                    case 2:
                        BinaryPrimitives.WriteUInt16LittleEndian(d[dos..], BinaryPrimitives.ReadUInt16BigEndian(a[amiga..]));
                        break;
                    default:
                        BinaryPrimitives.WriteUInt32LittleEndian(d[dos..], BinaryPrimitives.ReadUInt32BigEndian(a[amiga..]));
                        break;
                }
        }
        return output;
    }

    /// <summary>Big-endian u16s to little-endian (an even length).</summary>
    private static byte[] SwapWords(ReadOnlySpan<byte> be)
    {
        var output = new byte[be.Length];
        for (var i = 0; i + 1 < be.Length; i += 2)
        {
            output[i] = be[i + 1];
            output[i + 1] = be[i];
        }
        return output;
    }

    // ---- names and items ----------------------------------------------------------------------------------------

    private static ComTables.NameTables NameTables(AmigaHunks.Hunk h, byte[] monsterTable)
    {
        int[] Pointers(int at, int count)
        {
            h.Relocs.TryGetValue(NameHunk, out var relocs);
            var set = relocs?.ToHashSet() ?? [];
            var ptrs = new int[count];
            for (var i = 0; i < count; i++)
            {
                if (!set.Contains(at + 4 * i))
                    throw Py.Bad($"game: hunk 12 +0x{at + 4 * i:x} is not a pointer into hunk 12");
                ptrs[i] = (int)BinaryPrimitives.ReadUInt32BigEndian(h.Data.AsSpan(at + 4 * i));
            }
            return ptrs;
        }

        string CString(int at)
        {
            if (at < 0 || at >= h.Data.Length)
                throw Py.Bad($"game: name pointer 0x{at:x} outside hunk 12");
            return Py.Latin1(Py.UntilNul(h.Data.AsSpan(at, Math.Min(0x40, h.Data.Length - at))));
        }

        return new ComTables.NameTables("game", CString, Pointers(ClassPtrs, 6), Pointers(RacePtrs, 15),
            Pointers(SpellPtrs, 60), Pointers(ItemPtrs, ComTables.ItemCount), monsterTable, MonsterTable, AmigaMonsterStride);
    }

    private static List<(string Name, int Ptr, int Value)> ItemEntries(AmigaHunks.Hunk h, ComTables.NameTables names)
    {
        var list = new List<(string, int, int)>();
        for (var i = 0; i < ComTables.ItemCount; i++)
            list.Add((names.CString(names.Items[i]), names.Items[i],
                BinaryPrimitives.ReadUInt16BigEndian(h.Data.AsSpan(ValueTable + 2 * i))));
        return list;
    }

    // ---- towns and elementals (twns.int) ------------------------------------------------------------------------

    /// <summary>
    /// The 12 towns in DOS layout (291 bytes): stock 181 bytes (the Amiga's pad byte 181 dropped), 45 restock words,
    /// the price index, a word, then the town's map cell (DOS x, y = Amiga 25 - y, x, the map's transposition), the name.
    /// </summary>
    public static byte[] TownsInDosLayout(byte[] twnsInt)
    {
        var size = DataTables.TownRecordSize;
        var output = new byte[size * DataTables.TownRecords];
        for (var t = 0; t < DataTables.TownRecords; t++)
        {
            var a = twnsInt.AsSpan(TownsAt + t * AmigaTownStride, AmigaTownStride);
            var d = output.AsSpan(t * size, size);
            if (a[181] != 0)
                throw Py.Bad($"twns.int: town {t}: pad byte 181 is {a[181]}");
            a[..181].CopyTo(d);
            for (var i = 0; i < 46; i++) // 45 restock items + the price index
                BinaryPrimitives.WriteUInt16LittleEndian(d[(181 + 2 * i)..], BinaryPrimitives.ReadUInt16BigEndian(a[(182 + 2 * i)..]));
            BinaryPrimitives.WriteUInt16LittleEndian(d[273..], BinaryPrimitives.ReadUInt16BigEndian(a[274..]));
            int ax = BinaryPrimitives.ReadUInt16BigEndian(a[276..]), ay = BinaryPrimitives.ReadUInt16BigEndian(a[278..]);
            if (ay > 25)
                throw Py.Bad($"twns.int: town {t}: map cell y {ay} outside the region");
            BinaryPrimitives.WriteUInt16LittleEndian(d[275..], (ushort)(25 - ay));
            BinaryPrimitives.WriteUInt16LittleEndian(d[277..], (ushort)ax);
            a[280..292].CopyTo(d[279..]);
        }
        return output;
    }

    /// <summary>
    /// twns.int's name fields, as DOS's <c>town_labels</c> lists them (its scan finds the 12 town and 3 elemental names):
    /// each town's name (record +280) and each elemental's (+270), up to its NUL, trimmed, at its file offset.
    /// </summary>
    private static JsonArray TownLabels(byte[] twnsInt)
    {
        var labels = new JsonArray();
        void Add(int at, int length) =>
            labels.Add(ComTables.TownLabel(at, Py.Strip(Py.Latin1(Py.UntilNul(twnsInt.AsSpan(at, length))))));
        for (var t = 0; t < DataTables.TownRecords; t++)
            Add(TownsAt + t * AmigaTownStride + 280, 12);
        for (var i = 0; i < DataTables.ElementalRecords; i++)
            Add(ElementalsAt + i * DataTables.ElementalRecordSize + 270, 12);
        return labels;
    }

    /// <summary>The three elemental character records in DOS layout: 135 words (big- to little-endian), the 12-byte name.</summary>
    public static byte[] ElementalsInDosLayout(byte[] twnsInt)
    {
        var size = DataTables.ElementalRecordSize;
        if (twnsInt.AsSpan(ElementalsAt - RosterRecords * size, RosterRecords * size).ContainsAnyExcept((byte)0))
            throw Py.Bad("twns.int: roster placeholder block is not zero");
        var output = new byte[size * DataTables.ElementalRecords];
        for (var i = 0; i < DataTables.ElementalRecords; i++)
        {
            var a = twnsInt.AsSpan(ElementalsAt + i * size, size);
            var d = output.AsSpan(i * size, size);
            SwapWords(a[..270]).CopyTo(d);
            a[270..].CopyTo(d[270..]);
        }
        return output;
    }

    // ---- the overworld ------------------------------------------------------------------------------------------

    /// <summary>
    /// Each region in DOS's OUT*.DAT layout (1024 bytes): the grid from maps.int (never rewritten), the 15 feature
    /// records (DOS x, y = Amiga 25 - y, x) and the text pages from the region file (play rewrites only its grid).
    /// </summary>
    private static Func<string, byte[]> RegionReader(Profiles.FileSet files)
    {
        var mapsInt = files.Read("maps.int");
        if (mapsInt.Length != MapsIntGrid * MapTables.OverworldRegions.Length)
            throw Py.Bad($"maps.int: expected {MapsIntGrid * MapTables.OverworldRegions.Length} bytes, got {mapsInt.Length}");
        return name =>
        {
            var n = int.Parse(name[3..name.IndexOf('.')], System.Globalization.CultureInfo.InvariantCulture);
            var k = Array.IndexOf(MapTables.OverworldRegions, n);
            var a = files.Read($"out{n}.dat");
            if (k < 0 || a.Length != AmigaOutSize)
                throw Py.Bad($"out{n}.dat: expected {AmigaOutSize} bytes, got {a.Length}");
            var d = new byte[DosOutSize];
            for (var x = 0; x < 26; x++)
                for (var y = 0; y < 20; y++)
                    d[y * 26 + x] = mapsInt[k * MapsIntGrid + (25 - x) * 20 + y];
            for (var i = 0; i < 15; i++)
            {
                var f = a.AsSpan(FeaturesAt + 5 * i, 5);
                // Byte arithmetic: OUT3's unused record 12 sits at DOS x 68 (Amiga 25 - 68 = 213).
                d[FeaturesAt + 5 * i] = (byte)(25 - f[1]);
                d[FeaturesAt + 5 * i + 1] = f[0];
                f[2..].CopyTo(d.AsSpan(FeaturesAt + 5 * i + 2));
            }
            a.AsSpan(TextAt, AmigaTextEnd - TextAt).CopyTo(d.AsSpan(TextAt));
            return d;
        };
    }
}
