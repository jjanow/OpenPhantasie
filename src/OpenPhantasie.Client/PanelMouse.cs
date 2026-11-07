using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// Reads mouse targets off the text a key-driven panel draws, so every <see cref="IModalDialog"/> (and the DOS save dialog)
/// works with the mouse without code of its own: hover and click become the keys the panel already takes.
/// </summary>
/// <remarks>
/// Rules, first match wins per line:
/// <list type="number">
/// <item>A cursor list — a run of lines starting with the marker slot (<c>&gt;</c> on the current row, a space on the
/// others): hover sends Up/Down until the marker is on that row, a click the same plus Return.</item>
/// <item>A one-line bar (" NO &gt;YES", "&gt;SPELLS  EQUIPMENT  …"): Left/Right to the option, a click adds Return.</item>
/// <item>A numbered row ("1 NAME", "&gt;2 NAME"): a click types the digit.</item>
/// <item>A key-and-word line ("A AGREE"), an <c>X)</c> word or a <c>(Y/N)</c>: a click types that key.</item>
/// </list>
/// A panel with none of these is an "any key" panel (health report, a text page): a click anywhere on it is Return.
/// </remarks>
public static class PanelMouse
{
    /// <summary>A clickable span of line <see cref="Line"/>, in character columns [<see cref="Start"/>, <see cref="End"/>).</summary>
    public sealed record LineHit(int Line, int Start, int End, IReadOnlyList<Keys> Hover, IReadOnlyList<Keys> Click);

    /// <summary>Columns a whole-row hit spans (the text panel's width).</summary>
    public const int RowWidth = 48;

    private static readonly Regex NumberedRow = new(@"^[> ]{0,2}([1-9])[ )]", RegexOptions.CultureInvariant);
    private static readonly Regex KeyWordLine = new(@"^([A-Z0-9]) [A-Z]+$", RegexOptions.CultureInvariant);

