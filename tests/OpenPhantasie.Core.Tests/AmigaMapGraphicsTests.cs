using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Amiga map graphics (<c>maps.json</c>, extract_amiga_map_graphics.py) line up with the rules data Core runs.</summary>
public class AmigaMapGraphicsTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static (AmigaMapManifest Maps, string Root) Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var root = Path.Combine(dir.FullName, "reverse-engineering", "amiga", "extracted", "maps");
            if (AmigaMapManifest.Load(Path.Combine(root, "maps.json")) is { } maps)
                return (maps, root);
            dir = dir.Parent;
        }

        throw new InvalidOperationException("maps.json not found — run tools/scripts/extract_amiga_map_graphics.py");
    }

    [OriginalDataFact]
    public void Geometry_MatchesTheAmigaMapModule()
    {
        var (maps, _) = Load();
        Assert.Equal((12, 9), (maps.CellWidth, maps.CellHeight));
        Assert.Equal((5, 4), (maps.GlyphWidth, maps.GlyphHeight));
        Assert.Equal(OverworldRegion.FogOffset, maps.FogThreshold);
    }

    [OriginalDataFact]
    public void EveryRegion_HasAPictureAndItsIconsSitOnTheMatchingCells()
    {
        var (maps, root) = Load();
        foreach (var region in Data.CreateTraversal().Regions)
        {
            Assert.True(maps.Regions.TryGetValue(region.RegionId, out var gfx), $"OUT{region.RegionId}");
            Assert.True(File.Exists(Path.Combine(root, gfx!.Image)), gfx.Image);
            var expected = new Dictionary<string, (int, int)>();
            for (var x = 0; x < OverworldRegion.Width; x++)
            for (var y = 0; y < OverworldRegion.Height; y++)
            {
                var tile = region.GetNormalized(x, y);
                if (tile / 10 == 4) expected[tile % 10 == 1 ? "inn" : "town"] = (x, y);
                else if (tile / 10 == 5) expected["dungeon"] = (x, y);
            }

            Assert.Equal(expected.OrderBy(e => e.Key), gfx.Icons.OrderBy(e => e.Key));
        }
    }

    [OriginalDataFact]
    public void EveryGlyphIconAndPicture_HasAPng()
    {
        var (maps, root) = Load();
        for (var glyph = 0xe6; glyph <= 0xed; glyph++)
            Assert.True(File.Exists(Path.Combine(root, maps.GlyphImages[glyph])), $"glyph 0x{glyph:x2}");
        foreach (var image in maps.IconImages.Values.Append(maps.PartyImage).Append(maps.CompassImage))
            Assert.True(File.Exists(Path.Combine(root, image)), image);
    }
}
