using System.Buffers.Binary;
using System.Text;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// Apple II text as Phantasie II shows it: the Applesoft detokeniser (<c>a2_applesoft.py</c>) and the COUT screen
/// model of <c>extract_a2_p2_tables.py</c> (<c>screen_text</c>, <c>cr_string</c>, <c>text_page</c>).
/// </summary>
internal static class A2Text
{
    private static readonly string[] Tokens = ("END FOR NEXT DATA INPUT DEL DIM READ GR TEXT PR# IN# CALL PLOT HLIN VLIN HGR2 HGR HCOLOR= HPLOT " +
        "DRAW XDRAW HTAB HOME ROT= SCALE= SHLOAD TRACE NOTRACE NORMAL INVERSE FLASH COLOR= POP VTAB HIMEM: " +
        "LOMEM: ONERR RESUME RECALL STORE SPEED= LET GOTO RUN IF RESTORE & GOSUB RETURN REM STOP ON WAIT LOAD " +
        "SAVE DEF POKE PRINT CONT LIST CLEAR GET NEW TAB( TO FN SPC( THEN AT NOT STEP + - * / ^ AND OR > = < " +
        "SGN INT ABS USR FRE SCRN( PDL POS SQR RND LOG EXP COS SIN TAN ATN PEEK LEN STR$ VAL ASC CHR$ LEFT$ " +
        "RIGHT$ MID$").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static string Detok(ReadOnlySpan<byte> body)
    {
        var sb = new StringBuilder();
        var quoted = false;
        foreach (var b in body)
        {
            if (b == 0x22)
            {
                quoted = !quoted;
                sb.Append('"');
            }
            else if (b >= 0x80 && !quoted && b - 0x80 < Tokens.Length)
                sb.Append(' ').Append(Tokens[b - 0x80]).Append(' ');
            else if (b is >= 32 and < 127)
                sb.Append((char)b);
            else
                sb.Append('{').Append(b.ToString("x2")).Append('}');
        }
        return sb.ToString();
    }

    /// <summary>The program's lines: (offset, line number, text).</summary>
    public static IEnumerable<(int Offset, int Line, string Text)> Lines(byte[] data)
    {
        var p = 0;
        while (p + 4 <= data.Length)
        {
            int next = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p)), ln = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p + 2));
            if (next == 0)
                yield break;
            var q = Array.IndexOf(data, (byte)0, p + 4);
            if (q < 0)
                throw new InvalidDataException("Applesoft line without its end byte");
            yield return (p, ln, Detok(data.AsSpan(p + 4, q - p - 4)));
            p = q + 1;
        }
    }

    /// <summary><c>program(disk, name)</c>: line number -> text (a repeated number keeps the last).</summary>
    public static Dictionary<int, string> Program(byte[] data)
    {
        var d = new Dictionary<int, string>();
        foreach (var (_, ln, text) in Lines(data))
            d[ln] = text;
        return d;
    }

    /// <summary>Play bytes onto a <paramref name="width"/>-column screen as COUT does; the lines, right-stripped.</summary>
    public static List<string> ScreenText(ReadOnlySpan<byte> raw, int width = 40, bool stopAtCr = false)
    {
        var lines = new List<List<char>> { new() };
        int row = 0, col = 0;
        foreach (var b in raw)
        {
            var c = b & 0x7F;
            if (c == 0x0D)
            {
                if (stopAtCr)
                    break;
                row++;
                col = 0;
            }
            else if (c == 0x08)
            {
                if (col > 0)
                    col--;
                else if (row > 0)
                {
                    row--;
                    col = width - 1;
                }
                continue;
            }
            else if (c < 0x20 || c == 0x7F)
                continue;
            else
            {
                while (lines.Count <= row)
                    lines.Add([]);
                var line = lines[row];
                while (line.Count <= col)
                    line.Add(' ');
                line[col] = (char)c;
                col++;
                if (col == width)
                {
                    row++;
                    col = 0;
                }
            }
            while (lines.Count <= row)
                lines.Add([]);
        }
        var output = lines.Select(x => RStrip(new string(x.ToArray()))).ToList();
        while (output.Count > 0 && output[^1].Length == 0)
            output.RemoveAt(output.Count - 1);
        return output;
    }

    /// <summary>One CR-ended string as displayed, its screen lines stripped and joined by a space.</summary>
    public static string CrString(ReadOnlySpan<byte> raw) => string.Join(" ", ScreenText(raw, stopAtCr: true).Select(Strip));

    /// <summary>A 1000-byte text-page dump ($400) -> 24 rows of up to 40 characters (interleaved layout).</summary>
    public static List<string> TextPage(byte[] raw)
    {
        var rows = new List<string>(24);
        for (var r = 0; r < 24; r++)
        {
            var o = (r % 8) * 0x80 + (r / 8) * 0x28;
            var sb = new StringBuilder();
            for (var i = o; i < Math.Min(o + 40, raw.Length); i++)
            {
                var c = raw[i] & 0x7F;
                sb.Append((char)(c < 0x20 ? c + 0x40 : c));
            }
            rows.Add(RStrip(sb.ToString()));
        }
        return rows;
    }

    public static string Strip(string s) => P2St.Py.Strip(s);

    public static string RStrip(string s) => P2St.Py.RStrip(s);

    /// <summary>The bytes with the high bit cleared.</summary>
    public static byte[] Low7(ReadOnlySpan<byte> raw)
    {
        var o = new byte[raw.Length];
        for (var i = 0; i < raw.Length; i++)
            o[i] = (byte)(raw[i] & 0x7F);
        return o;
    }

    /// <summary>Python's <c>bytes.split(b"\r")</c>.</summary>
    public static List<byte[]> SplitCr(byte[] raw)
    {
        var parts = new List<byte[]>();
        var start = 0;
        for (var i = 0; i <= raw.Length; i++)
            if (i == raw.Length || raw[i] == 0x0D)
            {
                parts.Add(raw[start..i]);
                start = i + 1;
            }
        return parts;
    }

    /// <summary>Python's <c>bytes.strip(b"\r\x00 ")</c> is empty.</summary>
    public static bool BlankSlot(ReadOnlySpan<byte> b)
    {
        foreach (var x in b)
            if (x is not (0x0D or 0x00 or 0x20))
                return false;
        return true;
    }
}
