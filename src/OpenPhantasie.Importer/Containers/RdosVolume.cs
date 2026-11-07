using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// SSI's RDOS 2.1 (Apple II, 13-sector) inside a 140K DOS-order <c>.dsk</c>, as Phantasie II's 4am crack keeps it.
/// Port of <c>tools/scripts/a2_rdos.py</c> (format: <c>reverse-engineering/apple2/phantasie2/FORMAT.md</c> section 1):
/// logical sector n = <c>.dsk</c> track n / 13, sector n % 13; catalog = track 1 sectors 0-10, 32-byte entries ended
/// by a first byte 0 (name 0-23 high-bit ASCII, 24 type, 25 sector count, 26 load, 28 length, 30 first sector); a
/// deleted entry starts with 0x80. Files are contiguous.
/// </summary>
public sealed class RdosVolume
{
    public const int ImageSize = 143_360;
    private const int SectorsPerTrack = 13, TrackBytes = 16 * 256, CatalogTrack = 1, CatalogSectors = 11, EntrySize = 32;

    public sealed record Entry(string Name, char Type, int Sectors, int Load, int Length, int Start, bool Deleted);

    private readonly byte[] _image;

    private RdosVolume(byte[] image, List<Entry> entries)
    {
        _image = image;
        Entries = entries;
    }

    /// <summary>Every catalog entry, deleted ones included (as <c>RdosDisk.files</c>).</summary>
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>The live files (deleted entries left out).</summary>
    public IEnumerable<Entry> Files => Entries.Where(e => !e.Deleted);

    /// <summary>
    /// The volume, or null when the image is not a 140K <c>.dsk</c> with a plausible RDOS catalog (every entry of a
    /// known type, inside the disk; at least one file).
    /// </summary>
    public static RdosVolume? TryOpen(byte[] image)
    {
        if (image.Length != ImageSize)
            return null;
        var entries = new List<Entry>();
        const int logicalSectors = 35 * SectorsPerTrack;
        var ended = false;
        for (var s = 0; s < CatalogSectors && !ended; s++)
        {
            var sec = Sector(image, CatalogTrack * SectorsPerTrack + s);
            for (var e = 0; e < 256 / EntrySize; e++)
            {
                var b = sec.Slice(e * EntrySize, EntrySize);
                if (b[0] == 0)
                {
                    ended = true;
                    break;
                }
                var deleted = b[0] == 0x80;
                var name = new StringBuilder();
                foreach (var c in b[..24])
                    name.Append((char)(c & 0x7F));
                var type = (char)(b[24] & 0x7F);
                int load = BinaryPrimitives.ReadUInt16LittleEndian(b[26..]), length = BinaryPrimitives.ReadUInt16LittleEndian(b[28..]),
                    start = BinaryPrimitives.ReadUInt16LittleEndian(b[30..]);
                if (!deleted && (type is not ('A' or 'B' or 'T') || b[25] == 0 || start + b[25] > logicalSectors))
                    return null;
                entries.Add(new Entry(name.ToString().TrimEnd(), type, b[25], load, length, start, deleted));
            }
        }
        return entries.Count == 0 ? null : new RdosVolume(image, entries);
    }

    private static ReadOnlySpan<byte> Sector(byte[] image, int n) =>
        image.AsSpan((n / SectorsPerTrack) * TrackBytes + (n % SectorsPerTrack) * 256, 256);

    /// <summary>
    /// A file's bytes: its sectors, cut to <c>length</c> when that is non-zero and fits (as <c>RdosDisk.read</c>).
    /// </summary>
    public byte[] Read(Entry e)
    {
        var data = new byte[e.Sectors * 256];
        for (var i = 0; i < e.Sectors; i++)
            Sector(_image, e.Start + i).CopyTo(data.AsSpan(i * 256));
        return e.Length != 0 && e.Length <= data.Length ? data[..e.Length] : data;
    }

    /// <summary>The size <see cref="Read"/> returns, without reading.</summary>
    public static int SizeOf(Entry e) => e.Length != 0 && e.Length <= e.Sectors * 256 ? e.Length : e.Sectors * 256;
}
