using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// A Human68k <c>.X</c> executable (port of <c>tools/scripts/x68k_x.py</c>): HU header, relocation table, symbol table.
/// Image addresses are offsets into text + data as loaded at base 0 (file offset = image address + 0x40).
/// </summary>
internal sealed class XFile
{
    public sealed record Symbol(string Name, int Value, int Type, string Kind);

    private static readonly Dictionary<int, string> SymKinds = new()
    {
        [0x0201] = "text", [0x0202] = "data", [0x0203] = "bss", [0x0003] = "common", [0x0200] = "abs", [0x0204] = "linker",
    };

    private readonly (int Value, string Name)[] _textSyms;

    public XFile(byte[] blob)
    {
        if (blob.Length < 0x40 || blob[0] != 'H' || blob[1] != 'U')
            throw new InvalidDataException("not a Human68k .X file");
        Base = U32(blob, 4);
        Entry = U32(blob, 8);
        Text = (int)U32(blob, 12);
        Data = (int)U32(blob, 16);
        Bss = (int)U32(blob, 20);
        RSize = (int)U32(blob, 24);
        SSize = (int)U32(blob, 28);
        Image = blob.AsSpan(0x40, Text + Data).ToArray();
        var roff = 0x40 + Text + Data;
        Relocs = ReadRelocs(blob.AsSpan(roff, RSize));
        Symbols = ReadSymbols(blob.AsSpan(roff + RSize, SSize));
        _textSyms = Symbols.Where(s => s.Kind == "text").Select(s => (s.Value, s.Name))
            .OrderBy(t => t.Value).ThenBy(t => t.Name, StringComparer.Ordinal).ToArray();
        SymbolValue = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in Symbols)
            SymbolValue[s.Name] = s.Value; // Python dict comprehension: the last symbol of a name wins
    }

    public uint Base { get; }
    public uint Entry { get; }
    public int Text { get; }
    public int Data { get; }
    public int Bss { get; }
    public int RSize { get; }
    public int SSize { get; }
    public byte[] Image { get; }
    public List<(int Site, int Size)> Relocs { get; }
    /// <summary>Sorted by value (stable, as Python's <c>list.sort</c>).</summary>
    public List<Symbol> Symbols { get; }
    public Dictionary<string, int> SymbolValue { get; }

    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));

    private static List<(int, int)> ReadRelocs(ReadOnlySpan<byte> r)
    {
        var sites = new List<(int, int)>();
        int pos = 0, i = 0;
        while (i + 2 <= r.Length)
        {
            long w = BinaryPrimitives.ReadUInt16BigEndian(r[i..]);
            i += 2;
            if (w == 1)
            {
                w = BinaryPrimitives.ReadUInt32BigEndian(r[i..]);
                i += 4;
            }
            var size = 4;
            if ((w & 1) != 0)
            {
                w &= ~1L;
                size = 2;
            }
            pos += (int)w;
            sites.Add((pos, size));
        }
        return sites;
    }

    private static List<Symbol> ReadSymbols(ReadOnlySpan<byte> s)
    {
        var outList = new List<Symbol>();
        var i = 0;
        while (i + 6 <= s.Length)
        {
            int typ = BinaryPrimitives.ReadUInt16BigEndian(s[i..]);
            var val = (int)BinaryPrimitives.ReadUInt32BigEndian(s[(i + 2)..]);
            i += 6;
            var j = s[i..].IndexOf((byte)0);
            if (j < 0)
                throw new InvalidDataException("unterminated symbol name");
            var name = Encoding.Latin1.GetString(s.Slice(i, j));
            i += j + 1;
            if ((i & 1) != 0)
                i++;
            outList.Add(new Symbol(name, val, typ, SymKinds.GetValueOrDefault(typ, "?")));
        }
        return outList.OrderBy(x => x.Value).ToList(); // OrderBy is stable
    }

    public uint Long(int a) => BinaryPrimitives.ReadUInt32BigEndian(Image.AsSpan(a));

    public int SLong(int a) => BinaryPrimitives.ReadInt32BigEndian(Image.AsSpan(a));

    /// <summary>Name of the text symbol containing <paramref name="addr"/> (null outside text).</summary>
    public string? FuncAt(int addr)
    {
        if (addr >= Text)
            return null;
        string? name = null;
        foreach (var (v, n) in _textSyms)
        {
            if (v > addr)
                break;
            name = n;
        }
        return name;
    }

    /// <summary>{target: [site, …]} for every 32-bit relocated longword, sites in relocation order.</summary>
    public Dictionary<int, List<int>> PointerMap()
    {
        var m = new Dictionary<int, List<int>>();
        foreach (var (site, size) in Relocs)
        {
            if (size != 4)
                continue;
            var t = (int)Long(site);
            if (!m.TryGetValue(t, out var l))
                m[t] = l = [];
            l.Add(site);
        }
        return m;
    }

    /// <summary>The length of the relocated pointer run starting at <paramref name="start"/>.</summary>
    public int RelocRun(int start)
    {
        var sites = Relocs.Where(r => r.Size == 4).Select(r => r.Site).ToHashSet();
        var n = 0;
        while (sites.Contains(start + 4 * n))
            n++;
        return n;
    }
}
