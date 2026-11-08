using System.Buffers.Binary;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// Apple II <c>.woz</c> flux images (WOZ 1 and WOZ 2) to a 140K sector image. The SSI disks these images hold
/// (Phantasie I and II, RDOS 2.1) are 13-sector disks: address field <c>D5 AA B5</c> (Phantasie I from track 1:
/// <c>D4 AA B7</c>), 4-and-4 volume / track / sector / checksum; data field <c>D5 AA AD</c>, 410 + 1 nibbles of
/// 5-and-3 GCR. Each track's bit stream (<c>TMAP</c> quarter-track 4t) is read twice round, so a sector across the
/// splice is still found; the first copy whose address checksum, track number and data checksum all hold is kept.
/// The output is the 4am crack's layout that <see cref="RdosVolume"/> reads: physical sector s of track t at offset
/// <c>t * 4096 + s * 256</c>, slots 13-15 zero. A sector that does not decode stays zero and is listed in
/// <see cref="Result.Missing"/> (on the SSI disks only track 0 sector 12: its address field has no data field, the
/// 16-sector boot sector sits in its place). Format: <c>reverse-engineering/apple2/phantasie2/FORMAT.md</c> section 1.
/// 16-sector (6-and-2) disks are not decoded: no Phantasie disk we know of needs it.
/// </summary>
public static class WozImage
{
    public const int Tracks = 35, SectorsPerTrack = 13;

    public sealed record Result(byte[] Image, IReadOnlyList<(int Track, int Sector)> Missing);

    public static bool IsWoz(byte[] data) =>
        data.Length >= 12 && data[0] == 'W' && data[1] == 'O' && data[2] == 'Z' && data[3] is (byte)'1' or (byte)'2'
        && data[4] == 0xFF && data[5] == 0x0A && data[6] == 0x0D && data[7] == 0x0A;

    /// <summary>The 5-and-3 disk bytes; a byte's index is its 5-bit value.</summary>
    private static readonly byte[] Gcr53 =
    [
        0xAB, 0xAD, 0xAE, 0xAF, 0xB5, 0xB6, 0xB7, 0xBA, 0xBB, 0xBD, 0xBE, 0xBF, 0xD6, 0xD7, 0xDA, 0xDB,
        0xDD, 0xDE, 0xDF, 0xEA, 0xEB, 0xED, 0xEE, 0xEF, 0xF5, 0xF6, 0xF7, 0xFA, 0xFB, 0xFD, 0xFE, 0xFF,
    ];

    private static readonly sbyte[] Gcr53Inverse = BuildInverse();

    private static sbyte[] BuildInverse()
    {
        var inv = new sbyte[256];
        Array.Fill(inv, (sbyte)-1);
        for (var i = 0; i < Gcr53.Length; i++)
            inv[Gcr53[i]] = (sbyte)i;
        return inv;
    }

    /// <summary>
    /// Decode a <c>.woz</c> to a 140K 13-sector image; null when the file is not a WOZ 1 / 2 image we can read or no
    /// 13-sector data sector decodes at all.
    /// </summary>
    public static Result? TryDecode(byte[] data)
    {
        try
        {
            return Decode(data);
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }

    private static Result? Decode(byte[] data)
    {
        if (!IsWoz(data))
            return null;
        var version = data[3] - '0';
        int tmap = -1, trks = -1, trksSize = 0;
        for (var p = 12; p + 8 <= data.Length;)
        {
            var id = data.AsSpan(p, 4);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p + 4));
            if (size < 0 || p + 8 + (long)size > data.Length)
                break;
            if (id.SequenceEqual("TMAP"u8))
                tmap = p + 8;
            else if (id.SequenceEqual("TRKS"u8))
                (trks, trksSize) = (p + 8, size);
            p += 8 + size;
        }
        if (tmap < 0 || trks < 0)
            return null;

