using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Amiga map graphics (<c>maps.json</c> from <c>tools/scripts/extract_amiga_map_graphics.py</c>, under
/// <c>reverse-engineering/amiga/extracted/maps</c>). The Amiga overworld is one pre-rendered picture per region (the
/// <c>OUT*.DAT</c> polygon list filled with 16×16 patterns) on a 12×9 cell grid, with town / inn / dungeon icons and a
/// party sprite; fogged cells are black. The dungeon map uses 5×4 glyphs keyed by the DOS glyph ids <c>0xe6..0xed</c>.
/// Image paths are relative to the folder holding <c>maps.json</c>. Since T1.5 this is the Amiga graphics pack's map layout
/// (<c>GameData/phantasie1/graphics/amiga.json</c> → <c>map_layout</c>): <see cref="GraphicsPack"/> turns its images into named
/// pictures (<c>overworld.region.N</c>, <c>dungeon.glyph.N</c>…) and the Client reads only the geometry from here.
/// </summary>
public sealed class AmigaMapManifest
{
    public required int CellWidth { get; init; }
    public required int CellHeight { get; init; }
    public required int FogThreshold { get; init; }
    public required IReadOnlyDictionary<int, AmigaRegionGraphics> Regions { get; init; }
    public required IReadOnlyDictionary<string, string> IconImages { get; init; }
    public required string PartyImage { get; init; }

    /// <summary>
    /// The 16×16 fill pattern of each overworld pattern number (<c>overworld.patterns</c>, optional; empty when absent).
    /// Phantasie II's ST <c>maps.json</c> has them: pattern number = cell class (<c>terrain.md</c> §5), so lava (8), mist
    /// (11) and dark voids (7) are drawn with the ST's own patterns (D2-014). The Amiga I extract has none.
    /// </summary>
    public IReadOnlyDictionary<int, string> PatternImages { get; init; } = new Dictionary<int, string>();

    public required int GlyphWidth { get; init; }
    public required int GlyphHeight { get; init; }
    public required IReadOnlyDictionary<int, string> GlyphImages { get; init; }
    public required string CompassImage { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads <c>maps.json</c>; <c>null</c> when the file is absent.</summary>
    public static AmigaMapManifest? Load(string mapsJsonPath)
    {
        if (!File.Exists(mapsJsonPath))
            return null;
        var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(mapsJsonPath), Options)
            ?? throw new InvalidDataException($"{mapsJsonPath}: empty.");
        var ow = dto.Overworld ?? throw new InvalidDataException($"{mapsJsonPath}: overworld missing.");
        var dg = dto.Dungeon ?? throw new InvalidDataException($"{mapsJsonPath}: dungeon missing.");

        return new AmigaMapManifest
        {
            CellWidth = ow.CellWidth,
            CellHeight = ow.CellHeight,
            FogThreshold = ow.FogThreshold,
            Regions = ow.Regions.ToDictionary(
                r => int.Parse(r.Key),
                r => new AmigaRegionGraphics(
                    r.Value.Image,
                    r.Value.Icons.ToDictionary(i => i.Key, i => (i.Value[0], i.Value[1])))
                {
                    IconCells = r.Value.IconCells?.ToDictionary(
                        i => i.Key,
                        i => (IReadOnlyList<(int X, int Y)>)[.. i.Value.Select(c => (c[0], c[1]))]),
                }),
            IconImages = ow.Icons,
            PartyImage = ow.Party,
            PatternImages = (ow.Patterns ?? []).ToDictionary(p => int.Parse(p.Key), p => p.Value),
            GlyphWidth = dg.GlyphWidth,
            GlyphHeight = dg.GlyphHeight,
            GlyphImages = dg.Glyphs.ToDictionary(g => int.Parse(g.Key), g => g.Value),
            CompassImage = dg.Compass,
        };
    }

    private sealed class FileDto
    {
        public OverworldDto? Overworld { get; set; }
        public DungeonDto? Dungeon { get; set; }
    }

    private sealed class OverworldDto
    {
        [JsonPropertyName("cell_width")] public int CellWidth { get; set; }
        [JsonPropertyName("cell_height")] public int CellHeight { get; set; }
        [JsonPropertyName("fog_threshold")] public int FogThreshold { get; set; }
        public Dictionary<string, string> Icons { get; set; } = [];
        public string Party { get; set; } = "";
        public Dictionary<string, string>? Patterns { get; set; }
        public Dictionary<string, RegionDto> Regions { get; set; } = [];
    }

    private sealed class RegionDto
    {
        public string Image { get; set; } = "";
        public Dictionary<string, int[]> Icons { get; set; } = [];
        [JsonPropertyName("icon_cells")] public Dictionary<string, int[][]>? IconCells { get; set; }
    }

    private sealed class DungeonDto
    {
        [JsonPropertyName("glyph_width")] public int GlyphWidth { get; set; }
        [JsonPropertyName("glyph_height")] public int GlyphHeight { get; set; }
        public Dictionary<string, string> Glyphs { get; set; } = [];
        public string Compass { get; set; } = "";
    }
}

/// <summary>One overworld region: its pre-rendered picture and the icon cells (kind → x, y; one per kind, h56 <c>0x5a4</c>).</summary>
public sealed record AmigaRegionGraphics(string Image, IReadOnlyDictionary<string, (int X, int Y)> Icons)
{
    /// <summary>
    /// Every icon cell per kind when the platform draws several (the ST draws each dungeon entrance and castle of a
    /// region, scan <c>0x4cd0</c>: up to 8 dungeons and 5 castles); null when the layout lists only <see cref="Icons"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<(int X, int Y)>>? IconCells { get; init; }

    /// <summary>The cells to draw icons on: <see cref="IconCells"/> when present, else one per kind.</summary>
    public IEnumerable<(string Kind, int X, int Y)> AllIcons() =>
        IconCells is { } cells
            ? cells.SelectMany(k => k.Value.Select(c => (k.Key, c.X, c.Y)))
            : Icons.Select(k => (k.Key, k.Value.X, k.Value.Y));
}
