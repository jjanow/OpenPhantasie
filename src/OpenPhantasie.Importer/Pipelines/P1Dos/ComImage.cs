namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// PHANTASI.COM unpacked (port of <c>re_common.py</c>, s22). The file is a small COM loader stub with an embedded MZ
/// header at file 0xf0 whose entry point is a Microsoft EXEPACK stub ("RB" at image CS:0). Data tables are read from
/// the unpacked load image at <c>DS = 0x0c9f</c> paragraphs (relocations not applied).
/// </summary>
internal sealed class ComImage
{
    private const int ComMzOffset = 0xF0;
    private const int ComDsPara = 0x0C9F;

    public ComImage(byte[] phantasiCom) => Image = Unpack(phantasiCom);

    public byte[] Image { get; }

    /// <summary><c>length</c> bytes at <c>DS:dsOffset</c> (clipped at the image end, like the Python slice).</summary>
    public byte[] Ds(int dsOffset, int length) => Py.Slice(Image, ComDsPara * 16 + dsOffset, length);

    /// <summary>The NUL-terminated latin-1 string at <c>DS:ds</c> (read through a 0x40-byte window).</summary>
    public string CString(int ds) => Py.Latin1(Py.UntilNul(Ds(ds, 0x40)));

    public static byte[] Unpack(byte[] f)
    {
        const int mz = ComMzOffset;
        if (f.Length < mz + 0x20 || f[mz] != (byte)'M' || f[mz + 1] != (byte)'Z')
            throw Py.Bad("PHANTASI.COM: embedded MZ header not found at 0xf0");
        var cparhdr = Py.U16(f, mz + 8);
        var cs = Py.U16(f, mz + 0x16);
        var img = mz + cparhdr * 16;
        var hdr = img + cs * 16;
        // struct "<HHHHHHH2s": the 7th word is dest_len, then the signature.
        var destLen = Py.U16(f, hdr + 12);
        if (f[hdr + 14] != (byte)'R' || f[hdr + 15] != (byte)'B')
            throw Py.Bad("PHANTASI.COM: EXEPACK signature missing");
        var output = new byte[destLen * 16];
        Array.Copy(f, img, output, 0, hdr - img);
        var src = hdr - img;
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
                throw Py.Bad($"PHANTASI.COM: bad EXEPACK opcode {op:x2}");
            }
            dst -= cnt;
            if ((op & 1) != 0)
                return output;
        }
    }
}
