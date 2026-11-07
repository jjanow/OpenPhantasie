using System.Buffers.Binary;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// The player's IV files and the extracted tables the build reads (port of <c>extract_x68k_p4_tables.py</c> and
/// <c>extract_x68k_p4_creation.py</c>, kept in memory: their <c>extracted/*.json</c> files are RE intermediates, not
/// game data).
/// </summary>
internal sealed class P4Source
{
    // Pointer tables in P4.X's data segment: symbol -> entry count (the relocated pointer run's length).
    private static readonly (string Sym, int Count)[] NameTables =
    [
        ("_plname", 7), ("_origin", 7), ("_towns", 22), ("_inns", 7), ("_dungeons", 22), ("_dnames", 22), ("_race", 35),
        ("_class", 10), ("_social", 10), ("_abil", 6), ("_skill", 11), ("_body", 6), ("_hurt", 4), ("_itm", 157),
        ("_magic", 65),
    ];

    public const int ItemCount = 157;
    private readonly FileSet _files;

    public P4Source(FileSet files)
    {
        _files = files;
        P4X = files.Read("P4.X");
        X = new XFile(P4X);
        Tabl = files.Read("TABL");
        if (Tabl.Length != 0x852)
            throw new InvalidDataException("TABL: unexpected size");
        foreach (var (sym, count) in NameTables)
        {
            var a = X.SymbolValue[sym];
            if (X.RelocRun(a) < count)
                throw new InvalidDataException($"{sym}: relocated run shorter than {count}");
            var list = new string[count];
            var ptrs = new int[count];
            for (var i = 0; i < count; i++)
            {
                ptrs[i] = (int)X.Long(a + 4 * i);
                list[i] = P4Util.CStr(X.Image, ptrs[i]);
            }
            Names[sym.TrimStart('_')] = list;
            NamePointers[sym.TrimStart('_')] = ptrs;
        }
    }

    public byte[] P4X { get; }
    public XFile X { get; }
    public byte[] Tabl { get; }
    public Dictionary<string, string[]> Names { get; } = new(StringComparer.Ordinal);
    /// <summary>The string addresses behind <see cref="Names"/> (their <c>p4x.0x…</c> ids).</summary>
    public Dictionary<string, int[]> NamePointers { get; } = new(StringComparer.Ordinal);

    public byte[] Read(string name) => _files.Read(name);

    private readonly Dictionary<int, byte[]> _events = new();

    public byte[] EventFile(int n) => _events.TryGetValue(n, out var b) ? b : _events[n] = Read($"O_{n:000}");

    // ---- P4.X image reads (image address = file offset - 0x40) -------------------------------------------------------

    public byte[] Image(int addr, int n) => X.Image.AsSpan(addr, n).ToArray();

    public int[] Longs(int addr, int n) => Enumerable.Range(0, n).Select(k => X.SLong(addr + 4 * k)).ToArray();

    public double[] Doubles(int addr, int n) => Enumerable.Range(0, n)
        .Select(k => BinaryPrimitives.ReadDoubleBigEndian(X.Image.AsSpan(addr + 8 * k))).ToArray();

    // ---- extracted tables ------------------------------------------------------------------------------------------

    public sealed record Item(int Id, string Name, int Icon, int Value, int Requirement, int MovePct, int Price);

    public List<Item> Items()
    {
        var b = X.SymbolValue["_idat"];
        var im = X.Image;
        return Enumerable.Range(0, ItemCount).Select(i =>
        {
            var o = b + 6 * i;
            return new Item(i, Names["itm"][i], im[o], im[o + 1], im[o + 2], (sbyte)im[o + 3],
                BinaryPrimitives.ReadUInt16BigEndian(im.AsSpan(o + 4)));
        }).ToList();
    }

    /// <summary>_gen_itm's bytes (to _idat).</summary>
    public byte[] GenItm() => X.Image[X.SymbolValue["_gen_itm"]..X.SymbolValue["_idat"]];

    public sealed record HallPerson(int Slot, string Name, int Race, int Cls, int Level, int Social, int Origin,
        int[] Stats, int[] Results, int Descendants, int DeedsLine, int Alive);

    public List<HallPerson> Hall()
    {
        var f = Read("FAME");
        var hall = new List<HallPerson>();
        for (var i = 0; i < f.Length / 28; i++)
        {
            var r = f.AsSpan(i * 28, 28);
            if (!r.ContainsAnyExcept((byte)0))
                continue;
            hall.Add(new HallPerson(i, P4Util.CStrAscii(r, 1), r[0x0B], r[0x0C], r[0x0D], r[0x0E], r[0x0F],
                r[0x10..0x15].ToArray().Select(v => (int)v).ToArray(), r[0x15..0x18].ToArray().Select(v => (int)v).ToArray(),
                (sbyte)r[0x18], r[0x19], r[0x1A]));
        }
        return hall;
    }

    // creation tables (TABL; extract_x68k_p4_creation.py). Classes 0-7 have TABL rows, 15 levels.
    public const int PlayerClasses = 8;
    public const int Levels = 15;

    private static int S8(byte b) => b > 127 ? b - 256 : b;

    public int[] HpGrowth(int c) => Tabl[(c * Levels)..((c + 1) * Levels)].Select(v => (int)v).ToArray();

    public int[] RaceAbilityMod(int r) => Tabl[(0x78 + 5 * r)..(0x7d + 5 * r)].Select(S8).ToArray();

    public int[] ClassAbilityMod(int c) => Tabl[(0x122 + 5 * c)..(0x127 + 5 * c)].Select(S8).ToArray();

    public int[] MagicLevel(int c) => Tabl[(0x14a + c * Levels)..(0x14a + (c + 1) * Levels)].Select(v => (int)v).ToArray();

    public int[] SkillGrowth(int s, int c) =>
        Tabl[(0x1c2 + (s * 8 + c) * Levels)..(0x1c2 + (s * 8 + c + 1) * Levels)].Select(v => (int)v).ToArray();

    /// <summary>Spell numbers made learnable per level (TABL 0x6EA), as the build maps names back to numbers.</summary>
    public List<List<int>> SpellsGranted(int c)
    {
        var magic = Names["magic"];
        var outList = new List<List<int>>();
        for (var lv = 0; lv < Levels; lv++)
        {
            var o = 0x6ea + (c * Levels + lv) * 3;
            outList.Add(Tabl[o..(o + 3)].Where(i => i != 0).Select(i => Array.IndexOf(magic, magic[i - 1]) + 1).ToList());
        }
        return outList;
    }
}
