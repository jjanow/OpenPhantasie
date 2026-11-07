using System.Buffers.Binary;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// Sharp X68000 <c>.dim</c> (DIFC header): media byte, one presence flag per track, <c>DIFC HEADER</c> at 0xAB,
/// track data from 0x100 with absent tracks left out. Port of <c>tools/scripts/x68k_dim.py</c> <c>dim_to_raw</c>.
/// </summary>
public static class DimImage
{
    private const int Sector = 1024, SectorsPerTrack = 8, Tracks = 154;

    public static bool IsDim(byte[] data) =>
        data.Length > 0x100 && data.AsSpan(0xAB, 11).SequenceEqual("DIFC HEADER"u8);

    public static byte[] ToRaw(byte[] data)
    {
        if (!IsDim(data))
            throw new InvalidDataException("not a DIFC .dim image");
        if (data[0] != 0)
            throw new InvalidDataException($"unsupported .dim media type {data[0]}");
        const int trackSize = Sector * SectorsPerTrack;
        var output = new byte[Tracks * trackSize];
        var pos = 0x100;
        for (var t = 0; t < Tracks; t++)
        {
            if (data[1 + t] == 0)
                continue;
            var n = Math.Min(trackSize, data.Length - pos);
            if (n > 0)
                data.AsSpan(pos, n).CopyTo(output.AsSpan(t * trackSize));
            pos += trackSize;
        }
        return output;
    }
}

/// <summary>
/// Atari ST Pasti <c>.stx</c> to a plain sector image (<c>.st</c>), as <c>tools/scripts/stx_to_st.py</c>: the 512-byte
/// sectors of every track in .st order, geometry from the boot sector's BPB; protection-only parts dropped, missing
/// sectors zero-filled.
/// </summary>
public static class StxImage
{
    private const int FlagSectorBlock = 0x01, FdcRnf = 0x10, FdcCrc = 0x08;

    public static bool IsStx(byte[] data) => data.Length > 16 && data.AsSpan(0, 4).SequenceEqual("RSY\0"u8);

    private sealed record Sec(int Track, int Head, int Number, int SizeCode, int Fdc, byte[]? Data);

    public static byte[] ToSt(byte[] blob)
    {
        if (!IsStx(blob))
            throw new InvalidDataException("not a Pasti STX file (missing RSY magic)");
        int trackCount = blob[10];
        var pos = 16;
        var tracks = new Dictionary<(int Cyl, int Side), List<Sec>>();
        for (var t = 0; t < trackCount; t++)
        {
            var recSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos));
            var fuzzyCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos + 4));
            int sectCount = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(pos + 8));
            int flags = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(pos + 10));
            int trkNum = blob[pos + 14];
            int cyl = trkNum & 0x7F, side = (trkNum >> 7) & 1;
            var sectors = new List<Sec>();
            var body = pos + 16;
            if ((flags & FlagSectorBlock) == 0)
            {
                for (var i = 0; i < sectCount; i++)
                    sectors.Add(new Sec(cyl, side, i + 1, 2, 0, Slice(blob, body + i * 512, 512)));
            }
            else
            {
                var descs = new List<(int Off, int Trk, int Head, int Num, int Size, int Fdc)>();
                var p = body;
                for (var i = 0; i < sectCount; i++)
                {
                    var off = (int)BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(p));
                    descs.Add((off, blob[p + 8], blob[p + 9], blob[p + 10], blob[p + 11], blob[p + 14]));
                    p += 16;
                }
                p += fuzzyCount;
                var trackData = p;
                foreach (var d in descs)
                {
                    var size = 128 << (d.Size & 3);
                    var data = (d.Fdc & FdcRnf) != 0 ? null : Slice(blob, trackData + d.Off, size);
                    sectors.Add(new Sec(d.Trk, d.Head, d.Num, d.Size, d.Fdc, data));
                }
            }
            tracks[(cyl, side)] = sectors;
            pos += recSize;
        }

        var boot = tracks.TryGetValue((0, 0), out var t0)
            ? t0.FirstOrDefault(s => s.Number == 1 && s.Data is not null)?.Data
            : null;
        if (boot is null || boot.Length < 30)
            throw new InvalidDataException("STX: no boot sector");
        int bps = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(11));
        int total = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(19));
        int spt = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(24));
        int sides = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(26));
        if (bps != 512 || sides is < 1 or > 2 || spt is < 8 or > 11)
            throw new InvalidDataException($"STX: unexpected boot sector geometry bps={bps} spt={spt} sides={sides}");
        var cyls = total / (spt * sides);
        var output = new byte[cyls * sides * spt * 512];
        var at = 0;
        for (var cyl = 0; cyl < cyls; cyl++)
        for (var side = 0; side < sides; side++)
        {
            tracks.TryGetValue((cyl, side), out var secs);
            for (var num = 1; num <= spt; num++, at += 512)
            {
                if (secs is null)
                    continue;
                var cands = secs.Where(s => s.Number == num && s.Data is not null && s.SizeCode == 2).ToList();
                var good = cands.Where(s => (s.Fdc & FdcCrc) == 0).ToList();
                var sec = good.FirstOrDefault() ?? cands.FirstOrDefault();
                sec?.Data!.CopyTo(output.AsSpan(at));
            }
        }
        return output;
    }

    private static byte[] Slice(byte[] b, int start, int len)
    {
        if (start >= b.Length)
            return new byte[len];
        var n = Math.Min(len, b.Length - start);
        var r = new byte[len];
        b.AsSpan(start, n).CopyTo(r);
        return r;
    }
}
