using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenPhantasie.Client;

/// <summary>
/// IV's item icons in the box (s151, T6.4; FORMAT.md section 6.9 "Item lists"): the original's item lists (the Armory
/// <c>_itemlist</c>, Share out <c>_dvitmlst</c>, Trade / U Use / Identify <c>_tritmlst</c>) draw each item's 32 × 32 font-2
/// glyph <c>_idat</c> b0 + 0x20 in colour 0 on the window's grey (colour 14), in 32-pixel rows with the text centred
/// beside it. The remaster's box keeps its text lines; a line with an icon (<see cref="MenuLine.Icon"/>, the pack's
/// <c>p4.item.N</c>) takes as many text rows as the icon needs (two at the pack's scale), the icon on a colour-14 cell at
/// the left, the text after it, centred on the icon when it fits one row. Without the pack nothing changes.
/// `[~]`: the original puts the row's letter (colour 2) left of the icon; the remaster's digit and letter come after it.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The rows <c>_dvitmlst</c> shows: the item being placed and the next four of the queue.</summary>
    public const int DistributeRows = 5;

    /// <summary>Layout pixels between the icon cell and the text.</summary>
    private const int IconGap = 3;

    /// <summary>
    /// One drawn row of a text area. In an icon row's block every row has <see cref="Owner"/> = the block's first row (−1
    /// outside a block); the first row carries the <see cref="Icon"/> and the block's height <see cref="Block"/>.
    /// </summary>
    private readonly record struct BoxRow(string Text, Color Color, bool Selected = false, Texture2D? Icon = null,
        int Indent = 0, int Dy = 0, int Owner = -1, int Block = 0, bool Lit = false);

    /// <summary>The pack picture of item <paramref name="item"/>'s icon (<c>p4.item.</c> + <c>_idat</c> b0), or null for none.</summary>
    private string? ItemIconName(int item) =>
        item != 0 && Game.Services?.Items.Icon(item) is int n ? $"p4.item.{n}" : null;

    /// <summary>The icon's size in layout pixels at the pack's scale.</summary>
    private Point IconCell(Texture2D icon)
    {
        var k = 1f / Assets!.PackPixelsPerLayoutPixel;
        return new((int)Math.Ceiling(icon.Width * k), (int)Math.Ceiling(icon.Height * k));
    }

    /// <summary>
    /// Text rows an icon <paramref name="iconHeight"/> layout pixels high takes: the fewest whose last row's glyph ends at or
    /// below the icon (rows are <see cref="PixelFont.LineHeight"/> apart, the last one only <see cref="PixelFont.GlyphHeight"/>
    /// high), so a block that is in view never crosses the box's edge.
    /// </summary>
    public static int IconRows(int iconHeight) =>
        (Math.Max(0, iconHeight - PixelFont.GlyphHeight) + PixelFont.LineHeight - 1) / PixelFont.LineHeight + 1;

    /// <summary>
    /// <paramref name="line"/>'s rows, wrapped to <paramref name="width"/> pixels; a line with a loaded icon becomes a block
    /// of at least <see cref="IconRows"/> rows, its text indented past the icon (never cut: it wraps in the narrower room).
    /// A block taller than the box's <paramref name="visible"/> rows (a narrow column) could never be shown whole, so that
    /// line stays plain text without its icon.
    /// </summary>
    private void AddBoxRows(List<BoxRow> rows, MenuLine line, Color color, int width, int visible)
    {
        var selected = line.Style == MenuLineStyle.Selected;
        var icon = line.Icon is { } name && Assets is not null ? Assets.Get(name) : null;
        var cell = icon is null ? Point.Zero : IconCell(icon);
        var indent = cell.X + IconGap;
        var parts = BoxLineRows(line.Text, Math.Max(1, (width - indent) / PixelFont.Advance));
        var block = Math.Max(parts.Count, IconRows(cell.Y));
        if (icon is null || block > visible)
        {
            foreach (var part in BoxLineRows(line.Text, width / PixelFont.Advance))
                rows.Add(new(part, color, selected));
            return;
        }
        var owner = rows.Count;
        // One text row: centred on the icon, as the original's text sits 8 pixels down its 32-pixel row.
        var dy = parts.Count == 1 ? Math.Max(0, (cell.Y - PixelFont.GlyphHeight) / 2) : 0;
        for (var i = 0; i < block; i++)
            rows.Add(new(i < parts.Count ? parts[i] : "", color, selected && i < parts.Count, i == 0 ? icon : null, indent,
                dy, owner, i == 0 ? block : 0, line.Lit));
    }

    /// <summary>Spaces before a wrapped menu entry's continuation rows (s185).</summary>
    public const string EntryIndent = " ";

    /// <summary>
    /// <paramref name="text"/>'s rows in a box <paramref name="cols"/> columns wide. Prose wraps by words
    /// (<see cref="UiOverlays.Wrap(string, int)"/>). s185 (Pad leftovers: "W:WITHDRA / W" in the narrow column beside the
    /// picture and the Command strip): a menu entry (<see cref="Option"/> / <see cref="DimOption"/>: the cursor mark '&gt;' or
    /// a space first) wraps at word boundaries only, a word too wide for a row breaking after its key's ':' (or a '/' or
    /// '-'), and its continuation rows are indented by <see cref="EntryIndent"/> (none where the piece fills the row): "2
    /// W:" / " WITHDRAW". Only a piece wider than the whole row (a long run with no break, as Japanese text) is cut between
    /// characters; nothing is dropped. A row that fits is as before (words joined by one space, the leading mark space
    /// dropped).
    /// </summary>
    public static IReadOnlyList<string> BoxLineRows(string text, int cols)
    {
        text ??= "";
        if (text.Length == 0)
            return [""];
        var max = Math.Max(1, cols) * PixelFont.Advance;
        var plain = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (text.Length < 2 || text[0] is not ('>' or ' ') || PixelFont.Measure(plain) <= max)
            return [.. UiOverlays.Wrap(text, Math.Max(1, cols))];
        var rows = new List<string>();
        var line = ""; // the row being filled; "" = a row not yet started
        bool Fits(string s) => PixelFont.Measure(s) <= max;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > 0)
            {
                if (line.Length > 0)
                {
                    if (Fits(line + " " + w))
                    {
                        line += " " + w;
                        break;
                    }
                    if (Fits(EntryIndent + w))
                    {
                        rows.Add(line);
                        line = EntryIndent + w;
                        break;
                    }
                    // Wider than a continuation row: its head up to a break mark ends this row when it fits there.
                    var head = BreakAt(w, line + " ", Fits);
                    rows.Add(head > 0 ? line + " " + w[..head] : line);
                    line = "";
                    w = w[head..];
                    continue;
                }
                var lead = rows.Count > 0 && Fits(EntryIndent + w) ? EntryIndent : "";
                if (Fits(w))
                {
                    line = lead + w;
                    break;
                }
                lead = rows.Count > 0 ? EntryIndent : "";
                var cut = BreakAt(w, lead, Fits);
                if (cut == 0)
                {
                    lead = "";
                    cut = BreakAt(w, lead, Fits);
                }
                if (cut == 0) // no break mark fits: cut between characters (nothing is lost)
                    cut = Math.Max(1, PixelFont.CutLength(w, max));
                rows.Add(lead + w[..cut]);
                w = w[cut..];
            }
        }
        if (line.Length > 0)
            rows.Add(line);
        return rows;
    }

    /// <summary>
    /// The length of <paramref name="word"/>'s longest head ending in ':' , '/' or '-' (not its last character) that fits
    /// after <paramref name="before"/>; 0 when none does.
    /// </summary>
    private static int BreakAt(string word, string before, Func<string, bool> fits)
    {
        for (var i = word.Length - 2; i >= 0; i--)
            if (word[i] is ':' or '/' or '-' && fits(before + word[..(i + 1)]))
                return i + 1;
        return 0;
    }

    /// <summary>
    /// The icon on its window-grey cell (colour 14 of the pack's palette) at <paramref name="at"/>, pack scale. s158:
    /// <paramref name="lit"/> draws it as <c>_wxbox(win, 15, …)</c> leaves it (Share out's row 0): every colour XOR 15, so
    /// the grey cell turns colour 1 and the black ink colour 15.
    /// </summary>
    private void DrawItemIcon(SpriteBatch batch, Texture2D pixel, Texture2D icon, Vector2 at, bool lit = false)
    {
        var cell = IconCell(icon);
        var grey = Assets!.PaletteColour("pkh", lit ? 14 ^ ShareOutXor : 14) ?? new Color(0xC2, 0xC2, 0xC2);
        Fill(batch, pixel, new Rectangle((int)at.X, (int)at.Y, cell.X, cell.Y), grey);
        batch.Draw(lit ? LitIcon(icon, ShareOutXor) : icon, at, null, Color.White, 0f, Vector2.Zero,
            1f / Assets.PackPixelsPerLayoutPixel, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Share out's highlight (s158, <c>_dvitmlst</c>: <c>_wxbox(win, 15, 16, 32, 280, 32)</c> over row 0, the item being
    /// placed): XOR with colour 15. On the window's grey (14) that is colour 1 with the black ink (0) turned colour 15.
    /// </summary>
    public const int ShareOutXor = 15;
}
