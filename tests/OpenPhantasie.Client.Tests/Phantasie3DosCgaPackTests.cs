using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s160: Phantasie III's DOS CGA pack (<c>GameData/phantasie3/graphics/dos-cga.json</c>, art from
/// <c>tools/scripts/extract_dos_p3_graphics.py</c> / the importer's <c>P3CgaArt</c>; formats in
/// <c>reverse-engineering/dos/phantasie3/FORMAT.md</c> section 10).
/// </summary>
public class Phantasie3DosCgaPackTests
{
    private static string GameDir => GameDataRepository.DefaultPathFor(GameId.P3);
    private static readonly GraphicsPackCatalog Catalog = GraphicsPackCatalog.Load(GameDir);

    private static GraphicsPack Dos => Catalog.Find("dos-cga") ?? throw new InvalidOperationException("III DOS extracts missing");

    [OriginalDataFact]
    public void Offered_NeverTheDefault_WhileTheAmigaPackIsThere()
    {
        Assert.False(Dos.AutoSelect);
        Assert.Equal("amiga", Catalog.Default?.Id);
        Assert.Equal(("phantasie3", "dos"), (Dos.Game, Dos.Platform));
    }

    [OriginalDataFact]
    public void ADosOnlyImport_DrawsTheCgaPack()
    {
        // The Amiga pack's root missing (a DOS-only import): fallback_order picks DOS CGA (as I's index does).
        var dir = Directory.CreateTempSubdirectory("phantasie-p3-dos-only-").FullName;
        try
        {
            var graphics = Path.Combine(dir, "graphics");
            Directory.CreateDirectory(graphics);
            File.Copy(Path.Combine(GameDir, "graphics", "index.json"), Path.Combine(graphics, "index.json"));
            var amiga = File.ReadAllText(Path.Combine(GameDir, "graphics", "amiga.json"))
                .Replace("\"root\": \"reverse-engineering/amiga/phantasie3/extracted/graphics\"",
                    "\"root\": \"reverse-engineering/no-such-folder\"");
            File.WriteAllText(Path.Combine(graphics, "amiga.json"), amiga);
            File.Copy(Path.Combine(GameDir, "graphics", "dos-cga.json"), Path.Combine(graphics, "dos-cga.json"));
            var catalog = GraphicsPackCatalog.Load(dir);
            Assert.Equal(["dos-cga"], catalog.Available.Select(p => p.Id));
            Assert.Equal("dos-cga", catalog.Default?.Id);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [OriginalDataFact]
    public void TileMaps_DosGeometry_TheAmigaGlyphNumbers_AndScandorsTileSwaps()
    {
        var t = Dos.TileMaps!;
        Assert.Equal((16, 16, 9, 9), (t.TileWidth, t.TileHeight, t.ViewColumns, t.ViewRows)); // ovl3 0x12f1
        Assert.Equal((8, 5), (t.GlyphWidth, t.GlyphHeight));                                  // ovl4 0x5a8
        Assert.Equal((46, 51, 50), (t.UnseenGlyph, t.PartyGlyph, t.DefaultGlyph));
        // ovl4 0x21bb: 0/1 -> 46, 185..199 -> 47, 200..214 -> 48, 215..217 -> 52, >= 221 -> 49, else 50.
        Assert.Equal([46, 46, 47, 48, 52, 50, 49, 50],
            new[] { 0, 1, 190, 200, 216, 218, 255, 100 }.Select(t.DungeonGlyphFor));
        // ovl3 0x0: on Scandor (plane 0) six tiles are drawn as others; never elsewhere.
        Assert.Equal(144, t.DrawnTile(Phantasie3Campaign.Scandor, 154));
        Assert.Equal(1, t.DrawnTile(Phantasie3Campaign.Scandor, 249));
        Assert.Equal(237, t.DrawnTile(Phantasie3Campaign.Scandor, 243));
        Assert.Equal(249, t.DrawnTile(Phantasie3Campaign.Netherworld, 249));
        Assert.Equal(154, t.DrawnTile(Phantasie3Campaign.Netherworld, 154));
        Assert.Equal(50, t.DrawnTile(Phantasie3Campaign.Scandor, 50));
        Assert.Empty(t.ViewCounters); // the Amiga's size counters are the Amiga's
        Assert.Empty(Amiga().TileMaps!.TileSwaps); // and the swaps DOS's
    }

    private static GraphicsPack Amiga() => Catalog.Find("amiga")!;

    [OriginalDataFact]
    public void Pictures_ResolveToTheExtract()
    {
        foreach (var name in new[]
                 {
                     PictureNames.TitleCover, PictureNames.TownBackdrop, PictureNames.OverworldParty,
                     PictureNames.ScrollPicture(126), PictureNames.SceneAstralPlane, PictureNames.SceneRewardGood,
                     PictureNames.SceneRewardEvil, PictureNames.PortraitElemental, PictureNames.PortraitElementalAttacking,
                     PictureNames.PortraitGrave, PictureNames.OverworldTile(0), PictureNames.OverworldTile(249),
                     PictureNames.DungeonCell(46), PictureNames.DungeonCell(52), PictureNames.BodyPiece(35),
                 })
            Assert.True(Dos.Resolve(name) is { } p && File.Exists(p), name);
        // D3-067 (user s161): DOS shows the Skeleton for Wolf / Mud Elemental and the Forest Elf for the Troll Baby; the
        // pack borrows an analogous creature's picture instead, as the Amiga pack does (Wild Dog / party elemental / Troll).
        Assert.EndsWith("31-wolf.png", Dos.Resolve(PictureNames.MonsterIdle(31)));
        Assert.Equal(File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(7))!),
            File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(31))!));
        Assert.Equal(File.ReadAllBytes(Dos.Resolve(PictureNames.PortraitElemental)!),
            File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(53))!));
        Assert.NotEqual(File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(0))!),
            File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(31))!));
        Assert.NotEqual(File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(55))!),
            File.ReadAllBytes(Dos.Resolve(PictureNames.MonsterIdle(19))!));
        for (var m = 0; m < 80; m++)
            Assert.NotNull(Dos.Resolve(PictureNames.MonsterAttacking(m)));
        for (var race = 0; race < 16; race++)
            Assert.NotNull(Dos.Resolve(PictureNames.PortraitIdle(race)));
        // DOS has no dialog buttons: the Amiga windows stay off with this pack.
        Assert.Null(Dos.Resolve(PictureNames.Button("ok")));
        Assert.Null(Dos.KeepWindows);
    }

    [OriginalDataFact]
    public void BodyFigure_FourSets_GoneDrawsItsBlackSet()
    {
        var f = Dos.BodyFigure!;
        Assert.Equal((24, 32), (f.Width, f.Height));
        Assert.Equal(["okay", "injured", "broken", "gone"], f.States);
        Assert.Equal(9, f.Pieces.Count);
        var areas = new Dictionary<string, string> { ["head"] = "gone", ["left-leg"] = "broken" };
        var steps = f.Plan(areas, dead: false);
        Assert.Equal(9, steps.Count);
        Assert.All(steps, s => Assert.NotNull(s.Slot)); // never a clear: DOS draws set 3 (black)
        Assert.Equal(27, steps[0].Slot);                // head, set 3
        Assert.Equal(9 * 2 + 6, steps[6].Slot);         // left leg (stored piece 10), set 2
        Assert.Equal(2, steps[2].Slot);                 // the chest, always set 0
    }
}
