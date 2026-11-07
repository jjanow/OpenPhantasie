using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>P0.7 (s41): DOS map tile selection (OV2 <c>0x54fa</c> / <c>0x21df</c>) + terrain step costs by class.</summary>
public class MapTileTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();
    private static MapTileManifest Tiles => Data.MapTiles ?? throw new InvalidOperationException("map_tiles missing");

    [OriginalDataTheory]
    [InlineData(0, 1, 0)]       // class 0
    [InlineData(16, 1, 1)]      // class 1, tile % 10 ignored
    [InlineData(34, 5, 3)]
    [InlineData(40, 1, 4)]      // town
    [InlineData(41, 1, 13)]     // wilderness inn override
    [InlineData(63, 8, 6)]      // class 6 path
    [InlineData(74, 8, 7)]      // bridge tile outside OUT18/19
    [InlineData(75, 18, 14)]    // class 7 inside OUT18/19
    [InlineData(60, 19, 14)]    // class 6 inside OUT18/19
    [InlineData(101, 1, 10)]    // water
    [InlineData(113, 5, 11)]    // pool
    public void OverworldPattern_FollowsOv2CellDraw(int raw, int region, int pattern) =>
        Assert.Equal(pattern, Tiles.OverworldPattern(raw, region));

    [OriginalDataTheory]
    [InlineData(120)]
    [InlineData(161)]
    [InlineData(221)]
    public void OverworldPattern_FoggedRawIsNull(int raw) => Assert.Null(Tiles.OverworldPattern(raw, 1));

    [OriginalDataFact]
    public void PartyMarker_IsPattern12() => Assert.Equal(12, Tiles.PartyPattern);

    [OriginalDataFact]
    public void EveryShippedOverworldTile_HasAPatternImage()
    {
        var traversal = Data.CreateTraversal();
        foreach (var region in traversal.Regions)
            for (var y = 0; y < OverworldRegion.Height; y++)
            for (var x = 0; x < OverworldRegion.Width; x++)
            {
                var p = Tiles.OverworldPattern(region.GetNormalized(x, y), region.RegionId);
                Assert.NotNull(p);
                Assert.True(Tiles.PatternImages.ContainsKey(p!.Value), $"OUT{region.RegionId} ({x},{y}) pattern {p}");
            }
    }

    [OriginalDataTheory]
    [InlineData(0x00, 0xe6)]
    [InlineData(0x01, 0xe6)]    // walkable 0x01 draws like a wall [C quirk]
    [InlineData(0x02, 0xe7)]
    [InlineData(0x8a, 0xe7)]
    [InlineData(0xb8, 0xe7)]
    [InlineData(0xb9, 0xea)]
    [InlineData(0xc7, 0xea)]
    [InlineData(0xc8, 0xe9)]
    [InlineData(0xd6, 0xe9)]
    [InlineData(0xd7, 0xeb)]
    [InlineData(0xd9, 0xeb)]
    [InlineData(0xda, 0xec)]
    [InlineData(0xdc, 0xec)]
    [InlineData(0xdd, 0xe8)]
    [InlineData(0xff, 0xe8)]
    public void DungeonGlyph_FollowsOv2_21df(int cell, int glyph) => Assert.Equal(glyph, Tiles.DungeonGlyph(cell));

    [OriginalDataFact]
    public void EveryGlyphAndPattern_HasAnExtractedPng()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? root = null;
        while (dir is not null && root is null)
        {
            var candidate = Path.Combine(dir.FullName, Tiles.Root);
            if (Directory.Exists(candidate)) root = candidate;
            dir = dir.Parent;
        }

        Assert.NotNull(root);
        foreach (var rel in Tiles.PatternImages.Values.Concat(Tiles.GlyphImages.Values).Append(Tiles.FogImage))
            Assert.True(File.Exists(Path.Combine(root!, rel)), rel);
        Assert.Equal(15, Tiles.PatternImages.Count);
        Assert.Equal(8, Tiles.GlyphImages.Count);
    }

    /// <summary>
    /// Travel time by class [C] (OV2 <c>0x5f88</c>): path (class 6) costs 60 in DOS like desert-looking
    /// classes 8/9 and water; class 0 (dots) 75, class 1 (trees) 150, class 2 225, class 3 300. The shipped data charges the path 30 (D-055).
    /// </summary>
    [OriginalDataTheory]
    [InlineData(60, 30)] // D-055: path (class 6) is the cheapest terrain
    [InlineData(63, 30)]
    [InlineData(66, 30)]
    [InlineData(74, 60)]
    [InlineData(85, 60)]
    [InlineData(93, 60)]
    [InlineData(101, 60)]
    [InlineData(4, 75)]
    [InlineData(12, 150)]
    [InlineData(23, 225)]
    [InlineData(33, 300)]
    public void StepCost_ByTileClass(int tile, int cost) => Assert.Equal(cost, Data.TravelTime.StepCost(tile / 10));

    /// <summary>The class-6 road on OUT8 joins the inn (5,10) to the town (18,15) and crosses the river on tile 74.</summary>
    [OriginalDataFact]
    public void Out8Road_CrossesRiverOnTile74()
    {
        var r = Data.CreateTraversal().GetRegion(8);
        Assert.Equal(41, r.GetNormalized(5, 10));
        Assert.Equal(40, r.GetNormalized(18, 15));
        Assert.Equal(74, r.GetNormalized(14, 11));
        Assert.Equal(6, r.GetNormalized(13, 11) / 10);
        Assert.Equal(6, r.GetNormalized(15, 11) / 10);
        Assert.Equal(101, r.GetNormalized(14, 10));
        Assert.Equal(101, r.GetNormalized(14, 12));
    }
}
