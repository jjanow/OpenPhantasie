using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Pipelines.P3Dos;

/// <summary>
/// Phantasie III DOS <c>MAIN.EXE</c> (port of <c>tools/scripts/extract_dos_p3_common.py</c>): an EXEPACK-packed root
/// module plus four MS-LINK overlays appended as separate MZ images (FORMAT.md section 2). Address conventions:
/// <b>img</b> = offset into the unpacked root load image; <b>DS:x</b> = img <c>0x28c00 + x</c>; <b>ovlN img</b> =
/// offset into overlay N's load image.
/// </summary>
public sealed class P3Exe
{
    public const int DsImg = 0x28C00;
    public const int DsSegment = 0x28C0;

    public P3Exe(byte[] mainExe)
    {
        Data = mainExe;
        Root = UnpackRoot(mainExe);
    }

    public byte[] Data { get; }

    /// <summary>The unpacked root load image (segment 0 = img 0).</summary>
    public byte[] Root { get; }

    public sealed record MzHeader(int Offset, int Size, int RelocCount, int HeaderBytes, int Cs, int Ip, int Overlay);

    public static MzHeader ReadMz(byte[] data, int off)
    {
        if (data[off] != (byte)'M' || data[off + 1] != (byte)'Z')
            throw new InvalidDataException($"MAIN.EXE: no MZ header at {off:#x}");
        int W(int i) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(off + 2 + 2 * i));
        var last = W(0);
        var pages = W(1);
        var size = (pages - 1) * 512 + (last == 0 ? 512 : last);
        return new MzHeader(off, size, W(2), W(3) * 16, W(10), W(9), W(12));
    }

    /// <summary>Root + overlays: each appended MZ starts at the next 512-byte boundary.</summary>
    public static List<MzHeader> Modules(byte[] data)
    {
        var list = new List<MzHeader>();
        var off = 0;
        while (off < data.Length)
        {
            var h = ReadMz(data, off);
            list.Add(h);
            off += h.Size;
            off = (off + 511) & ~511;
        }
        return list;
    }

    /// <summary>Undo EXEPACK (the image only; relocations are not needed by the extractors).</summary>
    public static byte[] UnpackRoot(byte[] data)
    {
        var h = ReadMz(data, 0);
        var img0 = h.HeaderBytes;
        var eh = img0 + h.Cs * 16; // EXEPACK header
        var destLen = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(eh + 12));
        var sigAt = data[eh + 14] == 'R' && data[eh + 15] == 'B' ? 14 : 16;
        if (data[eh + sigAt] != 'R' || data[eh + sigAt + 1] != 'B')
            throw new InvalidDataException("MAIN.EXE: not EXEPACK");
        var packedLen = eh - img0;
        var output = new byte[destLen * 16];
        Array.Copy(data, img0, output, 0, packedLen);
        int src = packedLen, dst = output.Length;
        while (output[src - 1] == 0xFF)
            src--;
        while (true)
        {
            var op = output[src - 1];
            var cnt = output[src - 3] | output[src - 2] << 8;
            src -= 3;
            if ((op & 0xFE) == 0xB0)
            {
                var fill = output[src - 1];
                src--;
                output.AsSpan(dst - cnt, cnt).Fill(fill);
            }
            else if ((op & 0xFE) == 0xB2)
            {
                // Python copies the source slice first, so overlapping ranges behave like a memmove.
                output.AsSpan(src - cnt, cnt).ToArray().CopyTo(output.AsSpan(dst - cnt));
                src -= cnt;
            }
            else
            {
                throw new InvalidDataException($"MAIN.EXE: bad EXEPACK op {op:#x}");
            }
            dst -= cnt;
            if ((op & 1) != 0)
                break;
        }
        return output;
    }

    /// <summary>Overlay <paramref name="n"/>'s load image (1..4).</summary>
    public byte[] Overlay(int n)
    {
        var h = Modules(Data).First(m => m.Overlay == n);
        return Data[(h.Offset + h.HeaderBytes)..(h.Offset + h.Size)];
    }

    public int DsByte(int x) => Root[DsImg + x];

    public int DsU16(int x) => BinaryPrimitives.ReadUInt16LittleEndian(Root.AsSpan(DsImg + x));

    public uint DsU32(int x) => BinaryPrimitives.ReadUInt32LittleEndian(Root.AsSpan(DsImg + x));

    public int DsS8(int x) => (sbyte)Root[DsImg + x];

    /// <summary>A NUL-terminated latin-1 string at img <paramref name="at"/>.</summary>
    public static string CStr(byte[] img, int at)
    {
        var end = Array.IndexOf(img, (byte)0, at);
        if (end < 0)
            throw new InvalidDataException($"unterminated string at {at:#x}");
        return Encoding.Latin1.GetString(img, at, end - at);
    }

    /// <summary>The master far-pointer string table at DS:0x1308 (FORMAT.md section 3): runs while the segment is DGROUP.</summary>
    public List<string> StringTable(int dsOffset = 0x1308)
    {
        var list = new List<string>();
        for (var i = 0; ; i++)
        {
            var off = DsU16(dsOffset + 4 * i);
            var seg = DsU16(dsOffset + 4 * i + 2);
            if (seg != DsSegment)
                break;
            list.Add(CStr(Root, DsImg + off));
        }
        return list;
    }

    /// <summary>Python's <c>str.rstrip()</c> (also strips U+001C..U+001F, which <see cref="string.TrimEnd()"/> keeps).</summary>
    public static string PyRStrip(string s)
    {
        var n = s.Length;
        while (n > 0 && IsPySpace(s[n - 1]))
            n--;
        return s[..n];
    }

    /// <summary>Python's <c>str.strip()</c>.</summary>
    public static string PyStrip(string s)
    {
        var r = PyRStrip(s);
        var i = 0;
        while (i < r.Length && IsPySpace(r[i]))
            i++;
        return r[i..];
    }

    private static bool IsPySpace(char c) => c is >= '\t' and <= '\r' or >= '\x1c' and <= ' ' || char.IsWhiteSpace(c);

    public static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);

    /// <summary>Python's <c>f"{n:#x}"</c>.</summary>
    public static string Hx(int n) => "0x" + n.ToString("x");
}
