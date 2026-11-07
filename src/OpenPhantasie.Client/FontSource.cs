using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Where <see cref="PixelFont"/> gets its glyphs and widths (T1.5 font seam). The active graphics pack picks one
/// (<see cref="FontSources.For"/>); the remaster 5×7 font is the default. Glyphs are looked up by Unicode code point and
/// may have any width, so a later game or platform (IV's Japanese text) brings its own set.
/// </summary>
public interface IFontSource
{
    string Id { get; }
    /// <summary>Glyph cell height in pixels.</summary>
    int Height { get; }
    /// <summary>Distance between text lines in pixels.</summary>
    int LineHeight { get; }
    /// <summary>The glyph drawn for <paramref name="codePoint"/>: its own, a case-folded one, a blank space or the fallback.</summary>
    GlyphDefinition Glyph(int codePoint);
}

/// <summary>A font made of <see cref="GlyphDefinition"/>s (a <see cref="FontDefinition"/>, built in or read from a pack).</summary>
public sealed class BitmapFont : IFontSource
{
    private readonly FontDefinition _def;
    private readonly GlyphDefinition _space;
    private readonly GlyphDefinition _fallback;

    public BitmapFont(FontDefinition definition)
    {
        _def = definition;
        _fallback = definition.Glyphs[definition.Fallback];
        _space = definition.Glyphs.TryGetValue(' ', out var s)
            ? s
            : new GlyphDefinition(0, definition.SpaceAdvance, []);
    }

    public string Id => _def.Id;
    public int Height => _def.Height;
    public int LineHeight => _def.LineHeight;

    /// <summary>True when the font has its own glyph for <paramref name="codePoint"/> (a case-folded one counts; the fallback does not).</summary>
    public bool Has(int codePoint) =>
        codePoint == ' ' || _def.Glyphs.ContainsKey(codePoint) ||
        (_def.FoldToUpper && System.Text.Rune.IsValid(codePoint) &&
         _def.Glyphs.ContainsKey(System.Text.Rune.ToUpperInvariant(new System.Text.Rune(codePoint)).Value));

    public GlyphDefinition Glyph(int codePoint)
    {
        if (codePoint == ' ')
            return _space;
        if (_def.Glyphs.TryGetValue(codePoint, out var g))
            return g;
        if (_def.FoldToUpper && System.Text.Rune.IsValid(codePoint) &&
            System.Text.Rune.ToUpperInvariant(new System.Text.Rune(codePoint)).Value is var up && up != codePoint &&
            _def.Glyphs.TryGetValue(up, out g))
            return g;
        return _fallback;
    }
}

/// <summary>
/// An original's font with a stand-in for what it lacks (s135: IV X68000's P4.X ANK font has no kanji, which the original
/// takes from the CG ROM): a glyph <see cref="Primary"/> has comes from it, any other from <see cref="Secondary"/>.
/// Height and line height are the primary's.
/// </summary>
public sealed class FallbackFont(BitmapFont primary, IFontSource secondary) : IFontSource
{
    public BitmapFont Primary { get; } = primary;
    public IFontSource Secondary { get; } = secondary;
    public string Id => Primary.Id;
    public int Height => Primary.Height;
    public int LineHeight => Primary.LineHeight;
    public GlyphDefinition Glyph(int codePoint) => Primary.Has(codePoint) ? Primary.Glyph(codePoint) : Secondary.Glyph(codePoint);
}

/// <summary>The fonts the Client can draw: built-in ones by id, or a pack's own font file.</summary>
public static class FontSources
{
    private static readonly Dictionary<string, IFontSource> BuiltIn = new(StringComparer.Ordinal)
    {
        [PixelFont.Id] = PixelFont.Remaster5x7,
    };

    private static readonly Dictionary<string, IFontSource?> Loaded = new(StringComparer.Ordinal);

    /// <summary>The built-in font <paramref name="id"/>, or null.</summary>
    public static IFontSource? BuiltInFont(string id) => BuiltIn.GetValueOrDefault(id);

    /// <summary>
    /// The font <paramref name="pack"/> names: a built-in one, else the pack's <c>fonts/&lt;id&gt;.json</c>; null when
    /// neither exists or the file is unreadable (such a pack is not drawable). No pack → the default font.
    /// </summary>
    public static IFontSource? For(GraphicsPack? pack)
    {
        if (pack is null)
            return PixelFont.Remaster5x7;
        if (BuiltIn.TryGetValue(pack.Font, out var font))
            return font;
        return pack.FontFile is { } file ? Load(file) : null;
    }

    /// <summary>
    /// A font a pack names for one screen (<c>combat_field.font</c>): built-in <paramref name="id"/>, else
    /// <paramref name="file"/> (read once); null when there is no id or neither exists / reads.
    /// </summary>
    public static IFontSource? Named(string? id, string? file)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        if (BuiltIn.TryGetValue(id, out var font))
            return font;
        return file is null ? null : Load(file);
    }

    /// <summary>
    /// The font <paramref name="pack"/> names for <paramref name="role"/> (<see cref="GraphicsPack.RoleFonts"/>), with the
    /// pack's own font standing in for the glyphs it lacks; null when the pack names none or its file does not read.
    /// </summary>
    public static IFontSource? ForRole(GraphicsPack? pack, string role)
    {
        if (pack?.RoleFonts.GetValueOrDefault(role) is not { } named || Named(named.Id, named.File) is not { } font)
            return null;
        if (font is not BitmapFont bitmap || For(pack) is not { } own || ReferenceEquals(own, font))
            return font;
        lock (Chained)
        {
            if (!Chained.TryGetValue((bitmap, own), out var chained))
                Chained[(bitmap, own)] = chained = new FallbackFont(bitmap, own);
            return chained;
        }
    }

    private static readonly Dictionary<(BitmapFont, IFontSource), FallbackFont> Chained = [];

    private static IFontSource? Load(string file)
    {
        lock (Loaded)
        {
            if (Loaded.TryGetValue(file, out var cached))
                return cached;
            try
            {
                cached = new BitmapFont(FontDefinition.Load(file));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                cached = null;
            }
            return Loaded[file] = cached;
        }
    }

    /// <summary>True when the Client can draw <paramref name="pack"/>'s text.</summary>
    public static bool CanDraw(GraphicsPack pack) => For(pack) is not null;
}
