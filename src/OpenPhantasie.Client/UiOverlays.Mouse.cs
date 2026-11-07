using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>Mouse targets the draw routines register as they lay things out (no pixels drawn here).</summary>
public static partial class UiOverlays
{
    /// <summary>
    /// The hit map the current Draw fills (<see cref="HitMap"/>), or null when nothing listens (tests, headless). Set by the
    /// shell at the start of each Draw; registering never changes what is drawn.
    /// </summary>
    public static HitMap? Hits { get; set; }

    /// <summary>What a modal panel's backdrop covers: everything above the help / message strip.</summary>
    public static readonly Rectangle ModalArea = new(0, 0, 320, 166);

    /// <summary>The pixel box of character columns [<paramref name="start"/>, <paramref name="end"/>) of a text line at (x, y).</summary>
    public static Rectangle TextSpan(int x, int y, int start, int end) =>
        new(x + start * PixelFont.Advance - 1, y - 1, Math.Max(1, end - start) * PixelFont.Advance + 1, 10);

    /// <summary>The pixel box of characters [<paramref name="start"/>, <paramref name="end"/>) of <paramref name="text"/>
    /// drawn at (x, y), measured with the font's widths (past the end of the text the layout's columns continue).</summary>
    public static Rectangle TextSpan(int x, int y, string text, int start, int end)
    {
        var left = PixelFont.ColumnX(text, start);
        return new(x + left - 1, y - 1, PixelFont.ColumnX(text, Math.Max(end, start + 1)) - left + 1, 10);
    }

    /// <summary>A modal text panel: backdrop (a click on an "any key" panel is Return), its rows, the wheel as Up/Down.</summary>
    private static void HitPanel(IReadOnlyList<string> lines, int x, int y0, int lineStep, int maxChars,
        Func<int, int?>? lineTop = null)
    {
        if (Hits is not { } hits)
            return;
        var (rows, anyKey) = PanelMouse.Analyze(lines);
        hits.Add(new HitRegion(ModalArea, new BackdropTag()) { Click = anyKey ? [Keys.Enter] : [] });
        foreach (var row in rows)
        {
            var end = row.End == PanelMouse.RowWidth ? maxChars : Math.Min(row.End, Shown(lines[row.Line], maxChars));
            // A wrapped or scrolled panel says where each line's first row is (null: scrolled out of view).
            if (row.Start < end && (lineTop is null ? y0 + row.Line * lineStep : lineTop(row.Line)) is int y)
                hits.Add(new HitRegion(TextSpan(x, y, lines[row.Line], row.Start, end), row)
                    { Hover = row.Hover, Click = row.Click });
        }
        hits.Wheel = (Keys.Up, Keys.Down);
    }

    /// <summary>Clickable key tokens of one drawn line (<see cref="PanelMouse.Hotkeys"/>), clipped to what is shown.</summary>
    private static void HitTokens(string text, int x, int y, bool commandList, int maxChars = 53)
    {
        if (Hits is not { } hits || string.IsNullOrEmpty(text))
            return;
        var shown = Shown(text, maxChars);
        foreach (var (start, end, key) in PanelMouse.Hotkeys(text, commandList))
            if (start < shown)
                hits.Add(new HitRegion(TextSpan(x, y, text, start, Math.Min(end, shown))) { Click = [key] });
    }

    /// <summary>Characters of <paramref name="text"/> left visible by <see cref="PixelFont.DrawWrapped"/> (the cut keeps maxChars − 1).</summary>
    private static int Shown(string text, int maxChars) => PixelFont.FitLength(text, maxChars * PixelFont.Advance).Length;

    /// <summary>A row the wheel or a click moves through with <paramref name="back"/> / <paramref name="forward"/>.</summary>
    private static void HitCursorRow(Rectangle bounds, int cursor, int row, Keys back, Keys forward, object? tag = null)
    {
        if (Hits is not { } hits)
            return;
        var move = HitTest.Steps(cursor, row, back, forward);
        hits.Add(new HitRegion(bounds, tag) { Hover = move, Click = [.. move, Keys.Enter] });
    }
}
