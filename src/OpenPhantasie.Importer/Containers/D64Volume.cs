using System.Text;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// A Commodore 1541 disk in a <c>.d64</c> (35 tracks, 683 sectors of 256 bytes; the 175,531-byte form with error bytes
/// too): the directory chain from track 18 sector 1 (8 entries of 32 bytes per sector), each file a sector chain (bytes
/// 0 / 1 = next track / sector; track 0 = last sector, byte 1 then = the index of its last used byte). A REL file reads
/// as its data-block chain (its records back to back; the record length is the directory entry's byte 23). Port of
/// <c>tools/scripts/extract_c64_d64.py</c> (format: <c>reverse-engineering/c64/phantasie1/FORMAT.md</c> section 1).
/// A PRG file keeps its 2-byte load address, as the script writes it.
/// </summary>
public sealed class D64Volume
{
    public const int ImageSize = 174_848;
    public const int ImageSizeWithErrors = 175_531;
    public const int Sectors = 683;
    private const int Tracks = 35, DirectoryTrack = 18, EntrySize = 32;

    /// <summary>A directory entry: its name (PETSCII printable as is, others as <c>{xx}</c>), type, start, REL record length.</summary>
    public sealed record Entry(string Name, string Type, int Track, int Sector, int Blocks, int RecordLength);

    private readonly byte[] _image;

    private D64Volume(byte[] image, List<Entry> entries, string diskName)
    {
        _image = image;
        Files = entries;
        DiskName = diskName;
    }

    public IReadOnlyList<Entry> Files { get; }

    /// <summary>The disk name in the BAM sector (18/0 +0x90).</summary>
    public string DiskName { get; }

    public static int SectorsPerTrack(int track) => track <= 17 ? 21 : track <= 24 ? 19 : track <= 30 ? 18 : 17;

    /// <summary>Byte offset of (track, sector) in the image; -1 outside the 35 tracks.</summary>
    public static int Offset(int track, int sector)
    {
        if (track is < 1 or > Tracks || sector < 0 || sector >= SectorsPerTrack(track))
            return -1;
        var n = 0;
        for (var t = 1; t < track; t++)
            n += SectorsPerTrack(t);
        return (n + sector) * 256;
    }

    private static readonly string[] Types = ["del", "seq", "prg", "usr", "rel"];

    /// <summary>
    /// The volume, or null when the image is not a 35-track D64 with a plausible directory (BAM byte 0 = 18, a directory
    /// chain inside the disk without loops, at least one file of a known type starting inside the disk).
    /// </summary>
    public static D64Volume? TryOpen(byte[] data)
    {
        if (data.Length != ImageSize && data.Length != ImageSizeWithErrors)
            return null;
        var image = data.Length == ImageSize ? data : data[..ImageSize];
        var bam = Offset(DirectoryTrack, 0);
        if (image[bam] != DirectoryTrack)
            return null;
        var entries = new List<Entry>();
        var seen = new HashSet<(int, int)>();
        int track = DirectoryTrack, sector = 1;
        while (track != 0)
        {
            var at = Offset(track, sector);
            if (at < 0 || !seen.Add((track, sector)))
                return null;
            for (var i = 0; i < 8; i++)
            {
                var e = image.AsSpan(at + i * EntrySize, EntrySize);
                var type = e[2];
                // Scratched / empty slots, DEL entries (no data) and odd types are left out, not fatal.
                if ((type & 0x07) is 0 or > 4 || Offset(e[3], e[4]) < 0)
                    continue;
                entries.Add(new Entry(Name(e.Slice(5, 16)), Types[type & 0x07], e[3], e[4], e[30] | e[31] << 8, e[23]));
            }
            track = image[at];
            sector = image[at + 1];
        }
        return entries.Count == 0 ? null : new D64Volume(image, entries, Name(image.AsSpan(bam + 0x90, 16)));
    }

    /// <summary>A PETSCII directory name: shifted-space padding (0xA0) dropped, '/' made '_' (a path separator here).</summary>
    public static string Name(ReadOnlySpan<byte> raw)
    {
        var end = raw.Length;
        while (end > 0 && raw[end - 1] == 0xA0)
            end--;
        var sb = new StringBuilder();
        foreach (var b in raw[..end])
            sb.Append(b is >= 0x20 and < 0x7F ? (b == '/' ? '_' : (char)b) : $"{{{b:x2}}}");
        return sb.ToString();
    }

    /// <summary>The file's bytes (the data blocks of its sector chain).</summary>
    public byte[] Read(Entry entry)
    {
        var output = new List<byte>();
        var seen = new HashSet<(int, int)>();
        int track = entry.Track, sector = entry.Sector;
        while (track != 0)
        {
            var at = Offset(track, sector);
            if (at < 0)
                throw new InvalidDataException($"{entry.Name}: chain leaves the disk at {track}/{sector}");
            if (!seen.Add((track, sector)))
                throw new InvalidDataException($"{entry.Name}: sector loop at {track}/{sector}");
            int next = _image[at], nextSector = _image[at + 1];
            var block = _image.AsSpan(at + 2, next != 0 ? 254 : Math.Max(0, nextSector - 1));
            output.AddRange(block.ToArray());
            track = next;
            sector = nextSector;
        }
        return [.. output];
    }
}

