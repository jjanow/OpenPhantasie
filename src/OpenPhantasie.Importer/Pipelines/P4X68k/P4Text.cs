using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>One text row (port of the rows of <c>extract_x68k_p4_text.py</c> + <c>event_text_rows</c>).</summary>
internal sealed class P4TextRow
{
    public required string Src { get; init; }
    public required int Off { get; init; }
    public int? Line { get; init; }
    public bool RefConfirmed { get; init; }
    public required string Ja { get; init; }
    public bool SourceEnglish { get; init; }
    public bool NotText { get; init; }
    /// <summary>The string-table id (<c>p4x.0x…</c>, <c>o&lt;nn&gt;.&lt;rel&gt;</c>, <c>scr&lt;n&gt;.&lt;line&gt;</c>).</summary>
    public string Key { get; set; } = "";
}

/// <summary>
/// IV's in-game text from P4.X, O_001–O_021 and SCR1–5, and the id-keyed English (strings/en.json, shipped) over it.
/// </summary>
internal sealed class P4Text
{
    private static readonly Regex Jp = new("[\u3000-\u30ff\u4e00-\u9fff\uff01-\uff9f]", RegexOptions.CultureInvariant);
    private static readonly Regex Bad = new("[\ue000-\uf8ff\\x00-\\x08\\x0b\\x0c\\x0e-\\x1a\\x1c-\\x1f\\x7f]", RegexOptions.CultureInvariant);
    private static readonly Regex KanjiOnly = new("^[\u4e00-\u9fff]+$", RegexOptions.CultureInvariant);
    private static readonly Regex Letters2 = new("[A-Za-z]{2}", RegexOptions.CultureInvariant);
    private static readonly Regex GluedOperand = new("^([!-~]{1,2})(?=[^\\x00-\\x7f])", RegexOptions.CultureInvariant);

    /// <summary>
    /// P4.X data-segment rows that decode as Shift-JIS but are data (double constants). The Python marks them by a
    /// <c>null</c> in the Japanese-keyed translation file; the importer keeps their addresses (numbers only).
    /// </summary>
    private static readonly HashSet<int> NotTextP4X =
    [
        0x3fa24, 0x3fbac, 0x3fbb4, 0x3fbdc, 0x42c04, 0x4406e, 0x4407e, 0x441fe, 0x4420e, 0x44524, 0x4452c, 0x447de,
        0x44cb0, 0x44cb8, 0x44cd0, 0x44cd8,
    ];

    public List<P4TextRow> Rows { get; } = [];
    private readonly Dictionary<string, string> _en;
    /// <summary>ja.json's strings (id -> Japanese), in the Python's order.</summary>
    public JsonObject JaStrings { get; } = new();
    private readonly Dictionary<string, string> _ja = new(StringComparer.Ordinal);
    private readonly Func<int, byte[]> _eventFile;

    public P4Text(XFile x, Func<int, byte[]> eventFile, Func<int, byte[]> scrollFile, Dictionary<string, string> shippedEnglish)
    {
        _en = shippedEnglish;
        _eventFile = eventFile;
        Rows.AddRange(P4xRows(x));
        for (var n = 1; n <= 21; n++)
            Rows.AddRange(ORows($"O_{n:000}", eventFile(n)));
        for (var k = 1; k <= 5; k++)
            Rows.AddRange(ScrRows($"SCR{k}", scrollFile(k)));
        Rows.AddRange(EventTextRows());
        foreach (var r in Rows)
        {
            r.Key = KeyOf(r);
            if (r.NotText || (r.Src == "P4.X" && r.SourceEnglish && !r.RefConfirmed))
                continue;
            JaStrings[r.Key] = r.Ja;
            _ja[r.Key] = r.Ja;
        }
    }

    public static bool HasJapanese(string s) => Jp.IsMatch(s);

    private string KeyOf(P4TextRow r)
    {
        if (r.Src == "P4.X")
            return "p4x.0x" + r.Off.ToString("x5", CultureInfo.InvariantCulture);
        if (r.Src.StartsWith("SCR", StringComparison.Ordinal))
            return $"scr{r.Src[3..]}.{r.Line}";
        var n = int.Parse(r.Src[2..], CultureInfo.InvariantCulture);
        var b = P4Events.Parse(_eventFile(n)).Base;
        return $"o{n:00}.{r.Off - b}";
    }

    /// <summary>True when ja.json has the id (Python: the en table's ids, which are the same).</summary>
    public bool HasId(string id) => _ja.ContainsKey(id);

    /// <summary>Every ja.json id, in ja.json's order.</summary>
    public IEnumerable<string> Ids => _ja.Keys;

    public string Ja(string id) => _ja[id];

    /// <summary>
    /// The English of a string id: the shipped id-keyed patch (strings/en.json), else the Japanese (the game's own
    /// English, and every id the patch leaves out because it equals the Japanese).
    /// </summary>
    public string En(string id) => _en.TryGetValue(id, out var e) ? e : _ja[id];

    /// <summary>English of a row (as the Python's row "en").</summary>
    public string En(P4TextRow r) => r.SourceEnglish ? r.Ja : _en.TryGetValue(r.Key, out var e) ? e : r.Ja;

    private static bool Plausible(string? t, bool confirmed)
    {
        if (t is null || P4Util.PyStrip(t).Length == 0 || Bad.IsMatch(t))
            return false;
        if (confirmed)
            return true;
        if (Jp.IsMatch(t))
            return Jp.Matches(t).Count >= 2 && !(t.Length <= 3 && KanjiOnly.IsMatch(t));
        return t.Length >= 3 && Letters2.IsMatch(t);
    }

