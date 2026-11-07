using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// Small helpers that behave like the Python the I DOS extractors were written in, so the output matches theirs
/// byte for byte (string decoding, <c>str.strip()</c>, <c>%</c>, <c>bytes.hex()</c>, little-endian reads).
/// </summary>
internal static class Py
{
    /// <summary><c>bytes.decode("latin1")</c>.</summary>
    public static string Latin1(ReadOnlySpan<byte> b) => Encoding.Latin1.GetString(b);

    /// <summary><c>bytes.decode("ascii", errors="replace")</c>: every byte above 0x7f becomes U+FFFD.</summary>
    public static string AsciiReplace(ReadOnlySpan<byte> b)
    {
        var sb = new StringBuilder(b.Length);
        foreach (var c in b)
            sb.Append(c < 0x80 ? (char)c : '�');
        return sb.ToString();
    }

    /// <summary><c>str.isspace()</c> for one character (Python counts 0x1c..0x1f, 0x85 and 0xa0 too).</summary>
    public static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r'
        or '\x1c' or '\x1d' or '\x1e' or '\x1f' or '\x85' or '\xa0' || (c > 0xff && char.IsWhiteSpace(c));

    /// <summary><c>str.strip()</c>.</summary>
    public static string Strip(string s) => RStrip(LStrip(s));

    public static string LStrip(string s)
    {
        var i = 0;
        while (i < s.Length && IsSpace(s[i]))
            i++;
        return s[i..];
    }

    /// <summary><c>str.rstrip()</c>.</summary>
    public static string RStrip(string s)
    {
        var j = s.Length;
        while (j > 0 && IsSpace(s[j - 1]))
            j--;
        return s[..j];
    }

    /// <summary>Python's <c>a % b</c> (result has the sign of <paramref name="b"/>).</summary>
    public static int Mod(int a, int b) => ((a % b) + b) % b;

    /// <summary><c>bytes.hex()</c>.</summary>
    public static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(b);

    /// <summary><c>f"0x{v:04x}"</c>.</summary>
    public static string Hex4(int v) => "0x" + v.ToString("x4", CultureInfo.InvariantCulture);

    /// <summary><c>hex(v)</c>.</summary>
    public static string HexPlain(int v) => "0x" + v.ToString("x", CultureInfo.InvariantCulture);

    /// <summary><c>data[start:start+length]</c>, clipped at the end like a Python slice.</summary>
    public static byte[] Slice(byte[] data, int start, int length)
    {
        if (start >= data.Length)
            return [];
        return data.AsSpan(start, Math.Min(length, data.Length - start)).ToArray();
    }

    /// <summary>The bytes before the first NUL (<c>b.split(b"\0", 1)[0]</c>).</summary>
    public static ReadOnlySpan<byte> UntilNul(ReadOnlySpan<byte> b)
    {
        var i = b.IndexOf((byte)0);
        return i < 0 ? b : b[..i];
    }

    public static ushort U16(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt16LittleEndian(b[off..]);
    public static short I16(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadInt16LittleEndian(b[off..]);
    public static uint U32(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt32LittleEndian(b[off..]);

    /// <summary>A JSON array of the byte values (<c>list(bytes)</c>).</summary>
    public static JsonArray Bytes(ReadOnlySpan<byte> b)
    {
        var a = new JsonArray();
        foreach (var v in b)
            a.Add(JsonValue.Create((int)v));
        return a;
    }

    public static JsonArray Ints(IEnumerable<int> values)
    {
        var a = new JsonArray();
        foreach (var v in values)
            a.Add(JsonValue.Create(v));
        return a;
    }

    public static JsonArray Strings(IEnumerable<string> values)
    {
        var a = new JsonArray();
        foreach (var v in values)
            a.Add(JsonValue.Create(v));
        return a;
    }

    /// <summary>
    /// Python's <c>round(num / den, 4)</c>: the ratio rounded to 4 decimals with exact integer arithmetic (an exact
    /// tie can't occur for a ratio the binary double doesn't hold exactly), then the nearest double, as Python gives.
    /// </summary>
    public static double Round4(long num, long den)
    {
        var scaled = num * 10000;
        var k = scaled / den;
        if ((scaled % den) * 2 >= den)
            k++;
        return k / 10000.0;
    }

    public static InvalidDataException Bad(string message) => new(message);
}