/// <summary>
/// A 1541 <c>.g64</c> (GCR-1541) to the 683 standard sectors of a D64. Port of <c>tools/scripts/c64_g64.py</c>: every full
/// track 1..35 (half-track index 2·(t−1)) is read as a circular bit stream; after each sync (≥ 10 one bits) a GCR block
/// is decoded: a header (0x08, checksum, sector, track, id2, id1) is matched with the next data block (0x07, 256 bytes,
/// checksum). A sector is kept when its header's track is the track read and both checksums are good; the first good
/// copy wins. Missing sectors read as zeros and are listed. Copy protection (extra tracks, half tracks) is ignored.
/// </summary>
public static class G64Image
{
    private static readonly int[] Gcr = BuildGcr();

    private static int[] BuildGcr()
    {
        var t = Enumerable.Repeat(-1, 32).ToArray();
        int[] codes = [0x0A, 0x0B, 0x12, 0x13, 0x0E, 0x0F, 0x16, 0x17, 0x09, 0x19, 0x1A, 0x1B, 0x0D, 0x1D, 0x1E, 0x15];
        for (var i = 0; i < 16; i++)
            t[codes[i]] = i;
        return t;
    }

    public static bool IsG64(byte[] data) => data.Length > 12 && data.AsSpan(0, 8).SequenceEqual("GCR-1541"u8);

    public sealed record Decoded(byte[] Image, IReadOnlyList<(int Track, int Sector)> Missing);

    public static Decoded Decode(byte[] data)
    {
        if (!IsG64(data))
            throw new InvalidDataException("not a G64 image");
        int trackCount = data[9];
        var d64 = new byte[D64Volume.ImageSize];
        var missing = new List<(int, int)>();
        for (var track = 1; track <= 35; track++)
        {
            var index = 2 * (track - 1);
            var found = new Dictionary<int, byte[]>();
            if (index < trackCount && 12 + 4 * index + 4 <= data.Length)
            {
                var o = (int)BitConverter.ToUInt32(data, 12 + 4 * index);
                if (o > 0 && o + 2 <= data.Length)
                {
                    var length = Math.Min(data[o] | data[o + 1] << 8, data.Length - o - 2);
                    found = DecodeTrack(data.AsSpan(o + 2, length), track);
                }
            }
            for (var s = 0; s < D64Volume.SectorsPerTrack(track); s++)
                if (found.TryGetValue(s, out var sec))
                    sec.CopyTo(d64, D64Volume.Offset(track, s));
                else
                    missing.Add((track, s));
        }
        return new Decoded(d64, missing);
    }

    private static Dictionary<int, byte[]> DecodeTrack(ReadOnlySpan<byte> trackBytes, int track)
    {
        var found = new Dictionary<int, byte[]>();
        var n = trackBytes.Length * 8;
        if (n == 0)
            return found;
        // The bits, the track twice (blocks wrap around the index).
        var bits = new byte[2 * n];
        for (var i = 0; i < n; i++)
            bits[i] = bits[i + n] = (byte)((trackBytes[i >> 3] >> (7 - (i & 7))) & 1);

        int? pending = null;
        var pos = 0;
        while (pos < n)
        {
            // The next run of at least 10 one bits starting before the track's end.
            var j = pos;
            var run = 0;
            var start = -1;
            for (; j < bits.Length; j++)
            {
                if (bits[j] == 1)
                {
                    run++;
                    if (run == 10)
                    {
                        start = j - 9;
                        break;
                    }
                }
                else
                    run = 0;
            }
            if (start < 0 || start >= n)
                break;
            var k = start;
            while (k < bits.Length && bits[k] == 1)
                k++;
            pos = k;

            var first = GcrDecode(bits, k, 1);
            if (first is null)
            {
                pending = null;
                continue;
            }
            if (first[0] == 0x08)
            {
                var h = GcrDecode(bits, k, 6);
                pending = h is not null && h[1] == (h[2] ^ h[3] ^ h[4] ^ h[5]) && h[3] == track ? h[2] : null;
            }
            else if (first[0] == 0x07 && pending is { } sector)
            {
                var d = GcrDecode(bits, k, 258);
                if (d is not null)
                {
                    var chk = 0;
                    for (var i = 1; i < 257; i++)
                        chk ^= d[i];
                    if (chk == d[257] && !found.ContainsKey(sector))
                        found[sector] = d[1..257];
                }
                pending = null;
            }
        }
        return found;
    }

    private static byte[]? GcrDecode(byte[] bits, int pos, int count)
    {
        if (pos + 10 * count > bits.Length)
            return null;
        var output = new byte[count];
        for (var i = 0; i < count; i++)
        {
            int hi = 0, lo = 0;
            var p = pos + 10 * i;
            for (var b = 0; b < 5; b++)
            {
                hi = hi << 1 | bits[p + b];
                lo = lo << 1 | bits[p + 5 + b];
            }
            if (Gcr[hi] < 0 || Gcr[lo] < 0)
                return null;
            output[i] = (byte)(Gcr[hi] << 4 | Gcr[lo]);
        }
        return output;
    }
}
