using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s219: Phantasie II's Commodore 64 graphics pack (<c>GameData/phantasie2/graphics/c64.json</c>, written by the
/// <c>p2-c64</c> import, <c>P2C64Art</c>; notes <c>reverse-engineering/c64/phantasie2/GRAPHICS.md</c>): C64 I's engine with
/// II's monsters, title and map routine. The ST stays the default, then the Apple; with neither copy the C64's.
/// </summary>
public class Phantasie2C64PackTests
{
    private static readonly string GameData = GameDataRepository.DefaultPathFor(GameId.P2);

    private static GraphicsPack C64 =>
        GraphicsPackCatalog.Load(GameData).Find("c64") ?? throw new InvalidOperationException("C64 extracts missing");

    [OriginalDataFact]
    public void C64Pack_LoadsAsAC64Pack_DrawnInTheRemasterFont()
    {
        var pack = C64;
        Assert.Equal(("phantasie2", "c64", "Commodore 64"), (pack.Game, pack.Platform, pack.Name));
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
        Assert.NotNull(pack.Resolve(PictureNames.MonsterAttacking(6)));   // VIPER: the extra block in sprite 0 (live s219)
        for (var race = 0; race < 15; race++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.PortraitIdle(race)));
            Assert.NotNull(pack.Resolve(PictureNames.PortraitAttacking(race)));
        }
        Assert.Null(pack.Resolve(PictureNames.PortraitIdle(15)));
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TownBackdrop, PictureNames.OverworldParty })
            Assert.NotNull(pack.Resolve(name));
        // s221: the ending and the party wipe are drawn (C64EndScreenTests); the sinking door and the sorry sign are not.
        // s223: defeat.temple for the C64's own party wipe (c64_defeat), not DOS's pages (I only).
        foreach (var name in new[] { PictureNames.EndingFinal, PictureNames.Decor, PictureNames.DefeatTemple })
            Assert.NotNull(pack.Resolve(name));
        foreach (var name in new[] { PictureNames.DecorDoor, PictureNames.DecorSorrySign })
            Assert.Null(pack.Resolve(name));
    }

    [OriginalDataFact]
    public void C64Pack_DrawsMapsFromCharacters_AsIIsUtil3Does()
    {
        var pack = C64;
        var tiles = pack.TileMaps!;
        Assert.Equal((8, 8, 8, 8), (tiles.TileWidth, tiles.TileHeight, tiles.GlyphWidth, tiles.GlyphHeight));
        Assert.Equal((19, 18), (tiles.ViewColumns, tiles.ViewRows));
        for (var b = 0; b < 256; b++)
        {
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(tiles.ColumnTile(b, 0))));
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(tiles.ColumnTile(b, 1))));
        }
        // II's routine puts the dungeon sprite on byte 50 only (I's on 50-59).
        Assert.Equal([new TileMapLayout.Overlay(40, 40, 1000, -4, 0), new(41, 41, 1001, -4, 0), new(50, 50, 1002, -4, 0)],
            tiles.Overlays);
        foreach (var o in tiles.Overlays)
            Assert.NotNull(pack.Resolve(PictureNames.OverworldTile(o.Tile)));
        Assert.Equal((-4, 0), tiles.PartyOffset);
        Assert.True(tiles.DungeonPartyOver);
        foreach (var glyph in new[] { 0x20, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED })
            Assert.NotNull(pack.Resolve(PictureNames.DungeonCell(glyph)));
    }

    [OriginalDataFact]
    public void WithoutTheStAndAppleExtracts_TheC64PackIsTheDefault()
    {
        var temp = Directory.CreateTempSubdirectory("p2-c64-only-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(temp.FullName, "phantasie2", "graphics"));
            foreach (var f in new[] { "index.json", "c64.json" })
                File.Copy(Path.Combine(GameData, "graphics", f), Path.Combine(graphics.FullName, f));
            foreach (var (f, root) in new[] { ("atari-st.json", "reverse-engineering/atari-st/phantasie2/extracted/graphics"),
                         ("apple2.json", "reverse-engineering/apple2/phantasie2/extracted/graphics") })
            {
                var text = File.ReadAllText(Path.Combine(GameData, "graphics", f));
                Assert.Contains(root, text);
                File.WriteAllText(Path.Combine(graphics.FullName, f), text.Replace(root, "no-such-folder/" + f));
            }

            var catalog = GraphicsPackCatalog.Load(Path.Combine(temp.FullName, "phantasie2"));
            Assert.Equal(["c64"], catalog.Available.Select(p => p.Id));
            Assert.Equal("c64", catalog.Default?.Id);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [OriginalDataFact]
    public void WithTheStExtracts_TheStStaysTheDefault()
    {
        var catalog = GraphicsPackCatalog.Load(GameData);
        if (catalog.Find("atari-st") is null)
            return;
        Assert.Equal("atari-st", catalog.Default?.Id);
        Assert.NotNull(catalog.Find("c64"));
    }
}
