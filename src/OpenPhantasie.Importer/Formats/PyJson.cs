using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Formats;

/// <summary>
/// Writes a <see cref="JsonNode"/> the way Python's <c>json.dumps(obj, indent=N, ensure_ascii=…)</c> does, so importer
/// output can be compared byte for byte with the files the Python scripts wrote (key order kept, <c>", "</c> /
/// <c>": "</c> separators, empty containers as <c>[]</c> / <c>{}</c>, floats as Python's <c>repr</c>).
/// </summary>
public static class PyJson
{
    /// <param name="indent">Python's <c>indent</c>; null = one line with <c>", "</c> / <c>": "</c>.</param>
    /// <param name="ensureAscii">Python's <c>ensure_ascii</c> (escape everything above 0x7e as <c>\uXXXX</c>).</param>
    /// <param name="trailingNewline">The scripts append <c>"\n"</c>.</param>
    public static string Serialize(JsonNode? node, int? indent = 2, bool ensureAscii = false, bool trailingNewline = true)
    {
        var sb = new StringBuilder();
        Write(sb, node, indent, ensureAscii, 0);
        if (trailingNewline)
            sb.Append('\n');
        return sb.ToString();
    }

    public static byte[] SerializeUtf8(JsonNode? node, int? indent = 2, bool ensureAscii = false, bool trailingNewline = true) =>
        new UTF8Encoding(false).GetBytes(Serialize(node, indent, ensureAscii, trailingNewline));

    private static void Write(StringBuilder sb, JsonNode? node, int? indent, bool ascii, int level)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                return;
            case JsonObject obj:
                if (obj.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }
                sb.Append('{');
                var first = true;
                foreach (var (key, value) in obj)
                {
                    if (!first)
                        sb.Append(indent is null ? ", " : ",");
                    first = false;
                    NewLine(sb, indent, level + 1);
                    WriteString(sb, key, ascii);
                    sb.Append(": ");
                    Write(sb, value, indent, ascii, level + 1);
                }
                NewLine(sb, indent, level);
                sb.Append('}');
                return;
            case JsonArray arr:
                if (arr.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }
                sb.Append('[');
                for (var i = 0; i < arr.Count; i++)
                {
                    if (i > 0)
                        sb.Append(indent is null ? ", " : ",");
                    NewLine(sb, indent, level + 1);
                    Write(sb, arr[i], indent, ascii, level + 1);
                }
                NewLine(sb, indent, level);
                sb.Append(']');
                return;
            case JsonValue v:
                WriteValue(sb, v, ascii);
                return;
        }
    }

    private static void NewLine(StringBuilder sb, int? indent, int level)
    {
        if (indent is null)
            return;
        sb.Append('\n');
        sb.Append(' ', indent.Value * level);
    }

    private static void WriteValue(StringBuilder sb, JsonValue v, bool ascii)
    {
        if (v.TryGetValue(out JsonElement el))
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.String: WriteString(sb, el.GetString()!, ascii); return;
                case JsonValueKind.Number: sb.Append(el.GetRawText()); return;
                case JsonValueKind.True: sb.Append("true"); return;
                case JsonValueKind.False: sb.Append("false"); return;
                case JsonValueKind.Null: sb.Append("null"); return;
                default: sb.Append(el.GetRawText()); return;
            }
        }
        if (v.TryGetValue(out string? s)) { WriteString(sb, s!, ascii); return; }
        if (v.TryGetValue(out bool b)) { sb.Append(b ? "true" : "false"); return; }
        if (v.TryGetValue(out int i)) { sb.Append(i.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out long l)) { sb.Append(l.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out uint ui)) { sb.Append(ui.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out ulong ul)) { sb.Append(ul.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out short sh)) { sb.Append(sh.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out ushort us)) { sb.Append(us.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out byte by)) { sb.Append(by.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out sbyte sb8)) { sb.Append(sb8.ToString(CultureInfo.InvariantCulture)); return; }
        if (v.TryGetValue(out double d)) { sb.Append(FloatRepr(d)); return; }
        if (v.TryGetValue(out float f)) { sb.Append(FloatRepr(f)); return; }
        if (v.TryGetValue(out decimal m)) { sb.Append(FloatRepr((double)m)); return; }
        sb.Append(v.ToJsonString());
    }

    /// <summary>Python's <c>float.__repr__</c> for the values the scripts produce.</summary>
    public static string FloatRepr(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "Infinity";
        if (double.IsNegativeInfinity(d)) return "-Infinity";
        var r = d.ToString("R", CultureInfo.InvariantCulture);
        var abs = Math.Abs(d);
        if (abs != 0 && (abs < 1e-4 || abs >= 1e16))
        {
            // Python: mantissa 'e' sign two-digit-minimum exponent (1e-05, 1.5e+16).
            var e = d.ToString("E16", CultureInfo.InvariantCulture);
            var mant = double.Parse(e[..e.IndexOf('E')], CultureInfo.InvariantCulture);
            var exp = int.Parse(e[(e.IndexOf('E') + 1)..], CultureInfo.InvariantCulture);
            var ms = mant.ToString("R", CultureInfo.InvariantCulture);
            return $"{ms}e{(exp < 0 ? "-" : "+")}{Math.Abs(exp):00}";
        }
        if (r.Contains('E'))
            r = d.ToString("F1", CultureInfo.InvariantCulture);
        if (!r.Contains('.'))
            r += ".0";
        return r;
    }

    private static void WriteString(StringBuilder sb, string s, bool ascii)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20 || (ascii && c > 0x7e))
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>Deep JSON equality (objects by key set, arrays by order; numbers by value).</summary>
    public static bool DeepEquals(JsonNode? a, JsonNode? b) => JsonNode.DeepEquals(a, b);
}
