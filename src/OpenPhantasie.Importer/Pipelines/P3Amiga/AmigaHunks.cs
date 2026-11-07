using System.Buffers.Binary;

namespace OpenPhantasie.Importer.Pipelines.P3Amiga;

/// <summary>
/// AmigaDOS hunk executables (port of <c>tools/scripts/extract_amiga_p3_common.py</c>). <c>PhantasieIII</c> is one
/// (not packed): <see cref="Parse"/> returns each loadable hunk with its data and HUNK_RELOC32 entries;
/// <see cref="LoadFlat"/> lays the hunks end to end (each at its own base, relocations applied), the flat image the
/// program addresses in <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> refer to (CODE at 0, DATA at 0x21890).
/// The art pipeline itself reads only data files; this is kept for pipelines that read tables out of the program.
/// </summary>
public static class AmigaHunks
{
    public sealed record Hunk(int Index, string Type, int Alloc, byte[] Data, int FileOffset,
        IReadOnlyDictionary<int, List<int>> Relocs);

    private static readonly Dictionary<uint, string> Names = new()
    {
        [0x3E7] = "UNIT", [0x3E8] = "NAME", [0x3E9] = "CODE", [0x3EA] = "DATA", [0x3EB] = "BSS",
        [0x3EC] = "RELOC32", [0x3ED] = "RELOC16", [0x3EE] = "RELOC8", [0x3EF] = "EXT", [0x3F0] = "SYMBOL",
        [0x3F1] = "DEBUG", [0x3F2] = "END", [0x3F3] = "HEADER", [0x3F5] = "OVERLAY", [0x3F6] = "BREAK",
    };

    public static List<Hunk> Parse(byte[] data)
    {
        var p = 0;
        uint U32()
        {
            var v = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p));
            p += 4;
            return v;
        }

        if (U32() != 0x3F3)
            throw new InvalidDataException("not a hunk executable");
        while (U32() != 0) // resident library names (none expected)
        {
        }
        U32(); // table size
        var first = U32();
        var last = U32();
        var sizes = new List<int>();
        for (var i = first; i <= last; i++)
            sizes.Add((int)(U32() & 0x3FFFFFFF));
        var output = new List<Hunk>();
        Hunk? cur = null;
        while (p < data.Length)
        {
            var t = U32() & 0x3FFFFFFF;
            switch (t)
            {
                case 0x3E9 or 0x3EA:
                {
                    var n = (int)U32() * 4;
                    cur = new Hunk(output.Count, Names[t], sizes[output.Count] * 4, data.AsSpan(p, n).ToArray(), p,
                        new Dictionary<int, List<int>>());
                    p += n;
                    break;
                }
                case 0x3EB:
                    U32();
                    cur = new Hunk(output.Count, "BSS", sizes[output.Count] * 4, [], p, new Dictionary<int, List<int>>());
                    break;
                case 0x3EC:
                    while (true)
                    {
                        var n = (int)U32();
                        if (n == 0)
                            break;
                        var target = (int)U32();
                        var relocs = (Dictionary<int, List<int>>)cur!.Relocs;
                        if (!relocs.TryGetValue(target, out var list))
                            relocs[target] = list = [];
                        for (var i = 0; i < n; i++)
                            list.Add((int)U32());
                    }
                    break;
                case 0x3F0:
                    while (true)
                    {
                        var n = (int)U32();
                        if (n == 0)
                            break;
                        p += n * 4 + 4;
                    }
                    break;
                case 0x3F1:
                    p += (int)U32() * 4;
                    break;
                case 0x3F2:
                    output.Add(cur!);
                    cur = null;
                    break;
                default:
                    throw new InvalidDataException($"unhandled hunk {t:x} at {p - 4:x}");
            }
        }
        return output;
    }

    /// <summary>All hunks in one image, relocations applied: (image, hunk base addresses).</summary>
    public static (byte[] Image, int[] Bases) LoadFlat(byte[] data, int align = 0x10)
    {
        var hs = Parse(data);
        var bases = new int[hs.Count];
        var at = 0;
        for (var i = 0; i < hs.Count; i++)
        {
            bases[i] = at;
            at += (hs[i].Alloc + align - 1) & ~(align - 1);
        }
        var img = new byte[at];
        for (var i = 0; i < hs.Count; i++)
            hs[i].Data.CopyTo(img, bases[i]);
        for (var i = 0; i < hs.Count; i++)
            foreach (var (target, offs) in hs[i].Relocs)
                foreach (var o in offs)
                {
                    var span = img.AsSpan(bases[i] + o);
                    BinaryPrimitives.WriteUInt32BigEndian(span,
                        unchecked(BinaryPrimitives.ReadUInt32BigEndian(span) + (uint)bases[target]));
                }
        return (img, bases);
    }
}
