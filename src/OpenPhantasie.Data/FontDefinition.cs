using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// One glyph of a bitmap font: <see cref="Rows"/> are <see cref="Width"/>-bit masks, most significant bit = leftmost
/// pixel (up to 32 pixels wide); <see cref="Advance"/> is how far the pen moves after it.
/// </summary>
public sealed record GlyphDefinition(int Width, int Advance, IReadOnlyList<uint> Rows)
{
    public bool IsSet(int column, int row) =>
        row >= 0 && row < Rows.Count && column >= 0 && column < Width && (Rows[row] & (1u << (Width - 1 - column))) != 0;

    /// <summary>A glyph from <c>#</c>/<c>.</c> rows (any other character is blank); width = the longest row.</summary>
    public static GlyphDefinition FromRows(IReadOnlyList<string> rows, int? advance = null)
    {
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
        if (width > 32)
            throw new InvalidDataException("A glyph is at most 32 pixels wide.");
        var bits = new uint[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            for (var c = 0; c < rows[i].Length; c++)
                if (rows[i][c] == '#')
                    bits[i] |= 1u << (width - 1 - c);
        return new GlyphDefinition(width, advance ?? width + 1, bits);
    }
}

/// <summary>
/// A bitmap font as data (T1.5 font seam): glyphs by Unicode code point with their own widths, so another game or
/// platform (IV's Japanese platforms) can bring its own glyph set. A graphics pack names its font by id; a font that is
/// not built into the Client is read from <c>GameData/&lt;game&gt;/graphics/fonts/&lt;id&gt;.json</c>
/// (<c>phantasie.font/v1</c>). Pure data — the Client turns it into drawing.
/// </summary>
public sealed class FontDefinition
{
    public required string Id { get; init; }
    /// <summary>Glyph cell height in pixels.</summary>
    public required int Height { get; init; }
    /// <summary>Distance between text lines in pixels.</summary>
    public required int LineHeight { get; init; }
    /// <summary>Pen advance of a space when the font defines no space glyph.</summary>
    public required int SpaceAdvance { get; init; }
    /// <summary>Code point drawn for one the font lacks (the glyph must exist).</summary>
    public required int Fallback { get; init; }
    /// <summary>Look a missing lower-case letter up as its upper-case form (the remaster 5×7 font has capitals only).</summary>
    public required bool FoldToUpper { get; init; }
    public required IReadOnlyDictionary<int, GlyphDefinition> Glyphs { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static FontDefinition Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Read a <c>phantasie.font/v1</c> font. Glyph keys are the character itself (<c>"A"</c>, <c>"あ"</c>) or
    /// <c>"U+3042"</c>; each glyph has <c>rows</c> (<c>#</c> = ink) and optional <c>advance</c> (default width + 1).
    /// </summary>
    public static FontDefinition Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<FontDto>(json, Options) ?? throw new InvalidDataException("empty font");
        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new InvalidDataException("font 'id' missing");
        if (dto.Height <= 0)
            throw new InvalidDataException("font 'height' must be positive");
        var glyphs = new Dictionary<int, GlyphDefinition>();
        foreach (var (key, g) in dto.Glyphs ?? [])
        {
            var rows = g.Rows ?? [];
            if (rows.Count != dto.Height)
                throw new InvalidDataException($"glyph '{key}' has {rows.Count} rows, the font is {dto.Height} high");
            glyphs[CodePoint(key)] = GlyphDefinition.FromRows(rows, g.Advance);
        }
        var fallback = CodePoint(dto.Fallback ?? "?");
        if (!glyphs.ContainsKey(fallback))
            throw new InvalidDataException($"fallback glyph '{dto.Fallback ?? "?"}' missing");
        return new FontDefinition
        {
            Id = dto.Id,
            Height = dto.Height,
            LineHeight = dto.LineHeight ?? dto.Height + 2,
            SpaceAdvance = dto.SpaceAdvance ?? (glyphs.TryGetValue(fallback, out var f) ? f.Advance : dto.Height),
            Fallback = fallback,
            FoldToUpper = dto.FoldToUpper ?? false,
            Glyphs = glyphs,
        };
    }

    /// <summary><c>"A"</c> / <c>"あ"</c> (one code point) or <c>"U+3042"</c> → the code point.</summary>
    public static int CodePoint(string key)
    {
        if (key.Length > 2 && key.StartsWith("U+", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp))
            return cp;
        var e = key.EnumerateRunes();
        if (e.MoveNext() && e.Current is var r && !e.MoveNext())
            return r.Value;
        throw new InvalidDataException($"glyph key '{key}' is not one character or U+XXXX");
    }

    private sealed class FontDto
    {
        public string? Id { get; set; }
        public int Height { get; set; }
        [JsonPropertyName("line_height")] public int? LineHeight { get; set; }
        [JsonPropertyName("space_advance")] public int? SpaceAdvance { get; set; }
        public string? Fallback { get; set; }
        [JsonPropertyName("fold_to_upper")] public bool? FoldToUpper { get; set; }
        public Dictionary<string, GlyphDto>? Glyphs { get; set; }
    }

    private sealed class GlyphDto
    {
        public List<string>? Rows { get; set; }
        public int? Advance { get; set; }
    }
}
