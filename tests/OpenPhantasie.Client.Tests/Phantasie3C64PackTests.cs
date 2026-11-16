using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s220: Phantasie III's Commodore 64 graphics pack (<c>GameData/phantasie3/graphics/c64.json</c>, written by the
/// <c>p3-c64</c> import, <c>P3C64Art</c>; notes <c>reverse-engineering/c64/phantasie3/GRAPHICS.md</c>). The Amiga stays the
/// default, then the Apple; with neither copy the C64's.
/// </summary>
public class Phantasie3C64PackTests
{
    private static readonly string GameData = GameDataRepository.DefaultPathFor(GameId.P3);

    private static GraphicsPack C64 =>
        GraphicsPackCatalog.Load(GameData).Find("c64") ?? throw new InvalidOperationException("C64 extracts missing");

    [OriginalDataFact]
    public void C64Pack_ResolvesMonstersPortraitsClassFiguresAndScenes()
    {
        var pack = C64;
        Assert.Equal(("phantasie3", "c64", "Commodore 64"), (pack.Game, pack.Platform, pack.Name));
        for (var id = 0; id < 80; id++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.MonsterIdle(id)));
            Assert.NotNull(pack.Resolve(PictureNames.MonsterAttacking(id)));
        }
        for (var race = 0; race < 16; race++)
            Assert.NotNull(pack.Resolve(PictureNames.PortraitIdle(race)));
        // A Monk, Ranger, Priest or Wizard has its class's figure; a Thief or Fighter its race's (PHANT.OBJ $1D94).
        foreach (var c in new[] { CharacterClass.Monk, CharacterClass.Ranger, CharacterClass.Priest, CharacterClass.Wizard })
        {
            Assert.NotNull(pack.Resolve(PictureNames.PortraitClassIdle((int)c)));
            Assert.NotNull(pack.Resolve(PictureNames.PortraitClassAttacking((int)c)));
        }
        foreach (var c in new[] { CharacterClass.Thief, CharacterClass.Fighter })
            Assert.Null(pack.Resolve(PictureNames.PortraitClassIdle((int)c)));
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TownBackdrop, PictureNames.OverworldParty,
                     PictureNames.SceneAstralPlane, PictureNames.SceneRewardGood, PictureNames.SceneRewardEvil,
                     PictureNames.PortraitElemental, PictureNames.PortraitElementalAttacking })
            Assert.NotNull(pack.Resolve(name));
    }

    [OriginalDataFact]
    public void C64Pack_DrawsTheMapInTwoByTwoCharacterCells_PartyOnColumnFour()
    {
        var tiles = C64.TileMaps!;
        Assert.Equal((16, 16, 6, 3), (tiles.TileWidth, tiles.TileHeight, tiles.GlyphWidth, tiles.GlyphHeight));
        Assert.Equal((10, 9), (tiles.ViewColumns, tiles.ViewRows));
        Assert.Equal((4, 4), (tiles.PartyColumn, tiles.PartyRow));
        for (var b = 0; b < 256; b++)
            Assert.NotNull(C64.Resolve(PictureNames.OverworldTile(b)));
        // Scandor's water bytes swap (DOS 0 is the C64's 1); a plane keeps the byte's own tile.
        Assert.Equal(1, tiles.DrawnTile(0, 0));
        Assert.Equal(0, tiles.DrawnTile(4, 0));
        Assert.Equal(3, tiles.DungeonGlyphFor(205));
        Assert.Equal(5, tiles.DungeonGlyphFor(215));
        Assert.Equal(0, tiles.DungeonGlyphFor(17));
        for (var g = 0; g < 7; g++)
            Assert.NotNull(C64.Resolve(PictureNames.DungeonCell(g)));
    }

    [Fact]
    public void WithoutAPartyCell_TheViewCentresAsBefore()
    {
        var layout = new TileMapLayout(16, 16, 9, 9, 8, 5, 46, 51, 50, []);
        Assert.Equal((4, 4), (layout.PartyColumn, layout.PartyRow));
        Assert.Equal((5, 4), (new TileMapLayout(16, 16, 10, 9, 8, 5, 46, 51, 50, []).PartyColumn, 4));
    }

    [OriginalDataFact]
    public void WithoutTheAmigaAndAppleExtracts_TheC64PackIsTheDefault()
    {
        var temp = Directory.CreateTempSubdirectory("p3-c64-only-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(temp.FullName, "phantasie3", "graphics"));
            foreach (var f in new[] { "index.json", "c64.json", "dos-cga.json" })
                File.Copy(Path.Combine(GameData, "graphics", f), Path.Combine(graphics.FullName, f));
            foreach (var (f, root) in new[] { ("amiga.json", "reverse-engineering/amiga/phantasie3/extracted/graphics"),
                         ("apple2.json", "reverse-engineering/apple2/phantasie3/extracted/graphics") })
            {
                var text = File.ReadAllText(Path.Combine(GameData, "graphics", f));
                Assert.Contains(root, text);
                File.WriteAllText(Path.Combine(graphics.FullName, f), text.Replace(root, "no-such-folder/" + f));
            }

            var catalog = GraphicsPackCatalog.Load(Path.Combine(temp.FullName, "phantasie3"));
            Assert.Equal("c64", catalog.Default?.Id);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
