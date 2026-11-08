using System.Runtime.CompilerServices;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s162: P4.X's <c>_wfnt</c> style passes on a font's glyphs (FORMAT.md 6.12, static <c>[C]</c>), for places whose style
/// the pack has no font file of its own for: bit 0 <b>bold</b> (<c>0x3A034</c>: row |= row &gt;&gt; 1, the glyph one column
/// wider, advance + 1) and bit 3 <b>shadow</b> (<c>0x3A236</c>: for each ink pixel (x, y) the pixels (x + 1, y),
/// (x, y + 1), (x + 2, y + 1), (x + 1, y + 2), (x + 3, y + 2) are set, then (x + 1, y + 1) of every ink pixel cleared: a
/// hollow outline of the glyph one pixel right and down with a pixel of drop shadow; two rows more, advance unchanged).
/// Bits 1 (italic) and 2 (outline) are not applied here (the pack's italic file stands for the one italic place).
/// </summary>
public static class X68kFontStyle
{
    public const int Bold = 1, Shadow = 8;

    private static readonly ConditionalWeakTable<IFontSource, Dictionary<int, IFontSource>> Cache = new();

    /// <summary><paramref name="font"/> with <paramref name="style"/>'s bold / shadow passes, or the font itself for none.</summary>
    public static IFontSource With(IFontSource font, int style)
    {
        ArgumentNullException.ThrowIfNull(font);
        style &= Bold | Shadow;
        if (style == 0)
            return font;
        var byStyle = Cache.GetOrCreateValue(font);
        lock (byStyle)
        {
            if (!byStyle.TryGetValue(style, out var styled))
                byStyle[style] = styled = new StyledFont(font, style);
            return styled;
        }
    }

    /// <summary>The bold pass on one glyph (rows as masks, MSB = the left column).</summary>
    public static GlyphDefinition BoldGlyph(GlyphDefinition g)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (g.Width >= 32)
            return g;
        return new GlyphDefinition(g.Width + 1, g.Advance + 1, [.. g.Rows.Select(r => (r << 1) | r)]);
    }

    /// <summary>The shadow pass on one glyph.</summary>
    public static GlyphDefinition ShadowGlyph(GlyphDefinition g)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (g.Width + 3 > 32)
            return g;
        var rows = new uint[g.Rows.Count + 2];
        for (var y = 0; y < g.Rows.Count; y++)
        {
            var r = g.Rows[y]; // in the new width, column c of the old glyph is bit (width + 2 - c): r << 3 is column + 0
            rows[y] |= r << 2;
            rows[y + 1] |= (r << 3) | (r << 1);
            rows[y + 2] |= r | (r << 2);
        }
        for (var y = 0; y < g.Rows.Count; y++)
            rows[y + 1] &= ~(g.Rows[y] << 2);
        return new GlyphDefinition(g.Width + 3, g.Advance, rows);
    }

    private sealed class StyledFont(IFontSource inner, int style) : IFontSource
    {
        private readonly Dictionary<int, GlyphDefinition> _glyphs = [];

        public string Id => $"{inner.Id}+style{style}";
        public int Height => inner.Height + ((style & Shadow) != 0 ? 2 : 0);
        public int LineHeight => inner.LineHeight;

        public GlyphDefinition Glyph(int codePoint)
        {
            lock (_glyphs)
            {
                if (_glyphs.TryGetValue(codePoint, out var g))
                    return g;
                g = inner.Glyph(codePoint);
                if ((style & Bold) != 0)
                    g = BoldGlyph(g);
                if ((style & Shadow) != 0)
                    g = ShadowGlyph(g);
                return _glyphs[codePoint] = g;
            }
        }
    }
}
