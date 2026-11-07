using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// Read-only FAT12 file system: MS-DOS / Atari ST floppies (<c>.ima</c>, <c>.img</c>, <c>.st</c>, MSX <c>.dsk</c>;
/// little-endian BPB at byte 11) and Human68k floppies (X68000, the payload of a <c>.dim</c>; big-endian BPB at 0x12,
/// 1024-byte sectors, names of 8 + 10 + 3 characters). Replaces mtools and <c>x68k_dim.py</c>'s <c>Human68kFat</c>.
/// </summary>
public sealed class Fat12Volume
{
    private readonly byte[] _raw;
    private readonly int _bps, _spc, _rootStart, _rootSectors, _dataStart;
    private readonly byte[] _fat;
    private readonly bool _human68k;

    private Fat12Volume(byte[] raw, int bps, int spc, int reserved, int fats, int rootEntries, int spf, bool human68k)
    {
        _raw = raw;
        _bps = bps;
        _spc = spc;
        _human68k = human68k;
        _rootStart = reserved + fats * spf;
        _rootSectors = rootEntries * 32 / bps;
        _dataStart = _rootStart + _rootSectors;
        _fat = raw.AsSpan(reserved * bps, spf * bps).ToArray();
    }

    /// <summary>An MS-DOS / Atari ST image. Disks without a usable BPB (DOS 1.x) fall back to the standard geometry for
    /// their size. Returns null when the bytes don't look like FAT12.</summary>
    public static Fat12Volume? TryOpenDos(byte[] raw)
    {
        if (raw.Length < 512 * 4)
            return null;
        int bps = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(11));
        int spc = raw[13];
        int reserved = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(14));
        int fats = raw[16];
        int rootEntries = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(17));
        int spf = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(22));
        var valid = bps is 512 or 1024 && spc is 1 or 2 or 4 && reserved >= 1 && fats is 1 or 2 && rootEntries is > 0 and <= 512
                    && spf is > 0 and < 16 && rootEntries * 32 % bps == 0;
        if (!valid)
        {
            // Standard geometries by size (DOS 1.x boot sectors carry no BPB).
            (bps, spc, reserved, fats, rootEntries, spf) = raw.Length switch
            {
                163_840 => (512, 1, 1, 2, 64, 1),
                184_320 => (512, 1, 1, 2, 64, 2),
                327_680 => (512, 2, 1, 2, 112, 1),
                368_640 => (512, 2, 1, 2, 112, 2),
                737_280 => (512, 2, 1, 2, 112, 3),
                _ => (0, 0, 0, 0, 0, 0),
            };
            if (bps == 0)
                return null;
            var media = raw[reserved * bps];
            if (media < 0xF0)
                return null;
        }
        if ((reserved + fats * spf) * bps + rootEntries * 32 > raw.Length)
            return null;
        return new Fat12Volume(raw, bps, spc, reserved, fats, rootEntries, spf, human68k: false);
    }

    /// <summary>A Human68k (X68000) volume: the raw 2HD bytes (<see cref="DimImage.ToRaw"/>).</summary>
    public static Fat12Volume? TryOpenHuman68k(byte[] raw)
    {
        if (raw.Length < 1024 * 8)
            return null;
        var b = raw.AsSpan(0x12);
        int bps = BinaryPrimitives.ReadUInt16BigEndian(b);
        int spc = b[2];
        int fats = b[3];
        int reserved = BinaryPrimitives.ReadUInt16BigEndian(b[4..]);
        int rootEntries = BinaryPrimitives.ReadUInt16BigEndian(b[6..]);
        int spf = b[11];
        if (bps != 1024 || spc < 1 || fats is < 1 or > 2 || rootEntries == 0 || spf == 0)
            return null;
        return new Fat12Volume(raw, bps, spc, reserved, fats, rootEntries, spf, human68k: true);
    }

    public sealed record Entry(string Path, int FirstCluster, int Size, byte Attributes);

    /// <summary>Every file (not directories or labels), depth first, with '/'-separated paths.</summary>
    public IEnumerable<Entry> Files()
    {
        var root = _raw.AsSpan(_rootStart * _bps, _rootSectors * _bps).ToArray();
        return Walk(root, "", 0);
    }

    public byte[] Read(Entry e)
    {
        if (e.Size == 0 || e.FirstCluster < 2)
            return [];
        var chain = ReadChain(e.FirstCluster);
        return chain.Length >= e.Size ? chain.AsSpan(0, e.Size).ToArray() : chain;
    }

    private IEnumerable<Entry> Walk(byte[] dir, string prefix, int depth)
    {
        if (depth > 8)
            yield break;
        for (var i = 0; i + 32 <= dir.Length; i += 32)
        {
            var e = dir.AsSpan(i, 32);
            if (e[0] == 0)
                yield break;
            if (e[0] == 0xE5)
                continue;
            var attr = e[11];
            if ((attr & 0x0F) == 0x0F || (attr & 0x08) != 0)
                continue; // LFN slot or volume label
            var name = Name(e);
            if (name is "." or ".." || name.Length == 0)
                continue;
            int cluster = BinaryPrimitives.ReadUInt16LittleEndian(e[26..]);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(e[28..]);
            var path = prefix.Length == 0 ? name : prefix + "/" + name;
            if ((attr & 0x10) != 0)
            {
                if (cluster >= 2)
                    foreach (var sub in Walk(ReadChain(cluster), path, depth + 1))
                        yield return sub;
                continue;
            }
            yield return new Entry(path, cluster, size, attr);
        }
    }

    private string Name(ReadOnlySpan<byte> e)
    {
        var baseName = _human68k ? [.. e[..8], .. e[12..22]] : e[..8].ToArray();
        var stem = Decode(baseName).TrimEnd('\0').TrimEnd(' ');
        var ext = Decode(e[8..11].ToArray()).TrimEnd(' ');
        if (stem.Length > 0 && stem[0] == '\u0005')
            stem = "å" + stem[1..];
        return ext.Length > 0 ? stem + "." + ext : stem;
    }

    private string Decode(byte[] bytes)
    {
        if (_human68k)
        {
            try { return ShiftJis.GetString(bytes); }
            catch (DecoderFallbackException) { }
        }
        return Encoding.Latin1.GetString(bytes);
    }

    private static Encoding ShiftJis
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932);
        }
    }

    private int FatEntry(int n)
    {
        var off = n * 3 / 2;
        if (off + 1 >= _fat.Length)
            return 0xFFF;
        var v = _fat[off] | (_fat[off + 1] << 8);
        return (n & 1) != 0 ? v >> 4 : v & 0xFFF;
    }

    private byte[] ReadChain(int first)
    {
        var clusterBytes = _spc * _bps;
        var output = new List<byte>();
        var seen = new HashSet<int>();
        for (var c = first; c >= 2 && c < 0xFF0 && seen.Add(c); c = FatEntry(c))
        {
            var start = (_dataStart + (c - 2) * _spc) * _bps;
            if (start + clusterBytes > _raw.Length)
                break;
            output.AddRange(_raw.AsSpan(start, clusterBytes).ToArray());
        }
        return [.. output];
    }
}
