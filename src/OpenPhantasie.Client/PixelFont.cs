using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Text drawing on the virtual canvas. Glyphs and widths come from the active <see cref="IFontSource"/> (T1.5 font seam:
/// the graphics pack picks it, <see cref="Use"/>); the default is the remaster 5×7 font. Measuring, fitting and
/// wrapping go through the font's widths. The shared layout is authored on a 6×9 text cell (<see cref="Advance"/>,
/// <see cref="LineHeight"/>): a column budget is <c>columns × Advance</c> pixels, whatever font fills it.
/// </summary>
public static class PixelFont
{
    /// <summary>Font id graphics packs name (<c>"font"</c> in a pack manifest) for the built-in 5×7 font.</summary>
    public const string Id = "remaster-5x7";

    /// <summary>The remaster 5×7 font's glyph size.</summary>
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    /// <summary>The shared layout's text cell: column pitch and line pitch (the 5×7 font's advance and line height).</summary>
    public const int Advance = 6;
    public const int LineHeight = 9;

    /// <summary>The built-in remaster font: capitals, digits and punctuation, 5×7 on a 6-pixel advance.</summary>
    public static IFontSource Remaster5x7 { get; } = new BitmapFont(BuildRemaster5x7());

    /// <summary>The font text is drawn and measured with (the active pack's; <see cref="Remaster5x7"/> by default).</summary>
    public static IFontSource Font { get; private set; } = Remaster5x7;

    /// <summary>Draw and measure with <paramref name="font"/> from now on (null = the default font).</summary>
    public static void Use(IFontSource? font) => Font = font ?? Remaster5x7;

    /// <summary>
    /// Draw and measure with <paramref name="font"/> until the returned scope is disposed, then go back to the font before
    /// (one screen drawn in the original's own font, s134: I Amiga's combat screen). Null keeps the current font.
    /// </summary>
    public static FontScope With(IFontSource? font)
    {
        var scope = new FontScope(Font);
        if (font is not null)
            Font = font;
        return scope;
    }

    /// <summary>Restores the font <see cref="With"/> replaced.</summary>
    public readonly struct FontScope(IFontSource previous) : IDisposable
    {
        public void Dispose() => Font = previous;
    }

    /// <summary>Width of <paramref name="text"/> in pixels (sum of the glyph advances).</summary>
    public static int Measure(string text) => Measure(text, Font);

    public static int Measure(string text, IFontSource font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var w = 0;
        foreach (var r in text.EnumerateRunes())
            w += font.Glyph(r.Value).Advance;
        return w;
    }

    /// <summary>
    /// How many UTF-16 chars of <paramref name="text"/> are shown in <paramref name="maxWidth"/> pixels, and whether it
    /// had to be cut (a cut text leaves room for a trailing "."; at least <paramref name="minChars"/> characters stay).
    /// </summary>
    public static (int Length, bool Cut) FitLength(string text, int maxWidth, int minChars = 0) =>
        FitLength(text, maxWidth, minChars, Font);

    public static (int Length, bool Cut) FitLength(string text, int maxWidth, int minChars, IFontSource font)
    {
        if (string.IsNullOrEmpty(text)) return (0, false);
        if (Measure(text, font) <= maxWidth) return (text.Length, false);
        var budget = maxWidth - Measure(".", font);
        int w = 0, end = 0, kept = 0;
        foreach (var r in text.EnumerateRunes())
        {
            var a = font.Glyph(r.Value).Advance;
            if (w + a > budget && kept >= minChars) break;
            w += a;
            end += r.Utf16SequenceLength;
            kept++;
        }
        return (end, true);
    }

    /// <summary><paramref name="text"/> if it fits <paramref name="maxWidth"/> pixels, else cut with a trailing ".".</summary>
    public static string Fit(string text, int maxWidth, int minChars = 0) => Fit(text, maxWidth, minChars, Font);

    public static string Fit(string text, int maxWidth, int minChars, IFontSource font)
    {
        var (length, cut) = FitLength(text, maxWidth, minChars, font);
        return cut ? text[..length] + "." : text ?? "";
    }

    /// <summary>
    /// How many UTF-16 chars of <paramref name="text"/> fit in <paramref name="maxWidth"/> pixels, cut without a mark
    /// (whole runes only).
    /// </summary>
    public static int CutLength(string text, int maxWidth) => CutLength(text, maxWidth, Font);

    public static int CutLength(string text, int maxWidth, IFontSource font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int w = 0, end = 0;
        foreach (var r in text.EnumerateRunes())
        {
            w += font.Glyph(r.Value).Advance;
            if (w > maxWidth) break;
            end += r.Utf16SequenceLength;
        }
        return end;
    }

    /// <summary>
    /// <paramref name="text"/> if it fits <paramref name="maxWidth"/> pixels, else its end behind a leading "..." (a long
    /// path keeps its last folders).
    /// </summary>
    public static string Tail(string text, int maxWidth) => Tail(text, maxWidth, Font);

    public static string Tail(string text, int maxWidth, IFontSource font)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (Measure(text, font) <= maxWidth) return text;
        var budget = maxWidth - Measure("...", font);
        var runes = text.EnumerateRunes().ToArray();
        int w = 0, start = text.Length;
        for (var i = runes.Length - 1; i >= 0; i--)
        {
            w += font.Glyph(runes[i].Value).Advance;
            if (w > budget) break;
            start -= runes[i].Utf16SequenceLength;
        }
        return "..." + text[start..];
    }

    /// <summary>
    /// Pixel offset of column <paramref name="column"/> (a UTF-16 index) in <paramref name="text"/>; past the end the
    /// layout's cell pitch continues.
    /// </summary>
    public static int ColumnX(string text, int column) => ColumnX(text, column, Font);

    public static int ColumnX(string text, int column, IFontSource font)
    {
        if (column <= 0) return column * Advance;
        text ??= "";
        if (column >= text.Length) return Measure(text, font) + (column - text.Length) * Advance;
        return Measure(text[..column], font);
    }

    /// <summary>The column (UTF-16 index) under pixel <paramref name="x"/> of <paramref name="text"/>; past the end the
    /// layout's cell pitch continues. Negative x is column 0.</summary>
    public static int ColumnAt(string text, int x) => ColumnAt(text, x, Font);

    public static int ColumnAt(string text, int x, IFontSource font)
    {
        if (x < 0) return 0;
        text ??= "";
        int w = 0, i = 0;
        foreach (var r in text.EnumerateRunes())
        {
            var a = font.Glyph(r.Value).Advance;
            if (x < w + a) return i;
            w += a;
            i += r.Utf16SequenceLength;
        }
        return text.Length + (x - w) / Advance;
    }

    public static void Draw(
        SpriteBatch batch,
        Texture2D pixel,
        int x,
        int y,
        string text,
        Color color,
        int scale = 1)
    {
        if (string.IsNullOrEmpty(text) || scale < 1) return;
        var font = Font;
        var cx = x;
        foreach (var r in text.EnumerateRunes())
        {
            var glyph = font.Glyph(r.Value);
            for (var row = 0; row < glyph.Rows.Count; row++)
            {
                var bits = glyph.Rows[row];
                if (bits == 0) continue;
                for (var col = 0; col < glyph.Width; col++)
                {
                    if ((bits & (1u << (glyph.Width - 1 - col))) == 0) continue;
                    batch.Draw(
                        pixel,
                        new Rectangle(cx + col * scale, y + row * scale, scale, scale),
                        color);
                }
            }

            cx += glyph.Advance * scale;
        }
    }

    /// <summary>
    /// Draw <paramref name="text"/> in <paramref name="font"/> with each font pixel <paramref name="pixelSize"/> layout
    /// pixels (any size, so an original's font sits on its own art at the art's scale: IV X68000's 640×400 pack at ½,
    /// s135). <paramref name="at"/> is the top-left of the first glyph cell in layout pixels.
    /// </summary>
    public static void DrawScaled(SpriteBatch batch, Texture2D pixel, Vector2 at, string text, Color color, float pixelSize,
        IFontSource font)
    {
        if (string.IsNullOrEmpty(text) || pixelSize <= 0) return;
        var pen = 0;
        var size = new Vector2(pixelSize);
        foreach (var r in text.EnumerateRunes())
        {
            var glyph = font.Glyph(r.Value);
            for (var row = 0; row < glyph.Rows.Count; row++)
            {
                var bits = glyph.Rows[row];
                if (bits == 0) continue;
                for (var col = 0; col < glyph.Width; col++)
                    if ((bits & (1u << (glyph.Width - 1 - col))) != 0)
                        batch.Draw(pixel, at + new Vector2((pen + col) * pixelSize, row * pixelSize), null, color, 0f,
                            Vector2.Zero, size, SpriteEffects.None, 0f);
            }
            pen += glyph.Advance;
        }
    }

    /// <summary>Draw <paramref name="text"/> in <paramref name="maxChars"/> layout columns, cut with "." when longer.</summary>
    public static void DrawWrapped(
        SpriteBatch batch,
        Texture2D pixel,
        int x,
        int y,
        int maxChars,
        string text,
        Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Draw(batch, pixel, x, y, Fit(text, maxChars * Advance), color);
    }

    // Each glyph is 7 rows of 5 pixels ('#' = ink), drawn on a 6-pixel advance.
    private static FontDefinition BuildRemaster5x7()
    {
        var g = new Dictionary<int, GlyphDefinition>();
        void Add(char ch, params string[] rows) => g[ch] = GlyphDefinition.FromRows(rows, Advance);

        Add('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
        Add('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
        Add('2', ".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####");
        Add('3', ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###.");
        Add('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
        Add('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
        Add('6', "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###.");
        Add('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
        Add('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
        Add('9', ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##..");

        Add('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        Add('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
        Add('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
        Add('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
        Add('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
        Add('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
        Add('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.");
        Add('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        Add('I', ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###.");
        Add('J', "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##..");
        Add('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
        Add('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
        Add('M', "#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#");
        Add('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
        Add('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        Add('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
        Add('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
        Add('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
        Add('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
        Add('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
        Add('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        Add('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
        Add('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
        Add('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
        Add('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
        Add('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");

        Add('.', ".....", ".....", ".....", ".....", ".....", ".##..", ".##..");
        Add(',', ".....", ".....", ".....", ".....", ".##..", "..#..", ".#...");
        Add('!', "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#..");
        Add('?', ".###.", "#...#", "....#", "..##.", "..#..", ".....", "..#..");
        Add(':', ".....", ".##..", ".##..", ".....", ".##..", ".##..", ".....");
        Add(';', ".....", ".##..", ".##..", ".....", ".##..", "..#..", ".#...");
        Add('-', ".....", ".....", ".....", "#####", ".....", ".....", ".....");
        Add('+', ".....", "..#..", "..#..", "#####", "..#..", "..#..", ".....");
        Add('=', ".....", ".....", "#####", ".....", "#####", ".....", ".....");
        Add('/', "....#", "...#.", "..#..", ".#...", "#....", ".....", ".....");
        Add('%', "#...#", "#..#.", "..#..", ".#...", ".#..#", "#...#", ".....");
        Add('#', ".#.#.", "#####", ".#.#.", ".#.#.", "#####", ".#.#.", ".....");
        Add('(', "..##.", ".#...", "#....", "#....", "#....", ".#...", "..##.");
        Add(')', ".##..", "...#.", "....#", "....#", "....#", "...#.", ".##..");
        Add('[', ".###.", ".#...", ".#...", ".#...", ".#...", ".#...", ".###.");
        Add(']', ".###.", "...#.", "...#.", "...#.", "...#.", "...#.", ".###.");
        Add('>', ".....", ".#...", "..#..", "...#.", "..#..", ".#...", ".....");
        Add('<', ".....", "...#.", "..#..", ".#...", "..#..", "...#.", ".....");
        Add('\'', ".##..", ".##..", "..#..", ".....", ".....", ".....", ".....");
        Add('"', "#.#..", "#.#..", ".....", ".....", ".....", ".....", ".....");
        Add('*', ".....", ".#.#.", "..#..", "#####", "..#..", ".#.#.", ".....");
        Add('@', ".###.", "#...#", "#.###", "#.#.#", "#.###", "#....", ".####");
        Add('_', ".....", ".....", ".....", ".....", ".....", ".....", "#####");
        Add('|', "..#..", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");

        return new FontDefinition
        {
            Id = Id,
            Height = GlyphHeight,
            LineHeight = LineHeight,
            SpaceAdvance = Advance,
            Fallback = '?',
            FoldToUpper = true,
            Glyphs = g,
        };
    }
}