    /// <summary>The hits of a panel's lines, and whether it is an "any key" panel (no hits at all).</summary>
    public static (IReadOnlyList<LineHit> Hits, bool AnyKey) Analyze(IReadOnlyList<string> lines)
    {
        var hits = new List<LineHit>();
        var used = new HashSet<int>();

        var marker = -1;
        for (var i = 0; i < lines.Count && marker < 0; i++)
            if (lines[i].StartsWith('>') || MidLineMarker(lines[i]))
                marker = i;
        if (marker >= 0)
        {
            var line = lines[marker];
            var first = marker;
            var last = marker;
            if (line.StartsWith('>'))
            {
                while (first > 0 && IsListRow(lines[first - 1])) first--;
                while (last < lines.Count - 1 && IsListRow(lines[last + 1])) last++;
            }
            var options = BarOptions(line);
            if (first == last && options.Count >= 2)
            {
                var current = options.FindIndex(o => line[o.Slot] == '>');
                for (var k = 0; k < options.Count; k++)
                {
                    var move = HitTest.Steps(current, k, Keys.Left, Keys.Right);
                    hits.Add(new LineHit(marker, options[k].Slot, options[k].End, move, [.. move, Keys.Enter]));
                }
                used.Add(marker);
            }
            else
            {
                for (var r = first; r <= last; r++)
                {
                    var move = HitTest.Steps(marker, r, Keys.Up, Keys.Down);
                    hits.Add(new LineHit(r, 0, RowWidth, move, [.. move, Keys.Enter]));
                    used.Add(r);
                }
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (used.Contains(i))
                continue;
            var line = lines[i];
            if (NumberedRow.Match(line) is { Success: true } numbered)
            {
                hits.Add(new LineHit(i, 0, RowWidth, [], [HitTest.Digit(numbered.Groups[1].Value[0] - '0')]));
                continue;
            }
            if (KeyWordLine.Match(line) is { Success: true } keyWord && KeyOf(keyWord.Groups[1].Value) is { } k)
            {
                hits.Add(new LineHit(i, 0, line.Length, [], [k]));
                continue;
            }
            foreach (var (start, end, key) in Hotkeys(line, commandList: false))
                hits.Add(new LineHit(i, start, end, [], [key]));
        }
        return (hits, hits.Count == 0);
    }

    /// <summary>
    /// Key tokens in a line of text, as character spans: <c>X)</c> words ("C)AST"), the letters of a <c>Y/N</c>, and with
    /// <paramref name="commandList"/> (help lines, the combat order line) key-and-word pairs ("L list", "Esc back",
    /// "1 THRUST", "F2 map").
    /// </summary>
    public static IEnumerable<(int Start, int End, Keys Key)> Hotkeys(string text, bool commandList)
    {
        if (string.IsNullOrEmpty(text))
            yield break;
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i + 1] != ')' || !char.IsLetterOrDigit(text[i]) || (i > 0 && text[i - 1] != ' '))
                continue;
            if (KeyOf(text[i].ToString().ToUpperInvariant()) is not { } key)
                continue;
            var end = text.IndexOf(' ', i);
            yield return (i, end < 0 ? text.Length : end, key);
        }
        for (var i = text.IndexOf("Y/N", StringComparison.Ordinal); i >= 0; i = text.IndexOf("Y/N", i + 3, StringComparison.Ordinal))
        {
            yield return (i, i + 1, Keys.Y);
            yield return (i + 2, i + 3, Keys.N);
        }
        if (!commandList)
            yield break;
        var words = Words(text);
        for (var w = 0; w + 1 < words.Count; w++)
        {
            var (start, word) = words[w];
            var (labelStart, label) = words[w + 1];
            if (KeyOf(word) is not { } key || KeyOf(label) is not null || !char.IsLetter(label[0])
                || label.Length < 2 || StopWords.Contains(label.ToLowerInvariant()))
                continue;
            yield return (start, labelStart + label.Length, key);
            w++;
        }
    }

    /// <summary>Movement and range words that follow a key name in help lines without being its command.</summary>
    private static readonly HashSet<string> StopWords = ["arrows", "numpad", "move", "or"];

    /// <summary>A key name as help lines write it: one capital letter or digit, F1..F12, Enter, Space, Esc, Tab, Bksp.</summary>
    public static Keys? KeyOf(string name) => name switch
    {
        { Length: 1 } when name[0] is >= 'A' and <= 'Z' => Keys.A + (name[0] - 'A'),
        { Length: 1 } when name[0] is >= '0' and <= '9' => HitTest.Digit(name[0] - '0'),
        "Enter" => Keys.Enter,
        "Space" => Keys.Space,
        "Esc" => Keys.Escape,
        "Tab" => Keys.Tab,
        "Bksp" => Keys.Back,
        _ when name.Length is 2 or 3 && name[0] == 'F' && int.TryParse(name[1..], out var f) && f is >= 1 and <= 12
            => Keys.F1 + (f - 1),
        _ => null,
    };

    private static List<(int Start, string Word)> Words(string text)
    {
        var words = new List<(int, string)>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && text[i] == ' ') i++;
            var start = i;
            while (i < text.Length && text[i] != ' ') i++;
            if (i > start)
                words.Add((start, text[start..i].TrimEnd(',', '.', ';')));
        }
        return words;
    }

    /// <summary>A row of a cursor list: the marker slot (space or <c>&gt;</c>) then text.</summary>
    private static bool IsListRow(string line) =>
        line.Length > 1 && line[0] is ' ' or '>' && line.Trim().Length > 0;

    /// <summary>A bar whose current option is not the first (" NO &gt;YES").</summary>
    private static bool MidLineMarker(string line)
    {
        var p = line.IndexOf('>');
        return p > 0 && line[p - 1] == ' ' && p + 1 < line.Length && line[p + 1] != ' ' && BarOptions(line).Count >= 2;
    }

    /// <summary>
    /// The options of a one-line bar: each begins at its marker slot (a <c>&gt;</c> or a space after a space, or the
    /// line's first column) followed by a word; it ends at the end of its words.
    /// </summary>
    private static List<(int Slot, int End)> BarOptions(string line)
    {
        var slots = new List<int>();
        for (var i = 0; i + 1 < line.Length; i++)
        {
            if (line[i] is not (' ' or '>') || line[i + 1] == ' ')
                continue;
            if (i == 0 || line[i] == '>' && line[i - 1] == ' ' || line[i] == ' ' && line[i - 1] == ' ')
                slots.Add(i);
        }
        var options = new List<(int, int)>();
        for (var k = 0; k < slots.Count; k++)
        {
            var limit = k + 1 < slots.Count ? slots[k + 1] : line.Length;
            options.Add((slots[k], line[..limit].TrimEnd().Length));
        }
        return options;
    }
}
