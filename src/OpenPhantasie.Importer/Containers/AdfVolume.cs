using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// Read-only AmigaDOS floppy (<c>.adf</c>, 880 KB, OFS or FFS). Replaces amitools <c>xdftool</c> for the Amiga I and
/// III disks. Paths keep the disk's case (<c>PHANT/tamb.dat</c>, <c>PhantasieIII/D/m0</c>); the volume name is not
/// part of the path.
/// </summary>
public sealed class AdfVolume
{
    private const int BlockSize = 512;
    private const int TableSize = 72; // (512 / 4) - 56
    private readonly byte[] _raw;
    private readonly bool _ffs;

    private AdfVolume(byte[] raw, bool ffs)
    {
        _raw = raw;
        _ffs = ffs;
    }

    public string VolumeName { get; private set; } = "";

    public static AdfVolume? TryOpen(byte[] raw)
    {
        if (raw.Length is not (901_120 or 1_802_240))
            return null;
        if (raw[0] != 'D' || raw[1] != 'O' || raw[2] != 'S' || raw[3] > 7)
            return null; // not a DOS disk (NDOS / custom track loader)
        var v = new AdfVolume(raw, (raw[3] & 1) != 0);
        var rootBlock = raw.Length / BlockSize / 2;
        if (v.Long(rootBlock, 0) != 2 || v.Long(rootBlock, BlockSize - 4) != 1)
            return null;
        v.VolumeName = v.BcplName(rootBlock);
        v._root = rootBlock;
        return v;
    }

    private int _root;

    public sealed record Entry(string Path, int HeaderBlock, int Size);

    public IEnumerable<Entry> Files() => Walk(_root, "", 0);

    public byte[] Read(Entry e)
    {
        var output = new byte[e.Size];
        var written = 0;
        var header = e.HeaderBlock;
        var guard = 0;
        while (header != 0 && written < e.Size && guard++ < 2000)
        {
            var count = Long(header, 8);
            for (var i = 0; i < count && i < TableSize && written < e.Size; i++)
            {
                var block = Long(header, 24 + (TableSize - 1 - i) * 4);
                if (block <= 0 || (block + 1) * BlockSize > _raw.Length)
                    return output[..written];
                var (offset, len) = _ffs ? (0, BlockSize) : (24, Long(block, 12));
                len = Math.Min(len, e.Size - written);
                _raw.AsSpan(block * BlockSize + offset, len).CopyTo(output.AsSpan(written));
                written += len;
            }
            header = Long(header, BlockSize - 8); // extension block
        }
        return written == e.Size ? output : output[..written];
    }

    private IEnumerable<Entry> Walk(int dirBlock, string prefix, int depth)
    {
        if (depth > 8)
            yield break;
        var blocks = new List<int>();
        for (var i = 0; i < TableSize; i++)
        {
            var b = Long(dirBlock, 24 + i * 4);
            var guard = 0;
            while (b > 0 && (b + 1) * BlockSize <= _raw.Length && guard++ < 1000)
            {
                blocks.Add(b);
                b = Long(b, BlockSize - 16); // hash chain
            }
        }
        foreach (var b in blocks.OrderBy(x => x))
        {
            if (Long(b, 0) != 2)
                continue;
            var name = BcplName(b);
            var path = prefix.Length == 0 ? name : prefix + "/" + name;
            var secType = Long(b, BlockSize - 4);
            if (secType == 2)
            {
                foreach (var sub in Walk(b, path, depth + 1))
                    yield return sub;
            }
            else if (secType == -3)
                yield return new Entry(path, b, Long(b, BlockSize - 188));
        }
    }

    private int Long(int block, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(_raw.AsSpan(block * BlockSize + offset, 4));

    private string BcplName(int block)
    {
        var at = block * BlockSize + BlockSize - 80;
        var len = Math.Min((int)_raw[at], 30);
        return Encoding.Latin1.GetString(_raw, at + 1, len);
    }
}
