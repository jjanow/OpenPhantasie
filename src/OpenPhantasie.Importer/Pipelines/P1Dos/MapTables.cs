using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// Overworld regions and dungeon levels (<c>extract_maps.py</c>) plus the dungeon event messages and menus
/// (<c>extract_dungeon_events.py</c>, made pure: it takes the levels built here instead of editing GameData in place).
/// </summary>
internal static class MapTables
{
    public static readonly int[] OverworldRegions = [.. Enumerable.Range(1, 16), 18, 19];

    private const int OwWidth = 26;
    private const int OwHeight = 20;
    private const int OwGridBytes = OwWidth * OwHeight;
    private const int OwFeatureOff = 0x208;
    private const int OwFeatureCount = 15;
    private const int OwFeatureSize = 5;
    private const int OwTextOff = 0x253;
    private const int OwTextPage = 40;

    private static bool IsText(string s)
    {
        if (!s.Contains(' ') || s.Length < 6)
            return false;
        var upper = s.Count(c => c is >= 'A' and <= 'Z' or ' ');
        return (double)upper / s.Length >= 0.6 && s.Distinct().Count() >= 4;
    }

    /// <summary><c>re.finditer(rb"[ -~]{4,}")</c> runs that look like text (legacy/debug output).</summary>
    private static JsonArray TextRuns(byte[] data)
    {
        var output = new JsonArray();
        var i = 0;
        while (i < data.Length)
        {
            if (data[i] is < 0x20 or > 0x7e)
            {
                i++;
                continue;
            }
            var j = i;
            while (j < data.Length && data[j] is >= 0x20 and <= 0x7e)
                j++;
            if (j - i >= 4)
            {
                var s = Py.Latin1(data.AsSpan(i, j - i));
                if (IsText(s))
                    output.Add(new JsonObject { ["offset"] = Py.Hex4(i), ["text"] = Py.Strip(s) });
            }
            i = j;
        }
        return output;
    }

    private static JsonObject ParseOverworld(byte[] data, string name)
    {
        if (data.Length != 1024)
            throw Py.Bad($"{name}: expected 1024 bytes, got {data.Length}");
        var tiles = new JsonArray();
        for (var r = 0; r < OwGridBytes; r += OwWidth)
            tiles.Add(Py.Bytes(data.AsSpan(r, OwWidth)));
        var features = new JsonArray();
        for (var i = 0; i < OwFeatureCount; i++)
        {
            var f = data.AsSpan(OwFeatureOff + i * OwFeatureSize, OwFeatureSize);
            features.Add(new JsonObject
            {
                ["index"] = i, ["x"] = (int)f[0], ["y"] = (int)f[1], ["arg"] = (int)f[2], ["dest_x"] = (int)f[3],
                ["dest_y"] = (int)f[4],
            });
        }
        var textBlob = data.AsSpan(OwTextOff);
        var pages = new JsonArray();
        for (var i = 0; i < textBlob.Length / OwTextPage; i++)
        {
            var raw = Py.Latin1(Py.UntilNul(textBlob.Slice(i * OwTextPage, OwTextPage)));
            var text = Py.Strip(raw);
            // s153: "raw" = the page as stored up to its NUL (its leading blanks place the text in I Amiga's box).
            if (text.Length != 0)
                pages.Add(new JsonObject { ["index"] = i, ["text"] = text, ["raw"] = raw });
        }
        return new JsonObject
        {
            ["size"] = data.Length,
            ["grid_width"] = OwWidth,
            ["grid_height"] = OwHeight,
            ["tiles"] = tiles,
            ["features"] = features,
            ["text_pages"] = pages,
            ["embedded_text"] = TextRuns(data),
        };
    }

    public static JsonObject Overworld(Func<string, byte[]> read)
    {
        var regions = new JsonObject();
        foreach (var n in OverworldRegions)
        {
            var f = $"OUT{n}.DAT";
            var parsed = ParseOverworld(read(f), f);
            parsed["region_id"] = n;
            regions[f] = parsed;
        }
        return new JsonObject
        {
            ["_source"] = "OUT*.DAT (SSI 1987 IBM/DOS overworld regions)",
            ["_note"] = "26×20 tile grid + 15 feature records + 40-byte text pages " +
                        "(OV2.OVR FUN_538B / FUN_6214). Fog encodes as tile+120.",
            ["grid_width"] = OwWidth,
            ["grid_height"] = OwHeight,
            ["regions"] = regions,
        };
    }

    // ---- dungeons (extract_maps.py + extract_dungeon_events.py) -------------------------------------------------

