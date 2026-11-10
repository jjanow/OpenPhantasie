using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>A title on I Amiga's map menu bar (s155): a click pulls its menu down or closes it.</summary>
public sealed record P1MapMenuTitleTag(int Menu);

/// <summary>An item of I Amiga's open map menu (s155): hover highlights it, a click takes it.</summary>
public sealed record P1MapMenuItemTag(int Item);

/// <summary>The screen behind an open map menu (s155): a click there closes the menu.</summary>
public sealed record P1MapMenuBackdropTag;

public static partial class UiOverlays
{
    /// <summary>
    /// s155: I Amiga's map screen top: the menu bar (pack <c>map_message.menu_bar</c>, h80 <c>+0xb48</c>) at rest — the
    /// titles in the bar's pen on its JAM2 ground — or, with a menu pulled down, Intuition's strip: the titles in the item
    /// pen, the lit title's box, the item box with its border (2 px at the sides) and the items, the pointed one
    /// complemented, a ghosted one (Look outdoors) cleared to the box pen in the pack's pattern. <paramref name="input"/>
    /// registers the titles (and the open menu's items, and a backdrop that closes it) for the mouse.
    /// </summary>
    /// <param name="gold">P3 QoL: the session whose Guild treasury gold line the bar's free right end shows (null: none).</param>
    public static void DrawP1MapMenuBar(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, P1MapMenuBar state,
        bool outdoors, bool input, GameSession? gold = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(state);
        if (layout.MenuBar is not { } bar)
            return;
        Color[] pens = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
        using var font = PixelFont.With(FontSources.Named(bar.Font, bar.FontFile));
        var open = state.Open is { } o && o < bar.Menus.Count ? o : (int?)null;
        if (open is not null && input)
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new P1MapMenuBackdropTag()));
        Fill(batch, pixel, new Rectangle(0, 0, 320, bar.Rows), pens[bar.BarPen]);
        for (var m = 0; m < bar.Menus.Count; m++)
        {
            var menu = bar.Menus[m];
            var lit = open == m;
            if (lit)
                Fill(batch, pixel, ToRect(menu.Highlight), pens[bar.HighlightPen]);
            var pen = lit ? bar.HighlightTextPen : open is null ? bar.TextPen : bar.ItemPen;
            PixelFont.Draw(batch, pixel, menu.TitleX, bar.TextY, UiText.S(menu.Title), pens[pen]);
            if (input)
                Hits?.Add(new HitRegion(ToRect(menu.Highlight) with { Height = bar.Rows }, new P1MapMenuTitleTag(m)));
        }
        var usedRight = bar.Menus.Count == 0 ? 0 : bar.Menus.Max(m => Math.Max(ToRect(m.Highlight).Right, m.TitleX + PixelFont.Measure(UiText.S(m.Title))));
        DrawBarGold(batch, pixel, gold, usedRight, bar.TextY, pens[bar.TextPen]);
        if (open is not { } shown || bar.Menus[shown] is not { Items.Count: > 0 } openMenu)
            return;
        var box = ToRect(openMenu.Box);
        Fill(batch, pixel, box, pens[bar.BoxPen]);
        Outline(batch, pixel, box, pens[bar.BorderPen]);
        Outline(batch, pixel, new Rectangle(box.X + 1, box.Y, box.Width - 2, box.Height), pens[bar.BorderPen]); // 2 px sides
        for (var i = 0; i < openMenu.Items.Count; i++)
        {
            var y = openMenu.ItemY + i * openMenu.ItemHeight;
            var rowBox = new Rectangle(box.X + 2, y, box.Width - 4, openMenu.ItemHeight);
            var ghosted = P1MapMenuBar.Ghosted(bar, shown, i, outdoors);
            var lit = i == state.Item && !ghosted;
            if (lit)
                Fill(batch, pixel, rowBox, pens[bar.HighlightPen]);
            var label = UiText.S(openMenu.Items[i]);
            PixelFont.Draw(batch, pixel, openMenu.ItemX, y + 1, label, pens[lit ? bar.HighlightTextPen : bar.ItemPen]);
            if (ghosted)
                Ghost(batch, pixel, bar.GhostPattern,
                    new Rectangle(openMenu.ItemX, y + 1, PixelFont.Measure(label), PixelFont.Font.Height), pens[bar.BoxPen]);
            if (input)
                Hits?.Add(new HitRegion(rowBox, new P1MapMenuItemTag(i)));
        }
    }

    /// <summary>The string ids the map bar of <paramref name="layout"/> shows (titles and items, named by the pack).</summary>
    public static IEnumerable<string> P1MapBarStringKeys(MapMessageLayout? layout) =>
        layout?.MenuBar?.Menus.SelectMany(m => m.Items.Prepend(m.Title)) ?? [];

    /// <summary>Intuition's ghosting: the pattern's set cells (anchored at the screen's origin) cleared in <paramref name="area"/>.</summary>
    private static void Ghost(SpriteBatch batch, Texture2D pixel, IReadOnlyList<string> pattern, Rectangle area, Color ground)
    {
        if (pattern.Count == 0)
            return;
        for (var y = area.Top; y < area.Bottom; y++)
        {
            var row = pattern[y % pattern.Count];
            for (var x = area.Left; x < area.Right; x++)
                if (row[x % row.Length] == '#')
                    Fill(batch, pixel, new Rectangle(x, y, 1, 1), ground);
        }
    }

    /// <summary>
    /// s155: where I Amiga's map line puts the remaster's text (pack <c>map_message.line</c>, h80 <c>+0x84a(text, 3, 192)</c>):
    /// each line centred on the line's width by its length in <paramref name="font"/>, at TopEdge 192. Text longer than the
    /// clear's cells, or with several lines (the remaster's alone: the Amiga's line holds one MESS record of 40), wraps at a
    /// word and stacks upward one font height a line, the last line at 192, so every line stays whole. The key help of a
    /// dialog (<paramref name="help"/>, the remaster's own) goes above the message, <see cref="P1MapHelpGap"/> rows clear of
    /// it (s183: before, the help's last row touched the message's first), or on the line itself when there is none. On the
    /// overworld, whose map reaches the line, the help goes on the bar instead (<see cref="P1MapHelpOnBar"/>).
    /// </summary>
    /// <remarks>
    /// s156: <paramref name="record"/> is the MESS record the line shows (a dungeon message cell's, as stored): a message row
    /// equal to it trimmed is placed as the Amiga places the record, centred by its <see cref="MapMessageLine.RecordCells"/>
    /// stored cells (blanks included), its leading blanks moving the ink right.
    /// </remarks>
    public static IReadOnlyList<(int X, int Y, string Text, bool Help)> P1MapLineRows(MapMessageLine line, IFontSource font,
        string? message, string? help, string? record = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(font);
        static IEnumerable<string> Split(string? text, int cells, IFontSource f) => (text ?? "").Split('\n')
            .Select(l => l.Trim()).Where(l => l.Length > 0)
            .SelectMany(l => PixelFont.Measure(l, f) <= cells * PixelFont.Advance ? [l] : Wrap(l, cells, f));
        var rows = Split(help, line.ClearCells, font).Select(t => (t, true))
            .Concat(Split(message, line.ClearCells, font).Select(t => (t, false))).ToList();
        var pitch = font.Height;
        var messageRows = rows.Count(r => !r.Item2);
        var gap = messageRows > 0 && messageRows < rows.Count ? P1MapHelpGap : 0;
        var stored = record is { Length: > 0 } rec && line.RecordCells > 0 && rec.Trim().Length > 0 ? rec.PadRight(line.RecordCells) : null;
        int X(string t, bool isHelp) => !isHelp && stored is not null && t == stored.Trim()
            ? (line.CentreWidth - PixelFont.Measure(stored, font)) / 2 + PixelFont.Measure(stored[..stored.IndexOf(t, StringComparison.Ordinal)], font)
            : (line.CentreWidth - PixelFont.Measure(t, font)) / 2;
        return [.. rows.Select((r, i) => (X(r.t, r.Item2), line.TopEdge - (rows.Count - 1 - i) * pitch - (r.Item2 ? gap : 0), r.t, r.Item2))];
    }

    /// <summary>s183: blank rows between a dialog's help and the message under it on I Amiga's map line.</summary>
    public const int P1MapHelpGap = 2;

    /// <summary>
    /// s183: on I Amiga's overworld map screen the map runs down to the line (TopEdge 192), so a dialog's help stacked above
    /// the message hid the map's last rows and the frame. While the bar takes no input (a dialog, a question or a box owns
    /// the keys, no menu pulled down) and both a message and a help show, the help goes on the bar's row instead (the
    /// titles and the gold line give way until the dialog closes) when it fits there on one row; else null (the line
    /// takes both, as before). Returns the help's place: x (centred), y (the bar's middle).
    /// </summary>
    public static (int X, int Y, string Text)? P1MapHelpOnBar(MapMessageLayout layout, IFontSource font, string? message,
        string? help, bool barIdle)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(font);
        var text = (help ?? "").Trim();
        if (!barIdle || layout.MenuBar is not { } bar || layout.Line is not { } line || string.IsNullOrWhiteSpace(message)
            || text.Length == 0 || text.Contains('\n') || PixelFont.Measure(text, font) > line.ClearCells * PixelFont.Advance
            || font.Height > bar.Rows)
            return null;
        return ((320 - PixelFont.Measure(text, font)) / 2, (bar.Rows - font.Height) / 2, text);
    }

    /// <summary>
    /// s183: the bar's row with a dialog's help on it (<see cref="P1MapHelpOnBar"/>): the bar's ground, the help in the line's
    /// font and the bar's at-rest pen (13), where the titles stand at rest.
    /// </summary>
    public static void DrawP1MapBarHelp(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, (int X, int Y, string Text) help)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.MenuBar is not { } bar)
            return;
        Color[] pens = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
        using var scope = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        Fill(batch, pixel, new Rectangle(0, 0, 320, bar.Rows), pens[bar.BarPen]);
        PixelFont.Draw(batch, pixel, help.X, help.Y, help.Text, pens[bar.TextPen]);
    }

    /// <summary>
    /// s156: the overworld's pen-13 frame (<see cref="MapMessageLayout.OverworldFrame"/>), s158: the dungeon's pen-10 one
    /// (<see cref="MapMessageLayout.DungeonFrame"/>, <paramref name="dungeon"/>): the nested outlines, outermost first.
    /// </summary>
    public static void DrawP1MapFrame(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, bool dungeon = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if ((dungeon ? layout.DungeonFrame : layout.OverworldFrame) is not { } f)
            return;
        var pen = VirtualScreen.ParseColor(layout.Palette[f.Pen]);
        for (var i = 0; i < f.Thickness; i++)
            Outline(batch, pixel, new Rectangle(f.Box.X + i, f.Box.Y + i, f.Box.W - 2 * i, f.Box.H - 2 * i), pen);
    }

    /// <summary>
    /// s155: the map line (<see cref="P1MapLineRows"/>): per row the blank clear (pen 0 over the clear's cells, the font's
    /// height) and the text in the line's pen; the help in the menu bar's at-rest pen (13).
    /// </summary>
    public static void DrawP1MapLine(SpriteBatch batch, Texture2D pixel, MapMessageLayout layout, string? message, string? help,
        string? record = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Line is not { } line)
            return;
        Color[] pens = [.. layout.Palette.Select(VirtualScreen.ParseColor)];
        using var scope = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        var font = PixelFont.Font;
        var helpPen = layout.MenuBar?.TextPen ?? line.Pen;
        foreach (var (x, y, text, isHelp) in P1MapLineRows(line, font, message, help, record))
        {
            Fill(batch, pixel, new Rectangle(line.ClearX, y, line.ClearCells * font.Glyph(' ').Advance, font.Height), pens[0]);
            PixelFont.Draw(batch, pixel, x, y, text, pens[isHelp ? helpPen : line.Pen]);
        }
    }
}