    private static IEnumerable<(int Off, int Len)> Segments(byte[] blob, int start, int end)
    {
        var i = start;
        while (i < end)
        {
            var j = Array.IndexOf(blob, (byte)0, i, end - i);
            if (j < 0)
                j = end;
            if (j > i)
                yield return (i, j - i);
            i = j + 1;
        }
    }

    private static List<P4TextRow> P4xRows(XFile x)
    {
        var im = x.Image;
        var pm = x.PointerMap();
        var dataSyms = x.Symbols.Where(s => s.Kind == "data").Select(s => (s.Value, s.Name))
            .OrderBy(t => t.Value).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();

        string Where(int site)
        {
            if (site < x.Text)
                return x.FuncAt(site) ?? "text?";
            string? name = null;
            foreach (var (v, n) in dataSyms)
            {
                if (v <= site)
                    name = n;
                else
                    break;
            }
            return name ?? "data?";
        }

        var starts = pm.Keys.Where(t => x.Text <= t && t < x.Text + x.Data).OrderBy(t => t).ToList();
        var rows = new List<P4TextRow>();
        foreach (var (off, len) in Segments(im, x.Text, x.Text + x.Data))
        {
            var cuts = new List<int> { off };
            cuts.AddRange(starts.Where(t => off < t && t < off + len));
            for (var k = 0; k < cuts.Count; k++)
            {
                var c = cuts[k];
                var e = k + 1 < cuts.Count ? cuts[k + 1] : off + len;
                var t = P4Util.DecodeCp932(im.AsSpan(c, e - c));
                var conf = pm.ContainsKey(c);
                if (!Plausible(t, conf))
                    continue;
                _ = conf ? pm[c].Select(Where).ToList() : null; // refs: not part of any output
                rows.Add(new P4TextRow
                {
                    Src = "P4.X", Off = c, RefConfirmed = conf, Ja = t!, SourceEnglish = !Jp.IsMatch(t!),
                    NotText = NotTextP4X.Contains(c),
                });
            }
        }
        return rows;
    }

    private static List<P4TextRow> ORows(string fn, byte[] b)
    {
        var rows = new List<P4TextRow>();
        foreach (var (off0, len) in Segments(b, 0, b.Length))
        {
            var t = P4Util.DecodeCp932(b.AsSpan(off0, len));
            if (t is null || !Jp.IsMatch(t) || !Plausible(t, false))
                continue;
            var off = off0;
            var m = GluedOperand.Match(t);
            if (m.Success && m.Groups[1].Value is not ("A" or "B" or "C" or "D" or "E" or "M" or "N"))
            {
                off += m.Groups[1].Length;
                t = t[m.Groups[1].Length..];
            }
            rows.Add(new P4TextRow { Src = fn, Off = off, Ja = t, SourceEnglish = !Jp.IsMatch(t) });
        }
        return rows;
    }

    private static List<P4TextRow> ScrRows(string fn, byte[] b)
    {
        var rows = new List<P4TextRow>();
        for (var line = 0; line < b.Length / 35 + 1; line++)
        {
            var s = Math.Min(line * 35, b.Length);
            var e = Math.Min((line + 1) * 35, b.Length);
            var seg = b.AsSpan(s, e - s);
            var z = seg.IndexOf((byte)0);
            if (z >= 0)
                seg = seg[..z];
            var t = P4Util.DecodeCp932(seg);
            if (string.IsNullOrEmpty(t) || P4Util.PyStrip(t).Length == 0)
                continue;
            rows.Add(new P4TextRow { Src = fn, Off = line * 35, Line = line, Ja = t, SourceEnglish = !Jp.IsMatch(t) });
        }
        return rows;
    }

    /// <summary>
    /// Event texts the bytecode prints (PRINT / TITLE / MADD) that the string scan skipped (build_p4_gamedata.py
    /// <c>event_text_rows</c>). Their English comes from the id-keyed patch like every other row.
    /// </summary>
    private List<P4TextRow> EventTextRows()
    {
        var have = Rows.Where(r => !r.NotText).Select(r => (r.Src, r.Off)).ToHashSet();
        var extra = new List<P4TextRow>();
        for (var n = 1; n <= 21; n++)
        {
            var src = $"O_{n:000}";
            var (ins, texts) = P4Events.Disasm(_eventFile(n));
            foreach (var i in ins.OrderBy(i => i.Args, ArgsComparer.Instance))
            {
                if (i.Op is not (0x09 or 0x0A or 0x28) || have.Contains((src, i.Args[0])))
                    continue;
                have.Add((src, i.Args[0]));
                var ja = texts[i.Args[0]];
                extra.Add(new P4TextRow { Src = src, Off = i.Args[0], Ja = ja, SourceEnglish = ja.All(c => c < 0x80) });
            }
        }
        return extra;
    }

    /// <summary>Python's list ordering (element by element, then length).</summary>
    private sealed class ArgsComparer : IComparer<int[]>
    {
        public static readonly ArgsComparer Instance = new();

        public int Compare(int[]? a, int[]? b)
        {
            for (var i = 0; i < Math.Min(a!.Length, b!.Length); i++)
                if (a[i] != b[i])
                    return a[i].CompareTo(b[i]);
            return a.Length.CompareTo(b.Length);
        }
    }
}