    // The committed dungeons.json carries three top-level notes: _note, which takes extract_maps.py's wording (user
    // s159; before s160 the file kept the script's old "grid geometry TBD" note of commit 13a3d839), and the hand-added
    // _grid_byte_length / _grid_note (13a3d839, 48a09246), which no script writes (extract_dungeon_events.py edits the
    // file in place, so they stayed). They are ours, so they are constants here, in the committed key order.
    public const string DungeonNote =
        "raw feature bytes; [C] grid = first 1254 B = 33x38, then side tables / events / scripts (dungeon-representation.md).";
    private const int GridByteLength = 1254;

    private const string GridNote =
        "[C] Walkable grid = first 1254 bytes = 33×38 (width 0x21, height 0x26); the rest of raw_hex is side tables, " +
        "event records and scripts. Cell legend: dungeon-representation.md.";

    private const string MessagesNote =
        "levels.*.messages: MESSn text + menu options as OV2 0x2a95 shows them (s36, " +
        "tools/scripts/extract_dungeon_events.py). Event records/scripts are read from raw_hex at run time. " +
        "continuation_of (s41): the slot is a later line of that message; DNG1..10 never ask for one.";

    /// <summary>
    /// dungeons.json: DNG1..10 raw bytes and text runs, each level's MESSn messages and menus, and the top-level
    /// notes (extract_maps.py, then extract_dungeon_events.py on its output).
    /// </summary>
    public static JsonObject Dungeons(Func<string, byte[]> read)
    {
        var levels = new JsonObject();
        for (var n = 1; n <= 10; n++)
        {
            var d = read($"DNG{n}");
            var plain = DataTables.DecryptMess(read($"MESS{n}"), n);
            levels[$"DNG{n}"] = new JsonObject
            {
                ["id"] = $"p1:{n}", // game-prefixed id = level number (Phase 09 T1.2)
                ["size"] = d.Length,
                ["raw_hex"] = Py.Hex(d),
                ["embedded_text"] = TextRuns(d),
                ["messages"] = DungeonEvents.LevelMessages(plain, d, $"DNG{n}"),
            };
        }
        var doc = new JsonObject
        {
            ["_source"] = "DNG1..DNG10 (SSI 1987 IBM/DOS dungeon levels)",
            ["_note"] = DungeonNote,
            ["levels"] = levels,
            ["_grid_byte_length"] = GridByteLength,
            ["_grid_note"] = GridNote,
            ["_messages_note"] = MessagesNote,
        };
        return doc;
    }
}

/// <summary>
/// Port of <c>extract_dungeon_events.py</c>: emulates OV2 <c>0x2a95</c> (message display + menu) on the decrypted
/// MESSn image (endgame.md 2.1 "Message and menu format").
/// </summary>
internal static class DungeonEvents
{
    private const int TextBase = 0xE6;
    private const int MaxId = 114;
    private const int OptionByteMax = 20;
    private static readonly int[] SetCellOps = [14, 15];
    private static readonly int[] FourByteOps = [12, 13, 14, 15, 18, 19];

    /// <summary>OV2 0x2418: (prefix, 40 chars) or null for an empty slot.</summary>
    private static (byte Prefix, byte[] Text)? LoadSlot(byte[] plain, int msgId)
    {
        var i = 2 * (msgId - 1);
        if (i < 0 || i + 4 > TextBase + 2)
            return null;
        int w = Py.U16(plain, i), nxt = Py.U16(plain, i + 2);
        if (w == nxt)
            return null;
        var p = TextBase + w;
        var rec = new byte[41];
        if (p < plain.Length)
            Array.Copy(plain, p, rec, 0, Math.Min(41, plain.Length - p));
        return (rec[0], rec[1..41]);
    }

