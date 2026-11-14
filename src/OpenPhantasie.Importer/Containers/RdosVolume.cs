using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// SSI's RDOS (Apple II) inside a 140K <c>.dsk</c>: RDOS 2.1 (13-sector; Phantasie I, II v1.0) as Phantasie II's 4am
/// crack keeps it (and as <see cref="WozImage"/> decodes the original disks' <c>.woz</c> images), and RDOS 3.3
/// (16-sector; II v2.1, III) in a DOS-order <c>.dsk</c>.
/// Port of <c>tools/scripts/a2_rdos.py</c> (format: <c>reverse-engineering/apple2/phantasie2/FORMAT.md</c> section 1):
/// logical sector n = <c>.dsk</c> track n / 13, sector n % 13; catalog = track 1 sectors 0-10, 32-byte entries ended
/// by a first byte 0 (name 0-23 high-bit ASCII, 24 type, 25 sector count, 26 load, 28 length, 30 first sector); a
/// deleted entry starts with 0x80. Files are contiguous.
/// The Memorial Set (GOG / Steam) ships the same disks in DOS 3.3 sector order: its sector s is the 4am image's sector
/// <c>[0,13,11,9,7,5,3,1,14,12,10,8,6,4,2,15][s]</c> on every track (s166). Both orders share catalog sector 0, so
/// <see cref="TryOpen"/> reads the image both ways and keeps the catalog with more entries.
/// RDOS 3.3 (s211): logical sector n = track n / 16, DOS-order slot <c>[0,14,13,...,1,15][n % 16]</c> (ProDOS order
/// within a track); catalog = logical sectors 16-31, same entries. Its first entry, <c>RDOS 3.3 COPYRIGHT 1986</c>,
/// picks this layout first: a 13-sector read of these disks also finds a plausible (partial) catalog.
/// </summary>
public sealed class RdosVolume
{
    public const int ImageSize = 143_360;
    private const int SectorsPerTrack = 13, TrackBytes = 16 * 256, CatalogTrack = 1, CatalogSectors = 11, EntrySize = 32;

    public sealed record Entry(string Name, char Type, int Sectors, int Load, int Length, int Start, bool Deleted);

    private readonly byte[] _image;

    private RdosVolume(byte[] image, List<Entry> entries, bool rdos33)
    {
        _image = image;
        Entries = entries;
        IsRdos33 = rdos33;
    }

    /// <summary>True for a 16-sector RDOS 3.3 disk, false for 13-sector RDOS 2.1.</summary>
    public bool IsRdos33 { get; }

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
        if (TryOpenInOrder(image, rdos33: true) is { } r33 && r33.Entries[0].Name.StartsWith(Rdos33Mark, StringComparison.Ordinal))
            return r33;
        var asIs = TryOpenInOrder(image);
        var remapped = TryOpenInOrder(FromDos33Order(image));
        return remapped is not null && (asIs is null || remapped.Entries.Count > asIs.Entries.Count) ? remapped : asIs;
    }

    /// <summary>DOS 3.3 → the 4am image's order (<c>tools/scripts/a2_rdos.py</c> <c>from_dos33_order</c>).</summary>
    internal static readonly int[] Dos33ToImage = [0, 13, 11, 9, 7, 5, 3, 1, 14, 12, 10, 8, 6, 4, 2, 15];

    /// <summary>RDOS 3.3 sector n % 16 → DOS-order <c>.dsk</c> slot (<c>a2_rdos.py</c> <c>RDOS33_SLOT</c>).</summary>
    internal static readonly int[] Rdos33Slot = [0, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 15];

    private const string Rdos33Mark = "RDOS 3.3";

    public static byte[] FromDos33Order(byte[] image)
    {
        var o = new byte[image.Length];
        for (var t = 0; t < image.Length / TrackBytes; t++)
            for (var s = 0; s < 16; s++)
                image.AsSpan(t * TrackBytes + s * 256, 256).CopyTo(o.AsSpan(t * TrackBytes + Dos33ToImage[s] * 256));
        return o;
    }

    private static RdosVolume? TryOpenInOrder(byte[] image, bool rdos33 = false)
    {
        var entries = new List<Entry>();
        int perTrack = rdos33 ? 16 : SectorsPerTrack, logicalSectors = 35 * perTrack;
        var ended = false;
        for (var s = 0; s < (rdos33 ? 16 : CatalogSectors) && !ended; s++)
        {
            var sec = Sector(image, CatalogTrack * perTrack + s, rdos33);
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
        return entries.Count == 0 ? null : new RdosVolume(image, entries, rdos33);
    }

    private static ReadOnlySpan<byte> Sector(byte[] image, int n, bool rdos33) => rdos33
        ? image.AsSpan((n / 16) * TrackBytes + Rdos33Slot[n % 16] * 256, 256)
        : image.AsSpan((n / SectorsPerTrack) * TrackBytes + (n % SectorsPerTrack) * 256, 256);

    /// <summary>
    /// A file's bytes: its sectors, cut to <c>length</c> when that is non-zero and fits (as <c>RdosDisk.read</c>).
    /// </summary>
    public byte[] Read(Entry e)
    {
        var data = new byte[e.Sectors * 256];
        for (var i = 0; i < e.Sectors; i++)
            Sector(_image, e.Start + i, IsRdos33).CopyTo(data.AsSpan(i * 256));
        return e.Length != 0 && e.Length <= data.Length ? data[..e.Length] : data;
    }

    /// <summary>The size <see cref="Read"/> returns, without reading.</summary>
    public static int SizeOf(Entry e) => e.Length != 0 && e.Length <= e.Sectors * 256 ? e.Length : e.Sectors * 256;
}
