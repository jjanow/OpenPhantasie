using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>Shift-JIS (Python's <c>cp932</c> codec), Python string helpers and JSON builders for the IV pipeline.</summary>
internal static class P4Util
{
    private static readonly Lazy<Encoding> Strict = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    });

    private static readonly Lazy<Encoding> Lenient = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932, EncoderFallback.ReplacementFallback, new DecoderReplacementFallback("\ufffd"));
    });

    /// <summary>Python's <c>bytes.decode('cp932')</c>: null where Python raises.</summary>
    public static string? DecodeCp932(ReadOnlySpan<byte> b)
    {
        // Python's cp932 maps the single bytes 0x80, 0xA0 and 0xFD-0xFF (U+0080, U+F8F0-U+F8F3); decode those runs apart.
        var sb = new StringBuilder();
        var start = 0;
        var i = 0;
        while (i < b.Length)
        {
            var c = b[i];
            if (IsLead(c))
            {
                i += 2;
                continue;
            }
            if (c is 0x80 or 0xA0 or >= 0xFD)
            {
                if (!Flush(b[start..i], sb))
                    return null;
                sb.Append(c switch { 0x80 => '\u0080', 0xA0 => '\uf8f0', _ => (char)(0xF8F1 + c - 0xFD) });
                i++;
                start = i;
                continue;
            }
            i++;
        }
        if (!Flush(b[start..Math.Min(i, b.Length)], sb))
            return null;
        if (i > b.Length)
            return null; // a lead byte at the end
        return sb.ToString();
    }

    private static bool IsLead(byte c) => c is >= 0x81 and <= 0x9F or >= 0xE0 and <= 0xFC;

    private static bool Flush(ReadOnlySpan<byte> run, StringBuilder sb)
    {
        if (run.IsEmpty)
            return true;
        try
        {
            sb.Append(Strict.Value.GetString(run));
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Python's <c>decode('cp932', 'replace')</c> (only used on event texts, which decode cleanly).</summary>
    public static string DecodeCp932Replace(ReadOnlySpan<byte> b) => DecodeCp932(b) ?? Lenient.Value.GetString(b);

    /// <summary>NUL-terminated cp932 string at <paramref name="a"/> (Python <c>cstr</c>); throws on a bad byte.</summary>
    public static string CStr(byte[] b, int a)
    {
        var j = Array.IndexOf(b, (byte)0, a);
        if (j < 0)
            throw new InvalidDataException($"no NUL after 0x{a:x}");
        return DecodeCp932(b.AsSpan(a, j - a)) ?? throw new InvalidDataException($"bad Shift-JIS at 0x{a:x}");
    }

    public static string CStrAscii(ReadOnlySpan<byte> b, int a)
    {
        var j = b[a..].IndexOf((byte)0);
        if (j < 0)
            throw new InvalidDataException("no NUL");
        var s = b.Slice(a, j);
        foreach (var c in s)
            if (c > 0x7F)
                throw new InvalidDataException("not ASCII");
        return Encoding.ASCII.GetString(s);
    }

    private static bool IsPySpace(char c) =>
        c is '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\x1c' or '\x1d' or '\x1e' or '\x1f' or '\x85' or '\xa0'
            or '\u1680' or (>= '\u2000' and <= '\u200a') or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000';

    /// <summary>Python's <c>str.strip()</c>.</summary>
    public static string PyStrip(string s)
    {
        int a = 0, e = s.Length;
        while (a < e && IsPySpace(s[a]))
            a++;
        while (e > a && IsPySpace(s[e - 1]))
            e--;
        return s[a..e];
    }

    public static string Hex(int v) => "0x" + v.ToString("x", CultureInfo.InvariantCulture);

    public static string P4xId(int addr) => "p4x." + Hex(addr);

    public static short S16(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt16BigEndian(b[o..]);

    public static uint U32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32BigEndian(b[o..]);

    public static int S32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt32BigEndian(b[o..]);

    // ---- JSON builders ------------------------------------------------------------------------------------------

    public static JsonArray Arr<T>(IEnumerable<T> xs) => new(xs.Select(Node).ToArray());

    /// <summary>An array of the given elements (each one element, never spread).</summary>
    public static JsonArray ArrOf(params object?[] xs) => new(xs.Select(Node).ToArray());

    [Obsolete("a string is one element: use ArrOf", error: true)]
    public static JsonArray Arr(string s) => throw new NotSupportedException();

    [Obsolete("an object is one element: use ArrOf", error: true)]
    public static JsonArray Arr(JsonObject o) => throw new NotSupportedException();

    public static JsonNode? Node<T>(T x) => x switch
    {
        null => null,
        JsonNode n => n,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        uint u => JsonValue.Create(u),
        short s16 => JsonValue.Create((int)s16),
        byte by => JsonValue.Create((int)by),
        sbyte sb => JsonValue.Create((int)sb),
        double d => JsonValue.Create(d),
        System.Collections.IEnumerable e => new JsonArray(e.Cast<object?>().Select(Node).ToArray()),
        _ => throw new ArgumentException($"no JSON for {x.GetType()}"),
    };

    /// <summary>An object from (key, value) pairs in order.</summary>
    public static JsonObject Obj(params (string Key, object? Value)[] kv)
    {
        var o = new JsonObject();
        foreach (var (k, v) in kv)
            o[k] = Node(v);
        return o;
    }

    public static JsonArray Lst(IEnumerable<string> xs) =>
        new(xs.Select((x, i) => (JsonNode)Obj(("index", i), ("name", x))).ToArray());
}
