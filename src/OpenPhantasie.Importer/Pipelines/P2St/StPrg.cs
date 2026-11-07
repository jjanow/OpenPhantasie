using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// Atari ST GEMDOS executable (.PRG): header and image (port of <c>tools/scripts/st_prg.py</c>). Addresses are
/// text-relative "image" addresses (text at 0, data at <see cref="TextSize"/>); file offset = image + 0x1c. The fixup
/// table is not needed by the extractors.
/// </summary>
internal sealed class StPrg
{
    public const int Header = 0x1C;

    public StPrg(byte[] raw, string name)
    {
        Raw = raw;
        if (raw.Length < Header || BinaryPrimitives.ReadUInt16BigEndian(raw) != 0x601A)
            throw new InvalidDataException($"{name}: not a GEMDOS PRG");
        TextSize = (int)BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(2));
        DataSize = (int)BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(6));
    }

    public byte[] Raw { get; }
    public int TextSize { get; }
    public int DataSize { get; }

    /// <summary>The data segment's image address.</summary>
    public int DataStart => TextSize;

    /// <summary>Text + data (image address 0 at index 0).</summary>
    public byte[] Image => _image ??= Raw.AsSpan(Header, TextSize + DataSize).ToArray();

    private byte[]? _image;

    public int U8(int a) => Raw[Header + a];
    public int U16(int a) => BinaryPrimitives.ReadUInt16BigEndian(Raw.AsSpan(Header + a));
    public int I16(int a) => BinaryPrimitives.ReadInt16BigEndian(Raw.AsSpan(Header + a));
    public long U32(int a) => BinaryPrimitives.ReadUInt32BigEndian(Raw.AsSpan(Header + a));

    /// <summary>NUL-terminated latin-1 string, at most <paramref name="limit"/> bytes (clipped at the file end).</summary>
    public string CStr(long a, int limit = 256)
    {
        var start = (int)(Header + a);
        var span = Raw.AsSpan(start, Math.Min(limit, Raw.Length - start));
        var nul = span.IndexOf((byte)0);
        return Py.Latin1(nul < 0 ? span : span[..nul]);
    }
}

/// <summary>Python behaviours the extractors rely on, so the output matches the scripts' byte for byte.</summary>
internal static class Py
{
    public static string Latin1(ReadOnlySpan<byte> b) => Encoding.Latin1.GetString(b);

    /// <summary><c>bytes.split(b"\0", 1)[0]</c>.</summary>
    public static ReadOnlySpan<byte> UntilNul(ReadOnlySpan<byte> b)
    {
        var n = b.IndexOf((byte)0);
        return n < 0 ? b : b[..n];
    }

    /// <summary><c>str.isspace()</c> for the latin-1 range (also \x1c-\x1f, which .NET's Trim keeps).</summary>
    public static bool IsSpace(char c) =>
        c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r' or '\x1c' or '\x1d' or '\x1e' or '\x1f' or '\x85' or '\xa0'
        || (c > '\xff' && char.IsWhiteSpace(c));

    public static string Strip(string s) => RStrip(LStrip(s));

    public static string LStrip(string s)
    {
        var i = 0;
        while (i < s.Length && IsSpace(s[i]))
            i++;
        return s[i..];
    }

    public static string RStrip(string s)
    {
        var n = s.Length;
        while (n > 0 && IsSpace(s[n - 1]))
            n--;
        return s[..n];
    }

    /// <summary><c>re.split(r"\s{2,}", s)</c> with the empty pieces dropped.</summary>
    public static List<string> SplitOnSpaceRuns(string s)
    {
        var parts = new List<string>();
        var start = 0;
        var i = 0;
        while (i < s.Length)
        {
            if (IsSpace(s[i]))
            {
                var j = i;
                while (j < s.Length && IsSpace(s[j]))
                    j++;
                if (j - i >= 2)
                {
                    parts.Add(s[start..i]);
                    start = j;
                }
                i = j;
            }
            else
                i++;
        }
        parts.Add(s[start..]);
        return parts.Where(p => p.Length > 0).ToList();
    }

    /// <summary><c>bytes.hex()</c>.</summary>
    public static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(b);

    /// <summary><c>f"{v:#x}"</c>.</summary>
    public static string HexAddr(long v) => "0x" + v.ToString("x");

    /// <summary><c>bytes.rstrip(b"\0")</c>.</summary>
    public static ReadOnlySpan<byte> RStripNul(ReadOnlySpan<byte> b)
    {
        var n = b.Length;
        while (n > 0 && b[n - 1] == 0)
            n--;
        return b[..n];
    }
}

/// <summary>Short JSON builders (insertion order = Python dict order).</summary>
internal static class J
{
    public static JsonObject O(params (string Key, JsonNode? Value)[] props)
    {
        var o = new JsonObject();
        foreach (var (k, v) in props)
            o[k] = v;
        return o;
    }

    public static JsonArray A(IEnumerable<JsonNode?> items) => new(items.ToArray());

    public static JsonArray Ints(IEnumerable<int> items) => new(items.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray());

    public static JsonArray Strs(IEnumerable<string> items) => new(items.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray());

    public static JsonNode? Clone(JsonNode? n) => n?.DeepClone();
}
