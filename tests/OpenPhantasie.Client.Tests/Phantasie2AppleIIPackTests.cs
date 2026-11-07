using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s158: Phantasie II's Apple II graphics pack (<c>GameData/phantasie2/graphics/apple2.json</c>, built by
/// <c>tools/scripts/extract_a2_p2_graphics.py</c> + <c>build_a2_p2_asset_manifest.py</c>; notes
/// <c>reverse-engineering/apple2/phantasie2/GRAPHICS.md</c>): shape-table monsters, party figures, map tiles and dungeon
/// marks, and the title picture. It is the default only when the ST pack is missing (the Memorial Set's II).
/// </summary>
public class Phantasie2AppleIIPackTests
{
    private static readonly string GameData = GameDataRepository.DefaultPathFor(GameId.P2);

    private static GraphicsPack Apple =>
        GraphicsPackCatalog.Load(GameData).Find("apple2") ?? throw new InvalidOperationException("Apple II extracts missing");

    [OriginalDataFact]
    public void ApplePack_LoadsAsAnApple2Pack_DrawnInTheRemasterFont()
    {
        var pack = Apple;
        Assert.Equal(("phantasie2", "apple2", "Apple II"), (pack.Game, pack.Platform, pack.Name));
        Assert.Equal(PixelFont.Id, pack.Font);
        Assert.True(FontSources.CanDraw(pack));
        Assert.Null(pack.MapLayout);
        Assert.NotNull(pack.TitleSequence);
        Assert.Equal((280, 192), (pack.TitleSequence!.Width, pack.TitleSequence.Height));
    }

    [OriginalDataFact]
    public void ApplePack_ResolvesEveryMonsterPoseAndRacePortrait()
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
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TitleCredits, PictureNames.OverworldParty })
            Assert.NotNull(pack.Resolve(name));
    }

    [OriginalDataFact]
    public void ApplePack_DrawsMapsFromTiles_AsOUTDOORS2AndDUNGON2Do()
    {
        var pack = Apple;
        var tiles = pack.TileMaps!;
        Assert.Equal((7, 6, 4, 4), (tiles.TileWidth, tiles.TileHeight, tiles.GlyphWidth, tiles.GlyphHeight));
        for (var b = 0; b < 256; b++) // every map byte, fog (120+) included, has a tile
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(b)));
        Assert.Equal(31, tiles.DungeonGlyphFor(0));   // wall
        Assert.Equal(32, tiles.DungeonGlyphFor(185)); // door class
        Assert.Equal(0, tiles.DungeonGlyphFor(216));  // 215-217 are drawn in HCOLOR 0: nothing shows
        Assert.Equal(36, tiles.DungeonGlyphFor(230));
        Assert.Equal(0, tiles.DungeonGlyphFor(50));   // an open cell is erased
        foreach (var glyph in new[] { 0, 30, 31, 32, 33, 35, 36 })
            Assert.NotNull(pack.Resolve(PictureNames.DungeonCell(glyph)));
    }

    [OriginalDataFact]
    public void WithoutTheStExtracts_TheApplePackIsTheDefault()
    {
        // An Apple II-only player (the Memorial Set): the ST root is not found, so the Apple pack becomes the default.
        var temp = Directory.CreateTempSubdirectory("p2-apple-only-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(temp.FullName, "phantasie2", "graphics"));
            File.Copy(Path.Combine(GameData, "graphics", "index.json"), Path.Combine(graphics.FullName, "index.json"));
            File.Copy(Path.Combine(GameData, "graphics", "apple2.json"), Path.Combine(graphics.FullName, "apple2.json"));
            var st = File.ReadAllText(Path.Combine(GameData, "graphics", "atari-st.json"))
                .Replace("reverse-engineering/atari-st/phantasie2/extracted/graphics", "no-such-folder/st-graphics");
            File.WriteAllText(Path.Combine(graphics.FullName, "atari-st.json"), st);

            var catalog = GraphicsPackCatalog.Load(Path.Combine(temp.FullName, "phantasie2"));
            Assert.Equal(["apple2"], catalog.Available.Select(p => p.Id));
            Assert.Equal(["atari-st"], catalog.Missing);
            Assert.Equal("apple2", catalog.Default?.Id);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
