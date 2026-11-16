using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s218: Phantasie I's Commodore 64 graphics pack (<c>GameData/phantasie1/graphics/c64.json</c>, written by the
/// <c>p1-c64</c> import, <c>P1C64Art</c>; notes <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>): sprite monsters,
/// hero sprites by race, the title picture, the town's buildings, character-tile maps with the C64's column pairs and its
/// map sprites drawn at their own size. The Amiga stays the default; with no Amiga copy the C64's.
/// </summary>
public class Phantasie1C64PackTests
{
    private static readonly string GameData = GameDataRepository.DefaultPhantasie1Path;

    private static GraphicsPack C64 =>
        GraphicsPackCatalog.Load(GameData).Find("c64") ?? throw new InvalidOperationException("C64 extracts missing");

    [OriginalDataFact]
    public void C64Pack_LoadsAsAC64Pack_DrawnInTheRemasterFont()
    {
        var pack = C64;
        Assert.Equal(("phantasie1", "c64", "Commodore 64"), (pack.Game, pack.Platform, pack.Name));
        Assert.Equal(PixelFont.Id, pack.Font);
        Assert.True(FontSources.CanDraw(pack));
    }

    [OriginalDataFact]
    public void C64Pack_ResolvesEveryMonster_EveryRaceButUndead_TitleAndTown()
    {
        var pack = C64;
        for (var id = 0; id < 80; id++)
            Assert.NotNull(pack.Resolve(PictureNames.MonsterIdle(id)));
        Assert.NotNull(pack.Resolve(PictureNames.MonsterAttacking(0)));   // SKELETON: one sprite, a second block
        Assert.NotNull(pack.Resolve(PictureNames.MonsterAttacking(76)));  // BLACK KNIGHT: the extra block in sprite 0 (s219) [~]
        for (var race = 0; race < 15; race++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.PortraitIdle(race)));
            Assert.NotNull(pack.Resolve(PictureNames.PortraitAttacking(race)));
        }
        Assert.Null(pack.Resolve(PictureNames.PortraitIdle(15))); // no UNDEAD sprite on the C64
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TownBackdrop, PictureNames.OverworldParty })
            Assert.NotNull(pack.Resolve(name));
        // What the C64 has no picture for falls back to the Client's own drawing (s221: the ending and the party wipe
        // are drawn, C64EndScreenTests; the Amiga's decor screen, its sinking door, sorry sign and grave are not).
        foreach (var name in new[] { PictureNames.EndingFinal, PictureNames.DefeatTemple })
            Assert.NotNull(pack.Resolve(name));
        foreach (var name in new[] { PictureNames.Decor, PictureNames.DecorDoor, PictureNames.DecorSorrySign, "combat.grave" })
            Assert.Null(pack.Resolve(name));
    }

    [OriginalDataFact]
    public void C64Pack_DrawsMapsFromCharacters_AsUtil3Does()
    {
        var pack = C64;
        var tiles = pack.TileMaps!;
        Assert.Equal((8, 8, 8, 8), (tiles.TileWidth, tiles.TileHeight, tiles.GlyphWidth, tiles.GlyphHeight));
        Assert.Equal((19, 18), (tiles.ViewColumns, tiles.ViewRows)); // 152 x 144: beside the sidebar, inside the content box
        for (var b = 0; b < 256; b++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(tiles.ColumnTile(b, 0))));
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(tiles.ColumnTile(b, 1))));
        }
        Assert.Equal(7, tiles.ColumnTile(7, 4));
        Assert.Equal(263, tiles.ColumnTile(7, 5));
        Assert.Equal([new TileMapLayout.Overlay(40, 40, 1000, -4, 0), new(41, 41, 1001, -4, 0), new(50, 59, 1002, -4, 0)],
            tiles.Overlays);
        foreach (var o in tiles.Overlays)
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(o.Tile)));
        Assert.Equal((-4, 0), tiles.PartyOffset);
        Assert.True(tiles.DungeonPartyOver);
        Assert.Equal(0xE6, tiles.DungeonGlyphFor(0));
        Assert.Equal(0xE7, tiles.DungeonGlyphFor(15));
        Assert.Equal(0xEA, tiles.DungeonGlyphFor(185));
        Assert.Equal(0xE9, tiles.DungeonGlyphFor(213));
        Assert.Equal(0xEB, tiles.DungeonGlyphFor(215));
        Assert.Equal(0xE8, tiles.DungeonGlyphFor(235));
        foreach (var glyph in new[] { 0x20, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED })
            Assert.NotNull(pack.Resolve(PictureNames.DungeonCell(glyph)));
    }

    [Fact]
    public void PacksWithoutTheC64Fields_KeepTheirOldDrawing()
    {
        var apple = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("apple2");
        if (apple?.TileMaps is not { } tiles)
            return;
        Assert.Equal(0, tiles.OddColumnTile);
        Assert.Equal(201, tiles.ColumnTile(201, 3));
        Assert.Empty(tiles.Overlays);
        Assert.Null(tiles.PartyOffset);
        Assert.False(tiles.DungeonPartyOver);
    }

    [OriginalDataFact]
    public void WithoutTheAmigaExtracts_TheC64PackIsTheDefault()
    {
        var temp = Directory.CreateTempSubdirectory("p1-c64-only-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(temp.FullName, "phantasie1", "graphics"));
            foreach (var f in new[] { "index.json", "c64.json" })
                File.Copy(Path.Combine(GameData, "graphics", f), Path.Combine(graphics.FullName, f));
            foreach (var (f, root) in new[] { ("amiga.json", "reverse-engineering/amiga/extracted"),
                         ("dos-cga.json", "reverse-engineering/dos/extracted") })
                File.WriteAllText(Path.Combine(graphics.FullName, f),
                    File.ReadAllText(Path.Combine(GameData, "graphics", f)).Replace(root, "no-such-folder/" + f));

            var catalog = GraphicsPackCatalog.Load(Path.Combine(temp.FullName, "phantasie1"));
            Assert.Equal(["c64"], catalog.Available.Select(p => p.Id));
            Assert.Equal("c64", catalog.Default?.Id);
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
            return;
        Assert.Equal("amiga", catalog.Default?.Id);
        Assert.NotNull(catalog.Find("c64"));
    }
}