    private static JsonObject? Message(byte[] plain, int msgId)
    {
        if (LoadSlot(plain, msgId) is not { } first)
            return null;
        int length = first.Prefix;
        var buf = first.Text;
        int opts = 1;
        var cur = msgId;
        var text = new List<byte>();
        var di = 0;
        while (di < length)
        {
            text.Add(buf[di % 40]);
            di++;
            if (di % 40 == 0)
            {
                cur++;
                if (LoadSlot(plain, cur) is { } nxt)
                {
                    if (di == 40)
                        opts = nxt.Prefix;
                    buf = nxt.Text;
                }
            }
        }
        if (length < 40)
            opts = 3;
        var options = new List<string>();
        var cells = new List<string>();
        if (opts is >= 2 and <= 20)
        {
            var width = opts <= 7 ? 11 : 3;
            for (var o = 0; o < opts - 1; o++)
            {
                var label = new byte[width];
                for (var k = 0; k < width; k++)
                {
                    label[k] = buf[di % 40];
                    di++;
                    if (di % 40 == 0)
                    {
                        cur++;
                        if (LoadSlot(plain, cur) is { } nxt)
                            buf = nxt.Text;
                    }
                }
                options.Add(Py.Strip(Py.Latin1(label)));
                cells.Add(Py.Latin1(label));
            }
        }
        var raw = Py.Latin1(text.ToArray());
        var lines = new List<string>();
        for (var i = 0; i < raw.Length; i += 40)
            lines.Add(Py.RStrip(raw.Substring(i, Math.Min(40, raw.Length - i))));
        var output = new JsonObject { ["lines"] = Py.Strings(lines), ["options"] = Py.Strings(options) };
        if (options.Count > 0)
            output["option_cells"] = Py.Strings(cells); // s153: the labels as stored (I Amiga's buttons)
        return output;
    }

    /// <summary>The start id whose text runs into slot <paramref name="msgId"/>, or null when it starts a message.</summary>
    private static int? ContinuationOf(byte[] plain, int msgId)
    {
        var rec = LoadSlot(plain, msgId);
        if (rec is null || rec.Value.Prefix > OptionByteMax)
            return null;
        for (var s = msgId - 1; s >= 1; s--)
        {
            var prev = LoadSlot(plain, s);
            if (prev is null)
                return null;
            if (prev.Value.Prefix > OptionByteMax)
                return prev.Value.Prefix > 40 * (msgId - s) ? s : null;
        }
        return null;
    }

    /// <summary>Every MESS id the level can ask 0x2a95 / 0x23c0 for, with the first reason found.</summary>
    private static SortedDictionary<int, string> ReferencedIds(byte[] raw)
    {
        var refs = new SortedDictionary<int, string>();
        void Set(int id, string why) => refs.TryAdd(id, why);

        void Cell(int b, string why)
        {
            if (b is >= 2 and <= 6)
                Set(b + 4, why);
            else if (b is >= 8 and <= 0x11)
                Set(b + 3, why);
            else if (b is >= 0x12 and <= 0x7f)
                Set(b - 0x11, why);
            else if (b is >= 0x8b and <= 0xb8)
                Set(b - 0x76, why);
        }

        for (var i = 0; i < 1254; i++)
            Cell(raw[i], $"cell {i % 33},{i / 33}");
        var scripts = new HashSet<int>();
        for (var k = 1; k < 36; k++)
        {
            var r = raw.AsSpan(0x4f6 + 5 * k, 6);
            if (r[0] is 0 or 0xff)
                continue;
            Set(r[0] & 0x7f, $"event {k}");
            for (var c = 2; c < 6; c++)
                scripts.Add(r[c]);
        }
        foreach (var sid in scripts)
        {
            var q = 0x5aa + 2 * sid;
            for (var step = 0; step < 6; step++)
            {
                int ob = raw[q], a = raw[q + 1];
                var op = ob % 128;
                if (op > 0x3f)
                    op -= 0x40;
                if (op == 11)
                    Set(a, $"script {sid} op 11");
                else if (op == 16)
                    Set(a + 4, $"script {sid} op 16");
                else if (op == 19)
                    Set(raw[q + 3], $"script {sid} TEST");
                else if (SetCellOps.Contains(op))
                    Cell(raw[q + 3], $"script {sid} SET-CELL");
                q += FourByteOps.Contains(op) ? 4 : 2;
                if (ob < 0x80)
                    break;
            }
        }
        return refs;
    }

    /// <summary>
    /// <c>levels.DNGn.messages</c>: every non-empty id 1..114 as OV2 shows it; fails when a referenced id is tagged
    /// as a continuation (the rule would be wrong).
    /// </summary>
    public static JsonObject LevelMessages(byte[] plain, byte[] raw, string level)
    {
        var msgs = new JsonObject();
        for (var mid = 1; mid <= MaxId; mid++)
        {
            if (Message(plain, mid) is not { } m)
                continue;
            if (ContinuationOf(plain, mid) is { } c)
                m["continuation_of"] = c;
            msgs[mid.ToString(System.Globalization.CultureInfo.InvariantCulture)] = m;
        }
        foreach (var (mid, why) in ReferencedIds(raw))
            if (msgs[mid.ToString(System.Globalization.CultureInfo.InvariantCulture)] is JsonObject m && m.ContainsKey("continuation_of"))
                throw Py.Bad($"{level}: {why} asks for message {mid}, tagged as a continuation of {m["continuation_of"]} - the rule is wrong");
        return msgs;
    }
}
