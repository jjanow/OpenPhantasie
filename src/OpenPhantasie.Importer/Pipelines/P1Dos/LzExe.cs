namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// LZEXE 0.90 (Fabrice Bellard, 1989) unpacker for the Bonus Edition's <c>PHANTASI.EXE</c>. The MZ header's
/// <c>e_cs</c> points at the loader, whose first words hold the packed program's original IP, CS, SP and SS; the
/// compressed stream runs from the end of the MZ header to the loader. Stream: 16-bit flag words read LSB first;
/// 1 = literal byte; 01 = short match (2 more bits: length − 2, one byte: distance − 256); 00 = long match (two bytes:
/// 13-bit distance, 3-bit length − 2, 0 = a length byte follows, where 0 ends and 1 is a segment step).
/// Relocations are not applied (I's extractors read the image as loaded at segment 0).
/// </summary>
internal static class LzExe
{
    /// <summary>An MZ file whose header says "LZ09" at 0x1c.</summary>
    public static bool Is(byte[] f) =>
        f.Length > 0x20 && f[0] == (byte)'M' && f[1] == (byte)'Z'
        && f[0x1C] == (byte)'L' && f[0x1D] == (byte)'Z' && f[0x1E] == (byte)'0' && f[0x1F] == (byte)'9';

    /// <summary>The unpacked load image and the packed program's original CS (paragraphs).</summary>
    public static (byte[] Image, int Cs) Unpack(byte[] f)
    {
        if (!Is(f))
            throw Py.Bad("not an LZEXE 0.90 file");
        var header = Py.U16(f, 8) * 16;
        var loader = header + Py.U16(f, 0x16) * 16;
        if (loader + 8 > f.Length || loader <= header)
            throw Py.Bad("LZEXE: loader outside the file");
        var origCs = Py.U16(f, loader + 2);
        var pos = header;
        var end = loader;
        var output = new List<byte>(64 * 1024);

        int Byte()
        {
            if (pos >= end)
                throw Py.Bad("LZEXE: stream runs past the loader");
            return f[pos++];
        }
        int word = Byte() | Byte() << 8, count = 16;
        int Bit()
        {
            var b = word & 1;
            word >>= 1;
            if (--count == 0)
            {
                word = Byte() | Byte() << 8;
                count = 16;
            }
            return b;
        }

        while (true)
        {
            if (Bit() == 1)
            {
                output.Add((byte)Byte());
                continue;
            }
            int length, span;
            if (Bit() == 0)
            {
                length = (Bit() << 1 | Bit()) + 2;
                span = Byte() - 0x100;
            }
            else
            {
                var lo = Byte();
                var hi = Byte();
                span = (lo | (hi & 0xF8) << 5 | 0xE000) - 0x10000;
                length = hi & 7;
                if (length == 0)
                {
                    length = Byte();
                    if (length == 0)
                        break;
                    if (length == 1)
                        continue;
                    length += 1;
                }
                else
                {
                    length += 2;
                }
            }
            var from = output.Count + span;
            if (from < 0)
                throw Py.Bad("LZEXE: match before the image start");
            for (var i = 0; i < length; i++)
                output.Add(output[from + i]);
        }
        return (output.ToArray(), origCs);
    }
}
