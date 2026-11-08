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
        // s169 root 0x125d: DOS runs the view on the Amiga's counters (Scandor 50 x 74, the Netherworld 31 x 15).
        Assert.Equal((50, 74), t.ViewCounters[Phantasie3Campaign.Scandor]);
        Assert.Equal((31, 15), t.ViewCounters[Phantasie3Campaign.Netherworld]);
        Assert.Empty(Amiga().TileMaps!.TileSwaps); // the swaps are DOS's (the Amiga's 0x9b92 draws byte n as tile n)
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
    public void ScenePalettes_CgaPalette1_OnTheScreensDosSelectsIt()
    {
        // s169 (FORMAT.md section 10): 0x284:0xaaf(1) on the dungeon, a plane, the fight's field, the gods, the Astral
        // Plane; the files are cut in palette 0.
        Assert.Equal("cga0", Dos.CutPalette);
        Assert.Equal(["#000000", "#55FFFF", "#FF55FF", "#FFFFFF"], Dos.Palettes["cga1"]);
        Assert.Equal(Dos.Palette, Dos.Palettes["cga0"]);
        foreach (var scene in new[] { "dungeon", "plane", "combat_field", "reward", "astral" })
            Assert.Equal("cga1", Dos.ScenePalettes[scene]);
        Assert.Equal(5, Dos.ScenePalettes.Count); // the title, town, Scandor, orders screen and text pages stay in 0
        Assert.Empty(Amiga().ScenePalettes);
        Assert.Null(Amiga().CutPalette);
    }

    [Fact]
    public void MappedScreenPalette_SnapsToTheCutPalette_ShowsTheSameIndexOfTheOther()
    {
        // s169: CGA's palette select: the frame's pixel values stay, palette 1's colours show them.
        var p = ScreenPalette.Mapped(["#000000", "#55FF55", "#FF5555", "#FFFF55"], ["#000000", "#55FFFF", "#FF55FF", "#FFFFFF"]);
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x55, 0xFF, 0xFF), p.Nearest(new Microsoft.Xna.Framework.Color(0x55, 0xFF, 0x55)));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0xFF, 0x55, 0xFF), p.Nearest(new Microsoft.Xna.Framework.Color(0xF0, 0x50, 0x50)));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0xFF, 0xFF, 0xFF), p.Nearest(new Microsoft.Xna.Framework.Color(0xFF, 0xFF, 0x55)));
        Assert.Equal(Microsoft.Xna.Framework.Color.Black, p.Nearest(new Microsoft.Xna.Framework.Color(10, 10, 14)));
        Assert.Throws<ArgumentException>(() => ScreenPalette.Mapped(["#000000", "#FFFFFF"], ["#000000"]));
    }

    [Fact]
    public void PreviewSwitches_ReadTheirArguments()
    {
        Assert.True(TestParty.P3RewardGood(["x", "--p3-reward", "good"]));
        Assert.False(TestParty.P3RewardGood(["x", "--p3-reward"]));
        Assert.Null(TestParty.P3RewardGood(["x"]));
        Assert.Equal(126, TestParty.P3ScrollItem(["--p3-scroll", "126"]));
        Assert.Null(TestParty.P3ScrollItem(["--p3-scroll"]));
        Assert.Equal((1, 4, 2), TestParty.P3PlaneCell(["--p3-plane", "1", "4", "2"]));
        Assert.Null(TestParty.P3PlaneCell(["--p3-plane", "1", "4"]));
    }

    [OriginalDataFact]
    public void TextPage_TheGlyphFrame_ScrollIvOnIt()
    {
        // s169 root 0x4a3: corners 0 / 1 / 3 / 2, edges 4|6, 7|8, 9|11, 12|14 (rnd and 2: 5, 10, 13 never drawn); the fill
        // (8,8)-(311,191) inclusive in colour 3, text in 0; Scroll IV's map at (96, 8) (root 0x124).
        var f = Dos.ScrollFrame!;
        Assert.Equal((8, 40, 23), (f.Cell, f.Columns, f.Rows));
        Assert.Equal([8, 8, 304, 184], f.Fill);
        Assert.Equal(("#FFFF55", "#000000"), (f.FillColour, f.TextColour));
        Assert.Equal([0, 1], f.TopCorners);
        Assert.Equal([3, 2], f.BottomCorners);
        Assert.Equal([4, 6], f.Top);
        Assert.Equal([7, 8], f.Bottom);
        Assert.Equal([9, 11], f.Left);
        Assert.Equal([12, 14], f.Right);
        Assert.Equal([96, 8], f.PictureAt);
        Assert.Null(Amiga().ScrollFrame!.PictureAt); // the Amiga's Scroll IV is a whole screen
        for (var g = 0; g < 15; g++)
            Assert.EndsWith($"shapes/glyph8-{g:00}.png", Dos.Resolve(PictureNames.ScrollFramePiece(g))!.Replace('\\', '/'));
        var edges = f.Pick(n => n - 1); // the last choice everywhere
        Assert.Equal(40 * 2 + 23 * 2, edges.Count);
        Assert.Contains(new ScrollFrameLayout.Edge(39, 24, 2), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(0, 1, 11), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(39, 23, 14), edges);
    }

    [OriginalDataFact]
    public void Flash_DosNumbers_TheFramesClearColour0()
    {
        // s169 root 0x1537: the Amiga's jitter and cap; the member at 6 slot (no -1); the wait w / 60 s with no extra VBL.
        var c = Dos.CombatFlash!;
        Assert.Equal((6, 0, 60), (c.Wait, c.WaitExtraVbl, c.VblHz));
        Assert.Equal((3, 2, 36, 22, 6), (c.XJitter, c.RightAdd, c.XMax, c.YJitter, c.YAdd));
        Assert.Equal((6, 0, 140), (c.PartyXPerSlot, c.PartyXAdd, c.PartyY));
        Assert.Null(c.Sound);
        Assert.Equal((0 + 0 + 2) * 8 - 0, c.PartyOffset(0, 2, _ => 0).X); // slot 0: 2 bytes right, no clamp to 0
        var r = Dos.RewardScene!.Flash!;
        Assert.Equal((50, 36, 120, 7, 1, 8, 5, 0), (r.Count, r.XRange, r.YRange, r.WaitStart, r.WaitMin, r.WaitMax,
            r.WaitStepEvery, r.WaitExtraVbl));
        Assert.Null(Dos.RewardScene.Tune);
        // The frames: colour 0 transparent (an OR blit), the rest opaque.
        foreach (var name in r.Pictures.Concat(c.Pictures))
            Assert.Contains("shapes/frame24-", Dos.Resolve(name)!.Replace('\\', '/'));
    }

    [OriginalDataFact]
    public void TownName_CentredInTwelveCells_AnOddGapHalfACellLeft()
    {
        // s169 root 0xa9e2: cell 14 + ((12 - length) >> 1), row 2: "Pendragon" (9) at x 120, as the s143 shot.
        var t = Dos.TownScreen;
        Assert.Equal((12, 8, 16, "#000000"), (t.NameCells, t.NameCell, t.NameY, t.NameColour));
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
