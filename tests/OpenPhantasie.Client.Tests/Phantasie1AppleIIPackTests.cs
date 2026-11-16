using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s223: Phantasie I's Apple II graphics pack (<c>GameData/phantasie1/graphics/apple2.json</c>, written by the
/// <c>p1-apple2</c> import, <c>P1Apple2Art</c>; notes <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c>): II's
/// Apple engine with I's shapes and title. The Amiga stays the default, then the C64; with neither the Apple's.
/// </summary>
public class Phantasie1AppleIIPackTests
{
    private static readonly string GameData = GameDataRepository.DefaultPhantasie1Path;

    private static GraphicsPack Apple =>
        GraphicsPackCatalog.Load(GameData).Find("apple2") ?? throw new InvalidOperationException("Apple II extracts missing");

    [OriginalDataFact]
    public void ApplePack_LoadsAsAnApple2Pack_DrawnInTheRemasterFont()
    {
        var pack = Apple;
        Assert.Equal(("phantasie1", "apple2", "Apple II"), (pack.Game, pack.Platform, pack.Name));
        Assert.Equal(PixelFont.Id, pack.Font);
        Assert.True(FontSources.CanDraw(pack));
        Assert.NotNull(pack.TitleSequence);
        Assert.Equal((280, 192), (pack.TitleSequence!.Width, pack.TitleSequence.Height));
    }

    [OriginalDataFact]
    public void ApplePack_ResolvesEveryMonsterPoseAndRacePortrait_AndNoEndScreens()
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
        // The Apple's town is a text page and its ending / party wipe are drawn in BASIC: no pictures (s224: the scenes are
        // replayed from apple2_scenes instead, Apple2EndSceneTests).
        foreach (var name in new[] { PictureNames.TownBackdrop, PictureNames.EndingFinal, PictureNames.DefeatTemple, PictureNames.EventTrap })
            Assert.Null(pack.Resolve(name));
    }

    [OriginalDataFact]
    public void ApplePack_DrawsMapsFromTiles_AsOUTDOORSAndDUNGONDo()
    {
        var pack = Apple;
        var tiles = pack.TileMaps!;
        Assert.Equal((7, 6, 4, 4), (tiles.TileWidth, tiles.TileHeight, tiles.GlyphWidth, tiles.GlyphHeight));
        for (var b = 0; b < 256; b++)
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(b)));
        Assert.Equal(31, tiles.DungeonGlyphFor(1));   // wall
        Assert.Equal(33, tiles.DungeonGlyphFor(205));
        Assert.Equal(0, tiles.DungeonGlyphFor(216));  // HCOLOR 0: nothing shows
        Assert.Equal(35, tiles.DungeonGlyphFor(219)); // stairs
        Assert.Equal(36, tiles.DungeonGlyphFor(230)); // events
        Assert.Equal(0, tiles.DungeonGlyphFor(50));   // an open cell is erased
        foreach (var glyph in new[] { 0, 30, 31, 32, 33, 35, 36 })
            Assert.NotNull(pack.Resolve(PictureNames.DungeonCell(glyph)));
    }

    [OriginalDataFact]
    public void WithoutTheAmigaAndC64Extracts_TheApplePackIsTheDefault()
    {
        var temp = Directory.CreateTempSubdirectory("p1-apple-only-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(temp.FullName, "phantasie1", "graphics"));
            foreach (var f in new[] { "index.json", "apple2.json" })
                File.Copy(Path.Combine(GameData, "graphics", f), Path.Combine(graphics.FullName, f));
            foreach (var (f, root) in new[] { ("amiga.json", "reverse-engineering/amiga/extracted"),
                         ("dos-cga.json", "reverse-engineering/dos/extracted"),
                         ("c64.json", "reverse-engineering/c64/phantasie1/extracted") })
                File.WriteAllText(Path.Combine(graphics.FullName, f),
                    File.ReadAllText(Path.Combine(GameData, "graphics", f)).Replace(root, "no-such-folder/" + f));

            var catalog = GraphicsPackCatalog.Load(Path.Combine(temp.FullName, "phantasie1"));
            Assert.Equal(["apple2"], catalog.Available.Select(p => p.Id));
            Assert.Equal("apple2", catalog.Default?.Id);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [OriginalDataFact]
    public void WithTheAmigaOrC64Extracts_TheyStayBeforeTheApple()
    {
        var catalog = GraphicsPackCatalog.Load(GameData);
        Assert.NotNull(catalog.Find("apple2"));
        var first = catalog.Find("amiga") is not null ? "amiga" : catalog.Find("c64") is not null ? "c64" : "apple2";
        Assert.Equal(first, catalog.Default?.Id);
    }
}