        var image = new byte[RdosVolume.ImageSize];
        var missing = new List<(int, int)>();
        var found = 0;
        for (var t = 0; t < Tracks; t++)
        {
            var index = data[tmap + t * 4];
            var bits = index == 0xFF ? null : TrackBits(data, version, trks, trksSize, index);
            var sectors = bits is null ? [] : DecodeTrack(bits.Value.Bytes, bits.Value.Count, t);
            for (var s = 0; s < SectorsPerTrack; s++)
            {
                if (sectors.TryGetValue(s, out var sector))
                {
                    sector.CopyTo(image, t * 4096 + s * 256);
                    found++;
                }
                else
                    missing.Add((t, s));
            }
        }
        return found == 0 ? null : new Result(image, missing);
    }

    /// <summary>A track's bit stream (most significant bit first) and its bit count.</summary>
    private static (byte[] Bytes, int Count)? TrackBits(byte[] data, int version, int trks, int trksSize, int index)
    {
        if (version == 1)
        {
            // WOZ 1: 6656-byte records, 6646 bytes of bits, then bytes used (u16), bit count (u16).
            const int record = 6656;
            if ((index + 1) * record > trksSize)
                return null;
            var at = trks + index * record;
            int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at + 6648));
            return count is > 0 and <= 6646 * 8 ? (data[at..(at + 6646)], count) : null;
        }
        // WOZ 2: 160 TRK entries of 8 bytes (first 512-byte block u16, block count u16, bit count u32).
        if (index >= 160 || (index + 1) * 8 > trksSize)
            return null;
        var e = trks + index * 8;
        int block = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(e)),
            blocks = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(e + 2));
        var bitCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(e + 4));
        long start = block * 512L, length = blocks * 512L;
        if (block == 0 || bitCount == 0 || bitCount > length * 8 || start + length > data.Length)
            return null;
        return (data[(int)start..(int)(start + length)], (int)bitCount);
    }

    /// <summary>
    /// The disk bytes of two turns of the track, as the drive's shift register gives them: bits are shifted in until
    /// the top bit is set (leading zero bits, the sync gaps, fall away).
    /// </summary>
    private static byte[] Nibbles(byte[] bits, int count)
    {
        var output = new List<byte>(count / 4);
        var current = 0;
        for (var i = 0; i < count * 2; i++)
        {
            var b = i % count;
            current = ((current << 1) | ((bits[b >> 3] >> (7 - (b & 7))) & 1)) & 0xFF;
            if ((current & 0x80) != 0)
            {
                output.Add((byte)current);
                current = 0;
            }
        }
        return output.ToArray();
    }

    private static int FourAndFour(byte a, byte b) => ((a << 1) | 1) & b;

    private const int DataNibbles = 411, SearchWindow = 60;

    /// <summary>Every 13-sector sector of track <paramref name="track"/> that decodes, by sector number.</summary>
    private static Dictionary<int, byte[]> DecodeTrack(byte[] bits, int count, int track)
    {
        var nb = Nibbles(bits, count);
        var result = new Dictionary<int, byte[]>();
        var values = new int[DataNibbles];
        for (var i = 0; i + 11 <= nb.Length; i++)
        {
            if (nb[i + 1] != 0xAA || nb[i] is not (0xD5 or 0xD4) || nb[i + 2] is not (0xB5 or 0xB7))
                continue;
            int vol = FourAndFour(nb[i + 3], nb[i + 4]), trk = FourAndFour(nb[i + 5], nb[i + 6]),
                sec = FourAndFour(nb[i + 7], nb[i + 8]), chk = FourAndFour(nb[i + 9], nb[i + 10]);
            if ((vol ^ trk ^ sec) != chk || trk != track || sec >= SectorsPerTrack || result.ContainsKey(sec))
                continue;
            for (var j = i + 11; j < i + SearchWindow && j + 3 + DataNibbles <= nb.Length; j++)
            {
                if (nb[j] != 0xD5 || nb[j + 1] != 0xAA || nb[j + 2] != 0xAD)
                    continue;
                if (TryDataField(nb.AsSpan(j + 3, DataNibbles), values))
                    result[sec] = FiveAndThree(values);
                break;
            }
        }
        return result;
    }

    /// <summary>
    /// The 410 values of a data field, un-chained (each disk value is XORed with the one before), into
    /// <paramref name="values"/>; false on a byte outside the 5-and-3 table or a checksum that does not end at 0.
    /// </summary>
    private static bool TryDataField(ReadOnlySpan<byte> field, int[] values)
    {
        var previous = 0;
        for (var k = 0; k < DataNibbles; k++)
        {
            int v = Gcr53Inverse[field[k]];
            if (v < 0)
                return false;
            previous ^= v;
            values[k] = previous;
        }
        return previous == 0;
    }

    /// <summary>
    /// 410 five-bit values to 256 bytes (DOS 3.2's layout): values 0-153 hold the low 3 bits, 154-409 the top 5.
    /// Bytes go in 51 groups of five (5g..5g+4) plus byte 255. With A = v[103 + g], B = v[52 + g], C = v[1 + g]:
    /// byte 5g+k's top bits are v[154 + 51k + 50 - g]; its low bits are A >> 2, B >> 2, C >> 2 for k = 0, 1, 2, and
    /// for k = 3 / 4 bit 1 / bit 0 of A, B, C (A highest). Byte 255 is v[409] over v[0]'s low 3 bits.
    /// </summary>
    private static byte[] FiveAndThree(int[] v)
    {
        var output = new byte[256];
        for (var g = 0; g < 51; g++)
        {
            int a = v[103 + g], b = v[52 + g], c = v[1 + g];
            Span<int> low =
            [
                a >> 2, b >> 2, c >> 2,
                ((a >> 1) & 1) << 2 | ((b >> 1) & 1) << 1 | ((c >> 1) & 1),
                (a & 1) << 2 | (b & 1) << 1 | (c & 1),
            ];
            for (var k = 0; k < 5; k++)
                output[5 * g + k] = (byte)((v[154 + 51 * k + 50 - g] << 3) | low[k]);
        }
        output[255] = (byte)((v[409] << 3) | (v[0] & 7));
        return output;
    }
}
