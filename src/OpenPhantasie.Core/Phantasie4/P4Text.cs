namespace OpenPhantasie.Core;

/// <summary>
/// IV's in-game text as the original prints it (<c>_xprintf</c>): the X68000 strings keep their C formats (<c>%s</c>,
/// <c>%d</c>), and a leading <c>\n</c> starts a new line in the message window. A small scrolling window of lines.
/// </summary>
public sealed class P4MessageWindow
{
    /// <summary>Lines kept (the oldest scroll away).</summary>
    public const int Capacity = 12;

    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public void Clear() => _lines.Clear();

    /// <summary>One <c>_xprintf</c>: each <c>\n</c> starts a new line; text before the first one continues the last line.</summary>
    public void Print(string text)
    {
        var parts = text.Split('\n');
        if (_lines.Count == 0)
            _lines.Add("");
        _lines[^1] += parts[0];
        for (var i = 1; i < parts.Length; i++)
            _lines.Add(parts[i]);
        while (_lines.Count > Capacity)
            _lines.RemoveAt(0);
    }

    /// <summary>Starts a new line with <paramref name="text"/>.</summary>
    public void Line(string text) => Print("\n" + text);

    public void Restore(IEnumerable<string> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
    }

    /// <summary>The string <paramref name="id"/> with its C formats (<c>%s</c>, <c>%d</c>) filled in order.</summary>
    public static string Format(string id, params object[] args)
    {
        var s = GameText.S(id);
        var sb = new System.Text.StringBuilder();
        var a = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 1 < s.Length && s[i + 1] is 's' or 'd' && a < args.Length)
            {
                sb.Append(Convert.ToString(args[a++], System.Globalization.CultureInfo.CurrentCulture));
                i++;
            }
            else
                sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
