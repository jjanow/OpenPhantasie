using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s212: Phantasie III's Apple II graphics pack (<c>GameData/phantasie3/graphics/apple2.json</c>, built by
/// <c>tools/scripts/extract_a2_p3_graphics.py</c> + <c>build_a2_p3_asset_manifest.py</c>; notes
/// <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c>): sprite monsters, shape party figures, map tiles and 3 × 3
/// dungeon marks, the title and the ending's gods' room. The Amiga stays the default; with no Amiga copy the Apple's.
/// </summary>
public class Phantasie3AppleIIPackTests
{
    private static readonly string GameData = GameDataRepository.DefaultPathFor(GameId.P3);

    private static GraphicsPack Apple =>
        GraphicsPackCatalog.Load(GameData).Find("apple2") ?? throw new InvalidOperationException("Apple II extracts missing");

    [OriginalDataFact]
    public void ApplePack_LoadsAsAnApple2Pack_DrawnInTheRemasterFont()
    {
        var pack = Apple;
        Assert.Equal(("phantasie3", "apple2", "Apple II"), (pack.Game, pack.Platform, pack.Name));
        Assert.Equal(PixelFont.Id, pack.Font);
        Assert.True(FontSources.CanDraw(pack));
        Assert.NotNull(pack.TitleSequence);
        Assert.Equal((280, 192), (pack.TitleSequence!.Width, pack.TitleSequence.Height));
    }

    [OriginalDataFact]
    public void ApplePack_ResolvesEveryMonsterPose_RacePortrait_AndScene()
    {
        var pack = Apple;
        for (var id = 0; id < 80; id++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.MonsterIdle(id)));
            Assert.NotNull(pack.Resolve(PictureNames.MonsterAttacking(id)));
        }
        for (var race = 0; race < 16; race++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.PortraitIdle(race)));
            Assert.NotNull(pack.Resolve(PictureNames.PortraitAttacking(race)));
        }
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TitleCredits, PictureNames.OverworldParty,
                     PictureNames.SceneRewardGood, PictureNames.SceneRewardEvil })
            Assert.NotNull(pack.Resolve(name));
        // What the Apple has no picture for falls back to the Client's own drawing.
        foreach (var name in new[] { PictureNames.TownBackdrop, PictureNames.SceneAstralPlane, PictureNames.PortraitElemental })
            Assert.Null(pack.Resolve(name));
    }

    [OriginalDataFact]
    public void ApplePack_DrawsMapsFromTiles_AsOUTDOORS3AndDUNGPLOTDo()
    {
        var pack = Apple;
        var tiles = pack.TileMaps!;
        Assert.Equal((7, 6, 3, 3), (tiles.TileWidth, tiles.TileHeight, tiles.GlyphWidth, tiles.GlyphHeight));
        for (var b = 0; b < 256; b++)
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(b)));
        // The Netherworld (region 4) draws its own terrain: DOS byte 197 is Meadows on Scandor, Lava there.
        Assert.Equal(197, tiles.DrawnTile(0, 197));
        Assert.Equal(308, tiles.DrawnTile(4, 197));
        Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(308)));
        Assert.Equal(31, tiles.DungeonGlyphFor(0));   // wall
        Assert.Equal(33, tiles.DungeonGlyphFor(205)); // door class
        Assert.Equal(0, tiles.DungeonGlyphFor(219));  // shape 35 is empty in III
        Assert.Equal(36, tiles.DungeonGlyphFor(230));
        Assert.Equal(0, tiles.DungeonGlyphFor(50));   // an open cell is erased
        Assert.Equal(99, tiles.UnseenGlyph);          // the box starts white
        foreach (var glyph in new[] { 0, 30, 31, 32, 33, 36, 99 })
            Assert.NotNull(pack.Resolve(PictureNames.DungeonCell(glyph)));
    }

    [OriginalDataFact]
    public void WithoutTheAmigaExtracts_TheApplePackIsTheDefault()
    {
        var temp = Directory.CreateTempSubdirectory("p3-apple-only-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(temp.FullName, "phantasie3", "graphics"));
            foreach (var f in new[] { "index.json", "apple2.json" })
                File.Copy(Path.Combine(GameData, "graphics", f), Path.Combine(graphics.FullName, f));
            foreach (var (f, root) in new[] { ("amiga.json", "reverse-engineering/amiga/phantasie3/extracted/graphics"),
                         ("dos-cga.json", "reverse-engineering/dos/phantasie3/extracted/graphics") })
                File.WriteAllText(Path.Combine(graphics.FullName, f),
                    File.ReadAllText(Path.Combine(GameData, "graphics", f)).Replace(root, "no-such-folder/" + f));

            var catalog = GraphicsPackCatalog.Load(Path.Combine(temp.FullName, "phantasie3"));
            Assert.Equal(["apple2"], catalog.Available.Select(p => p.Id));
            Assert.Equal("apple2", catalog.Default?.Id);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [OriginalDataFact]
    public void WithTheAmigaExtracts_TheAmigaStaysTheDefault()
    {
        var catalog = GraphicsPackCatalog.Load(GameData);
        if (catalog.Find("amiga") is null)
            return; // no Amiga extract in this checkout
        Assert.Equal("amiga", catalog.Default?.Id);
        Assert.NotNull(catalog.Find("apple2"));
    }
}
