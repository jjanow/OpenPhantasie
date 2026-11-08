namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// I's DOS program unpacked (port of <c>re_common.py</c>, s22). Two packings give the same load image:
/// <list type="bullet">
/// <item>SSI 1987 <c>PHANTASI.COM</c>: a small COM loader stub with an embedded MZ header at file 0xf0 whose entry
/// point is a Microsoft EXEPACK stub ("RB" at image CS:0).</item>
/// <item>WizardWorks Bonus Edition (1990/91, the Memorial Set's I) <c>PHANTASI.EXE</c>: LZEXE 0.90 around the same
/// EXEPACK image (inner CS 0x83b). Unpacked it equals 1987's except DS:0x3094 (0, not 1: saves are not created
/// hidden; <c>reverse-engineering/dos/bonus-edition.md</c>).</item>
/// </list>
/// Data tables are read from the unpacked load image at <c>DS = 0x0c9f</c> paragraphs (relocations not applied).
/// </summary>
internal sealed class ComImage
{
    private const int ComMzOffset = 0xF0;
    private const int ComDsPara = 0x0C9F;

    /// <summary>The program file names, 1987's first.</summary>
    public static readonly string[] ProgramFiles = ["PHANTASI.COM", "PHANTASI.EXE"];

    public ComImage(byte[] program) => Image = UnpackAny(program);

    public byte[] Image { get; }

    /// <summary><c>length</c> bytes at <c>DS:dsOffset</c> (clipped at the image end, like the Python slice).</summary>
    public byte[] Ds(int dsOffset, int length) => Py.Slice(Image, ComDsPara * 16 + dsOffset, length);

    /// <summary>The NUL-terminated latin-1 string at <c>DS:ds</c> (read through a 0x40-byte window).</summary>
    public string CString(int ds) => Py.Latin1(Py.UntilNul(Ds(ds, 0x40)));

    /// <summary>Either program file: an LZEXE-packed EXE (Bonus) or the 1987 COM.</summary>
    public static byte[] UnpackAny(byte[] program) =>
        LzExe.Is(program) ? UnpackExe(program) : Unpack(program);

    /// <summary>1987's <c>PHANTASI.COM</c>.</summary>
    public static byte[] Unpack(byte[] f)
    {
        const int mz = ComMzOffset;
        if (f.Length < mz + 0x20 || f[mz] != (byte)'M' || f[mz + 1] != (byte)'Z')
            throw Py.Bad("PHANTASI.COM: embedded MZ header not found at 0xf0");
        var cparhdr = Py.U16(f, mz + 8);
        var cs = Py.U16(f, mz + 0x16);
        var img = mz + cparhdr * 16;
        return Exepack(f.AsSpan(img), cs, "PHANTASI.COM");
    }

    /// <summary>The Bonus Edition's <c>PHANTASI.EXE</c>: LZEXE 0.90, then the same EXEPACK image.</summary>
    public static byte[] UnpackExe(byte[] exe)
    {
        var (image, cs) = LzExe.Unpack(exe);
        return Exepack(image, cs, "PHANTASI.EXE");
    }

    /// <summary>Expands an EXEPACK load image whose stub header sits at <c>cs</c> paragraphs.</summary>
    private static byte[] Exepack(ReadOnlySpan<byte> image, int cs, string what)
    {
        var hdr = cs * 16;
        if (image.Length < hdr + 16)
            throw Py.Bad($"{what}: EXEPACK header past the image end");
        // struct "<HHHHHHH2s": the 7th word is dest_len, then the signature.
        var destLen = image[hdr + 12] | image[hdr + 13] << 8;
        if (image[hdr + 14] != (byte)'R' || image[hdr + 15] != (byte)'B')
            throw Py.Bad($"{what}: EXEPACK signature missing");
        var output = new byte[destLen * 16];
        if (hdr > output.Length)
            throw Py.Bad($"{what}: EXEPACK image longer than its output");
        image[..hdr].CopyTo(output);
        var src = hdr;
        while (output[src - 1] == 0xFF) // the stub skips up to 15 bytes of 0xff padding
            src--;
        var dst = output.Length;
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
                // Copy backwards-safe: source and destination may overlap (Python copies a snapshot of the slice).
                var tmp = output.AsSpan(src - cnt, cnt).ToArray();
                tmp.CopyTo(output.AsSpan(dst - cnt));
                src -= cnt;
            }
            else
            {
                throw Py.Bad($"{what}: bad EXEPACK opcode {op:x2}");
            }
            dst -= cnt;
            if ((op & 1) != 0)
                return output;
        }
    }
}
