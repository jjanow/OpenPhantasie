using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// DOS map tile selection rules (<c>asset-manifest.json</c> → <c>map_tiles</c>, P0.7 s41). Overworld: OV2 <c>0x54fa</c>
/// picks a <c>PAT</c> pattern from the raw region byte; dungeon: OV2 <c>0x21df</c> picks a 5×4 glyph from the cell byte
/// [C] (<c>logic-specs/map-tile-graphics.md</c>). Image paths are relative to <see cref="Root"/> (repo-relative). The selection
/// rule is game logic shared by every pack; the CGA images themselves are drawn only through the DOS CGA graphics pack
/// (<c>GameData/phantasie1/graphics/dos-cga.json</c>, T1.5), which names the same files.
/// </summary>
public sealed class MapTileManifest
{
    public required string Root { get; init; }
    public required int OverworldTileWidth { get; init; }
    public required int OverworldTileHeight { get; init; }
    public required int FogThreshold { get; init; }
    public required string FogImage { get; init; }
    public required int PartyPattern { get; init; }
    public required IReadOnlyDictionary<int, int> TileOverrides { get; init; }
    public required IReadOnlyList<(IReadOnlySet<int> Regions, IReadOnlySet<int> Classes, int Pattern)> RegionOverrides { get; init; }
    public required IReadOnlyDictionary<int, string> PatternImages { get; init; }

    public required int GlyphWidth { get; init; }
    public required int GlyphHeight { get; init; }
    public required int PartyGlyph { get; init; }
    public required IReadOnlyList<(int Min, int Max, int Glyph)> CellRanges { get; init; }
    public required IReadOnlyDictionary<int, string> GlyphImages { get; init; }

    /// <summary>Pattern index for a raw overworld byte, or <c>null</c> when the cell is fogged (DOS draws black).</summary>
    public int? OverworldPattern(int raw, int regionId)
    {
        if (raw >= FogThreshold) return null;
        if (TileOverrides.TryGetValue(raw, out var p)) return p;
        var cls = raw / 10;
        foreach (var (regions, classes, pattern) in RegionOverrides)
            if (regions.Contains(regionId) && classes.Contains(cls))
                return pattern;
        return cls;
    }

    /// <summary>Glyph id (<c>0xe6..0xec</c>) for a dungeon cell byte.</summary>
    public int DungeonGlyph(int cell)
    {
        foreach (var (min, max, glyph) in CellRanges)
            if (cell >= min && cell <= max)
                return glyph;
        throw new InvalidDataException($"No dungeon glyph range covers cell 0x{cell:x2}.");
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads <c>map_tiles</c> from an asset manifest; <c>null</c> when the section is absent.</summary>
    public static MapTileManifest? Load(string assetManifestPath)
    {
        var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(assetManifestPath), Options)?.MapTiles;
        if (dto?.Overworld is null || dto.Dungeon is null || dto.Root is null)
            return null;

        var ow = dto.Overworld;
        var dg = dto.Dungeon;
        var ranges = (dg.CellRanges ?? []).Select(r => (r.Min, r.Max, r.Glyph)).ToList();
        for (var c = 0; c <= 255; c++)
            if (!ranges.Any(r => c >= r.Min && c <= r.Max))
                throw new InvalidDataException($"{assetManifestPath}: map_tiles.dungeon.cell_ranges misses 0x{c:x2}.");

        return new MapTileManifest
        {
            Root = dto.Root,
            OverworldTileWidth = ow.TileWidth,
            OverworldTileHeight = ow.TileHeight,
            FogThreshold = ow.FogThreshold,
            FogImage = ow.FogImage ?? "",
            PartyPattern = ow.PartyPattern,
            TileOverrides = (ow.TileOverrides ?? []).ToDictionary(t => t.Tile, t => t.Pattern),
            RegionOverrides = (ow.RegionOverrides ?? [])
                .Select(r => ((IReadOnlySet<int>)r.Regions.ToHashSet(), (IReadOnlySet<int>)r.Classes.ToHashSet(), r.Pattern))
                .ToList(),
            PatternImages = (ow.Patterns ?? []).ToDictionary(p => p.Index, p => p.Image),
            GlyphWidth = dg.GlyphWidth,
            GlyphHeight = dg.GlyphHeight,
            PartyGlyph = dg.PartyGlyph,
            CellRanges = ranges,
            GlyphImages = (dg.Glyphs ?? []).ToDictionary(g => g.Glyph, g => g.Image),
        };
    }

    private sealed class FileDto
    {
        [JsonPropertyName("map_tiles")] public SectionDto? MapTiles { get; set; }
    }

    private sealed class SectionDto
    {
        public string? Root { get; set; }
        public OverworldDto? Overworld { get; set; }
        public DungeonDto? Dungeon { get; set; }
    }

    private sealed class OverworldDto
    {
        [JsonPropertyName("tile_width")] public int TileWidth { get; set; }
        [JsonPropertyName("tile_height")] public int TileHeight { get; set; }
        [JsonPropertyName("fog_threshold")] public int FogThreshold { get; set; }
        [JsonPropertyName("fog_image")] public string? FogImage { get; set; }
        [JsonPropertyName("party_pattern")] public int PartyPattern { get; set; }
        [JsonPropertyName("tile_overrides")] public List<TileOverrideDto>? TileOverrides { get; set; }
        [JsonPropertyName("region_overrides")] public List<RegionOverrideDto>? RegionOverrides { get; set; }
        public List<PatternDto>? Patterns { get; set; }
    }

    private sealed class TileOverrideDto
    {
        public int Tile { get; set; }
        public int Pattern { get; set; }
    }

    private sealed class RegionOverrideDto
    {
        public List<int> Regions { get; set; } = [];
        public List<int> Classes { get; set; } = [];
        public int Pattern { get; set; }
    }

    private sealed class PatternDto
    {
        public int Index { get; set; }
        public string Image { get; set; } = "";
    }

    private sealed class DungeonDto
    {
        [JsonPropertyName("glyph_width")] public int GlyphWidth { get; set; }
        [JsonPropertyName("glyph_height")] public int GlyphHeight { get; set; }
        [JsonPropertyName("party_glyph")] public int PartyGlyph { get; set; }
        [JsonPropertyName("cell_ranges")] public List<RangeDto>? CellRanges { get; set; }
        public List<GlyphDto>? Glyphs { get; set; }
    }

    private sealed class RangeDto
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public int Glyph { get; set; }
    }

    private sealed class GlyphDto
    {
        public int Glyph { get; set; }
        public string Image { get; set; } = "";
    }
}
