using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Phase 09 T4 (4c): Phantasie III's Amiga graphics pack (<c>GameData/phantasie3/graphics/amiga.json</c>), wired from the
/// extract (<c>tools/scripts/extract_amiga_p3_graphics.py</c>; formats in <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c>).
/// </summary>
public class Phantasie3GraphicsPackTests
{
    private static readonly GraphicsPackCatalog Catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static GraphicsPack Amiga => Catalog.Find("amiga") ?? throw new InvalidOperationException("III Amiga extracts missing");

    [OriginalDataFact]
    public void PhantasieIII_OffersTheAmigaPack_AsItsDefault()
    {
        Assert.Equal(["amiga"], Catalog.Available.Select(p => p.Id));
        Assert.Empty(Catalog.Missing);
        Assert.Same(Amiga, Catalog.Default);
        Assert.Equal(("phantasie3", "amiga"), (Amiga.Game, Amiga.Platform));
        Assert.Equal(PixelFont.Id, Amiga.Font);
        Assert.Empty(Amiga.Palette); // each PNG holds its own screen's colours
        Assert.Null(Amiga.MapLayout); // tile-drawn, not I's polygon regions
    }

    [OriginalDataFact]
    public void WiredManifest_IsTheExtractsPackJson()
    {
        var wired = Path.Combine(GameDataRepository.DefaultPathFor(GameId.P3), "graphics", "amiga.json");
        Assert.Equal(File.ReadAllText(Path.Combine(Amiga.RootDirectory, "pack.json")), File.ReadAllText(wired));
    }

    [OriginalDataFact]
    public void TileMaps_AreTheAmigaGeometry()
    {
        // FORMAT.md §4: 9×9 window of 16×16 tiles (0x9b92); §5: 8×5 dungeon glyphs (0x1e47a).
        var t = Amiga.TileMaps!;
        Assert.Equal((16, 16, 9, 9), (t.TileWidth, t.TileHeight, t.ViewColumns, t.ViewRows));
        Assert.Equal((8, 5), (t.GlyphWidth, t.GlyphHeight));
        Assert.Equal((46, 51), (t.UnseenGlyph, t.PartyGlyph));
    }

    [OriginalDataFact]
    public void OverworldView_StopsAtTheTopWithRow0_AtTheBottomAtTheSizeCounters()
    {
        // s102 (0x985e / 0x9912): the view's centre stays in 4 .. counter - 4. s125 [C] seen: the boot's plane change
        // (0x7758 -> 0x1eb18) sets Scandor's counters to 50 x 74, so its view ends at columns 42..50 (one past the map)
        // and rows 66..74 (the map's last), not 67..75. The Netherworld: 31 x 15. A map no larger than the view: 0.
        var counters = Amiga.TileMaps!.ViewCounters;
        Assert.Equal((50, 74), counters[Phantasie3Campaign.Scandor]);
        Assert.Equal((31, 15), counters[Phantasie3Campaign.Netherworld]);
        Assert.False(counters.ContainsKey(Phantasie3Campaign.LightPlane));
        Assert.Equal(66, Game1.OverworldViewMaxOrigin(75, 9, 74));
        Assert.Equal(42, Game1.OverworldViewMaxOrigin(50, 9, 50));
        Assert.Equal(0, Game1.OverworldViewMaxOrigin(9, 9, 9));
        Assert.Equal(7, Game1.OverworldViewMaxOrigin(16, 9, 15)); // the Netherworld's rows
        Assert.Equal(23, Game1.OverworldViewMaxOrigin(31, 9, 31)); // its columns: one past (0x1ec24)
        Assert.Equal(66, Game1.OverworldViewMaxOrigin(75, 9, null)); // no counter: the view stays on the map
    }

    [OriginalDataTheory] // 0x7e54: cell 14 + (12 − len) / 2 of 8 px; the exact centre is 160 − 4·len
    [InlineData("Pendragon", -4)] // 9: gap 3, half a cell left
    [InlineData("Sandy Shores", 0)] // 12
    [InlineData("Rocky Hills", -4)] // 11
    [InlineData("Deltor", 0)] // 6
    [InlineData("Light", -4)] // 5
    [InlineData("A thirteen ch", 4)] // 13: C truncates −1 / 2 to 0, half a cell right
    public void TownName_CellCentring_OddGapsSitHalfACellLeft(string name, int shift)
    {
        var t = Amiga.TownScreen!;
        Assert.Equal(12, t.NameCells);
        Assert.Equal(shift, t.NameShift(name.Length));
        Assert.Equal(0, TownScreenLayout.Default.NameShift(name.Length)); // packs without cells: exact centring
    }

    [OriginalDataFact]
    public void DarkAndDeath_TownsShowInTheDimPalette_WithABlackName()
    {
        // 0x78ee / 0xb228: towns 16–17 fade to 0x23050 (words 000 222 333 444 …) instead of 0x24010; name pen 0.
        var t = Amiga.TownScreen!;
        Assert.Equal([16, 17], t.DimTowns);
        Assert.Equal("#000000", t.DimNameColour);
        var from = Amiga.Palettes[t.DimFrom!];
        var to = Amiga.Palettes[t.DimPalette!];
        Assert.Equal(16, to.Count);
        Assert.Equal(["#000000", "#494949", "#6D6D6D", "#929292"], to.Take(4));
        var pixels = new[] { VirtualScreen.ParseColor(from[2]), VirtualScreen.ParseColor(from[15]), Microsoft.Xna.Framework.Color.Transparent };
        AssetCatalog.Recolour(pixels, from, to);
        Assert.Equal(VirtualScreen.ParseColor("#6D6D6D"), pixels[0]);
        Assert.Equal(VirtualScreen.ParseColor("#929292"), pixels[1]);
        Assert.Equal(Microsoft.Xna.Framework.Color.Transparent, pixels[2]);
    }

    [OriginalDataFact]
    public void PastTheMap_TheNextRowsFirstCell_TheNetherworldsF9_ElseBlack()
    {
        // s108 [C]: the view reads map byte y·w + x unchecked (0x9b9e); the Netherworld's 496 / 497 := 0xF9 (0x1ec62).
        var past = Amiga.TileMaps!.PastMap;
        Assert.Equal((0, 4), Game1.PastMapTile(50, 75, 0, 50, 3, past)!.Cell); // Scandor's column 50: row 4's first cell
        Assert.Null(Game1.PastMapTile(50, 75, 0, 10, 75, past)); // row 75: padding / tile bytes (undefined): black
        Assert.Null(Game1.PastMapTile(50, 75, 0, 50, 74, past));
        Assert.Equal((0, 15), Game1.PastMapTile(31, 16, 4, 31, 14, past)!.Cell);
        var corner = Game1.PastMapTile(31, 16, 4, 31, 15, past)!;
        Assert.Null(corner.Cell);
        Assert.Equal(249, corner.Tile);
        Assert.Null(Game1.PastMapTile(31, 16, 0, 31, 15, past)); // only the Netherworld's code writes it
    }

    [OriginalDataTheory] // 0x1e750: map byte -> glyph slot
    [InlineData(0, 46)]
    [InlineData(1, 46)]
    [InlineData(2, 50)]
    [InlineData(184, 50)]
    [InlineData(185, 47)]
    [InlineData(199, 47)]
    [InlineData(200, 48)]
    [InlineData(214, 48)]
    [InlineData(215, 52)]
    [InlineData(217, 52)]
    [InlineData(218, 50)]
    [InlineData(220, 50)]
    [InlineData(221, 49)]
    [InlineData(255, 49)]
    public void DungeonGlyph_FollowsTheAmigaCellRule(int cell, int glyph) =>
        Assert.Equal(glyph, Amiga.TileMaps!.DungeonGlyphFor(cell));

    [OriginalDataFact]
    public void Resolves_Tiles_Glyphs_Monsters_Portraits_AndScrollIV()
    {
        for (var tile = 0; tile < 250; tile++)
            Assert.NotNull(Amiga.Resolve(PictureNames.OverworldTile(tile)));
        Assert.Null(Amiga.Resolve(PictureNames.OverworldTile(250))); // phantasy.set holds 250 tiles
        for (var glyph = 45; glyph <= 53; glyph++)
            Assert.NotNull(Amiga.Resolve(PictureNames.DungeonCell(glyph)));
        for (var race = 0; race < 16; race++) // every remaster Race (Core, I's order)
            Assert.NotNull(Amiga.Resolve(PictureNames.PortraitIdle(race)));
        for (var id = 0; id < 80; id++)
        {
            // Every monster has a picture: byte 27 >= 48 cuts none on the Amiga (0x1b4c8), so Wolf (31, byte 65) and
            // Mud Elemental (53, byte 48) borrow one (D3-067).
            Assert.NotNull(Amiga.Resolve(PictureNames.MonsterIdle(id)));
            Assert.NotNull(Amiga.Resolve(PictureNames.MonsterAttacking(id)));
        }
        Assert.EndsWith("00-skeleton.png", Amiga.Resolve(PictureNames.MonsterIdle(0)));
        foreach (var kind in new[] { "idle", "attacking" })
        {
            bool Same(string a, string b) => File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
            var monster = kind == "idle" ? (Func<int, string>)PictureNames.MonsterIdle : PictureNames.MonsterAttacking;
            var elemental = kind == "idle" ? PictureNames.PortraitElemental : PictureNames.PortraitElementalAttacking;
            // D3-067: Wolf = the Wild Dog's picture (7, byte 0); Mud Elemental = the party elemental's (heros 22).
            Assert.True(Same(Amiga.Resolve(monster(31))!, Amiga.Resolve(monster(7))!));
            Assert.True(Same(Amiga.Resolve(monster(53))!, Amiga.Resolve(elemental)!));
        }
        Assert.NotNull(Amiga.Resolve(PictureNames.ScrollPicture(126)));
        Assert.Null(Amiga.Resolve(PictureNames.ScrollPicture(121))); // text scrolls have no picture
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TownBackdrop, PictureNames.OverworldParty })
            Assert.NotNull(Amiga.Resolve(name));
    }

    [OriginalDataFact]
    public void PhantasieI_Pack_HasNoTileMaps()
    {
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")!;
        Assert.Null(i.TileMaps);
        Assert.Null(i.Resolve(PictureNames.OverworldTile(0)));
    }

    [OriginalDataTheory]
    [InlineData(Race.Human, "00-human.png")]
    [InlineData(Race.Halfling, "03-halfling.png")]
    [InlineData(Race.Gnome, "04-gnome.png")]
    [InlineData(Race.Pixie, "05-pixie.png")]
    [InlineData(Race.Sprite, "06-sprite.png")]
    [InlineData(Race.Ogre, "07-ogre.png")]
    [InlineData(Race.Troll, "09-troll.png")]
    [InlineData(Race.LizardMan, "13-lizardman.png")]
    [InlineData(Race.Undead, "15-undead.png")]
    public void Portraits_AreFiledByTheRemasterRace(Race race, string file) =>
        // The Client asks portrait.<Core Race>; III's own race byte order (0x239a0) differs from 5 on.
        Assert.EndsWith(Path.Combine("portraits", "idle", file), Amiga.Resolve(PictureNames.PortraitIdle((int)race)));

    [OriginalDataFact]
    public void Elemental_HasPicture22_ButtonsAndFramePieces_Resolve()
    {
        Assert.EndsWith(Path.Combine("portraits", "idle", "elemental.png"), Amiga.Resolve(PictureNames.PortraitElemental));
        for (var slot = 16; slot <= 36; slot++)
            Assert.NotNull(Amiga.Resolve(PictureNames.BodyPiece(slot)));
        Assert.Null(Amiga.Resolve(PictureNames.BodyPiece(37))); // the spell sparkles are not figure pieces
        for (var slot = 54; slot <= 68; slot++)
            Assert.NotNull(Amiga.Resolve(PictureNames.ScrollFramePiece(slot)));
        foreach (var b in new[] { "ok", "cancel", "more", "yes", "no" })
        {
            Assert.NotNull(Amiga.Resolve(PictureNames.Button(b)));
            Assert.NotNull(Amiga.Resolve(PictureNames.Button(b, chosen: true)));
        }
        // D/W<n>.cmp: 2 = the Astral Plane's gates (defeat, 0x1dc16), 1 = Zeus, 0 = Pluto (reward, 0x1ee82).
        Assert.EndsWith(Path.Combine("screens", "w2.png"), Amiga.Resolve(PictureNames.SceneAstralPlane));
        Assert.EndsWith(Path.Combine("screens", "w1.png"), Amiga.Resolve(PictureNames.SceneRewardGood));
        Assert.EndsWith(Path.Combine("screens", "w0.png"), Amiga.Resolve(PictureNames.SceneRewardEvil));
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")!;
        Assert.Null(i.Resolve(PictureNames.PortraitElemental)); // I draws no elemental portrait
        Assert.Null(i.Resolve(PictureNames.SceneAstralPlane));
        Assert.Null(i.BodyFigure);
        Assert.Null(i.ScrollFrame);
        Assert.Null(i.KeepWindows); // I keeps DOS's Yes / No bar
    }

    [OriginalDataFact]
    public void BodyFigure_FollowsTheAmigaRoster()
    {
        // 0x9694: shape 16 + 7·state + piece; pieces head, right arm, left arm, torso, right leg, left leg, chest.
        var f = Amiga.BodyFigure!;
        Assert.Equal((32, 32), (f.Width, f.Height));
        var okay = f.Plan(null, dead: false);
        Assert.Equal([16, 17, 18, 19, 20, 21, 22], okay.Select(s => s.Slot!.Value));
        Assert.Equal([(8, 0), (0, 8), (16, 8), (0, 16), (0, 24), (16, 24), (8, 8)], okay.Select(s => (s.X, s.Y)));

        var hurt = new Dictionary<string, string>
        {
            ["head"] = "injured", ["right-arm"] = "broken", ["left-leg"] = "gone", ["torso"] = "okay",
        };
        var steps = f.Plan(hurt, dead: false);
        Assert.Equal([23, 31, 18, 19, 20, null, 22], steps.Select(s => s.Slot));
        Assert.Equal(new BodyFigureLayout.Step(null, 16, 24, 9, 8), steps[5]); // Gone: 0x648 clears x..x+8, y..y+7
        Assert.Equal(new BodyFigureLayout.Step(null, 0, 24, 25, 8),
            f.Plan(new Dictionary<string, string> { ["right-leg"] = "gone" }, dead: false)[4]); // the fifth piece: x + 24
        Assert.Empty(f.Plan(hurt, dead: true)); // the roster skips the figure at status 0
    }

    [OriginalDataFact]
    public void KeepWindows_AreTheAmigaWindows1And23()
    {
        // FORMAT.md §2.3 (s104): window structs at 0x23228, drawn by 0x3904 in the map palette 0x24010.
        var k = Amiga.KeepWindows!;
        Assert.Equal(("#000000", "#FF9224", 64, 18), (k.FillColour, k.TextColour, k.ButtonWidth, k.ButtonHeight));
        Assert.Equal(["#B6926D", "#926D49"], k.FrameColours); // map colours 4 / 3
        Assert.Equal([0, 72, 319, 140], k.Dungeon.Box);
        Assert.Equal([(8, 80)], k.Dungeon.Text);
        Assert.Equal(((72, 115), (192, 115)), (k.Dungeon.Ok, k.Dungeon.Cancel));
        Assert.Equal([16, 64, 300, 128], k.Fight.Box);
        Assert.Equal([(32, 72), (48, 88)], k.Fight.Text);
        Assert.Equal(((72, 104), (176, 104)), (k.Fight.Ok, k.Fight.Cancel));
        foreach (var name in new[] { "ok", "cancel" })
        {
            Assert.NotNull(Amiga.Resolve(PictureNames.Button(name)));
            Assert.NotNull(Amiga.Resolve(PictureNames.Button(name, chosen: true)));
        }
    }

    [OriginalDataFact]
    public void KeepWindowButtons_InADungeon_ShowInTheDungeonPalette()
    {
        // s125 seen (parity/shots/p3-amiga-keep-dungeon*.png): 0x3904 keeps the dungeon palette in force, so the button
        // art cut in the map palette shows its label (index 8) yellow #FFFF00 and the held label (index 5) #00DBFF.
        var k = Amiga.KeepWindows!;
        Assert.Equal(("map", "dungeon"), (k.ButtonPalette, k.DungeonPalette));
        var map = Amiga.Palettes["map"];
        var dungeon = Amiga.Palettes["dungeon"];
        foreach (var i in new[] { 0, 3, 4, 9 }) // fill, frame, text: the same in both
            Assert.Equal(map[i], dungeon[i]);
        Microsoft.Xna.Framework.Color[] pixels =
        [
            VirtualScreen.ParseColor("#FFDB49"), VirtualScreen.ParseColor("#24B692"), VirtualScreen.ParseColor("#B6926D"),
            Microsoft.Xna.Framework.Color.Transparent,
        ];
        AssetCatalog.Repalette(pixels, map, dungeon);
        Assert.Equal(VirtualScreen.ParseColor("#FFFF00"), pixels[0]);
        Assert.Equal(VirtualScreen.ParseColor("#00DBFF"), pixels[1]);
        Assert.Equal(VirtualScreen.ParseColor("#B6926D"), pixels[2]);
        Assert.Equal(Microsoft.Xna.Framework.Color.Transparent, pixels[3]);
    }

    [Theory]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, 1)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.O, 1)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Y, 1)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.C, 2)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.N, 2)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Down, null)] // no highlight to move
    public void KeepWindow_Keys_AreTheAmigas(Microsoft.Xna.Framework.Input.Keys key, int? expected) =>
        Assert.Equal(expected, Game1.KeepWindowChoice(key));

    [OriginalDataFact]
    public void P3WindowFor_DungeonEntryAndExitQuestions_AreWindowOne()
    {
        // s128 static: 0x1bb58 asks "Load previously saved dungeon?" (0x1bc18), "An exit. Will you take it?" (0x1bda4)
        // and "Would you like to save this map?" (0x1bdc4) through 0x1d77e = window 1 (struct 0x2325c), like "Keep …?".
        var k = Amiga.KeepWindows!;
        Assert.Same(k.Dungeon, Game1.P3WindowFor(SessionPromptKind.OverworldEntry, entryAsksLoad: true, fightFind: false, k));
        Assert.Same(k.Dungeon, Game1.P3WindowFor(SessionPromptKind.DungeonExitTakeIt, false, false, k));
        Assert.Same(k.Dungeon, Game1.P3WindowFor(SessionPromptKind.DungeonExitSaveMap, false, false, k));
        Assert.Same(k.Dungeon, Game1.P3WindowFor(SessionPromptKind.Treasure, false, fightFind: false, k));
        Assert.Same(k.Fight, Game1.P3WindowFor(SessionPromptKind.Treasure, false, fightFind: true, k));
        Assert.Null(Game1.P3WindowFor(SessionPromptKind.OverworldEntry, entryAsksLoad: false, fightFind: false, k)); // "Will you enter?"
        Assert.Null(Game1.P3WindowFor(SessionPromptKind.DungeonTrap, false, false, k)); // I's DISARM / LEAVE prompt
        // s134: the dungeon step's "<name> hears monsters. Continue?" (0x1c560) and "<name> spots a trap. Disarm it?"
        // (0x1c6c8) go through 0x1d77e too; Core asks them as III Yes / No event questions.
        Assert.Same(k.Dungeon, Game1.P3WindowFor(SessionPromptKind.DungeonEvent, false, false, k, yesNoEvent: true));
        Assert.Null(Game1.P3WindowFor(SessionPromptKind.DungeonEvent, false, false, k)); // other event menus: the page
    }

    [OriginalDataFact]
    public void MessageWindow_IsWindow7() // s134 static: 0xaaac draws struct 0x23394
    {
        var m = Amiga.KeepWindows!.Message!;
        Assert.Equal([0, 0, 319, 52], m.Box);
        Assert.Equal((8, 8), m.Text); // title cell (1, 1)
        Assert.Equal("#FFDB49", m.TextColour); // colour +18 = 8 in the map palette
        Assert.Equal((72, 27), m.Ok); // bytes (9, 27)
    }

    [OriginalDataFact]
    public void CreditsWindow_IsWindow0() // s140 static: 0x86e6 draws struct 0x23228, its title the pointer 0x2308c
    {
        var c = Amiga.KeepWindows!.Credits!;
        Assert.Equal([36, 44, 284, 168], c.Box);
        Assert.Equal((40, 56), c.Text); // title cell (5, 7)
        Assert.Equal("#FF9224", c.TextColour); // colour +18 = 9 in the map palette (the keep windows' colour)
        Assert.Equal(Amiga.KeepWindows.TextColour, c.TextColour);
        Assert.Equal((80, 143), c.Ok); // bytes (10, 143)
        Assert.Equal(["p3bar.credits"], c.TextIds);
    }

    [OriginalDataFact]
    public void PartyStatusWindow_IsWindow22() // s141: 0x8832 draws struct 0x236a0, labels 0x23160, values column 23
    {
        var w = Amiga.KeepWindows!.PartyStatus!;
        Assert.Equal([72, 28, 248, 180], w.Box);
        Assert.Equal((80, 40), w.Text); // 0x854 at cell (10, 5)
        Assert.Equal(5, w.ColourIndex); // colour 5 of the palette in force
        Assert.Equal(184, w.ValueX); // column 23
        Assert.Equal([56, 64, 72, 96, 104, 112, 120, 128, 136, 144], w.ValueRows); // rows 7-9, 12-18
        Assert.Equal((96, 155), w.Ok); // bytes (12, 155)
        Assert.Equal(["p3bar.party_status"], w.TextIds);
        Assert.Equal([new(1, 20), new(21, 40), new(41, 60), new(61, 100), new(101, 120), new(121, 140),
            new OpenPhantasie.Core.P3FoundItemKind(141, null)], w.Kinds);
    }

    [Theory]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, true, 1)] // "Press a key": any key (0x1e8c4(1))
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Escape, true, 1)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Q, true, 1)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.D2, false, 2)] // "Press a number": its numbers only
    [InlineData(Microsoft.Xna.Framework.Input.Keys.NumPad3, false, 3)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.D4, false, null)] // past the menu's three choices
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, false, null)] // no highlight on the page
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Down, false, null)]
    public void EventPage_Keys_AreTheAmigas(Microsoft.Xna.Framework.Input.Keys key, bool anyKey, int? expected)
    {
        var prompt = anyKey
            ? new SessionPrompt(SessionPromptKind.DungeonPage, ["A text."], [SessionPrompts.Continue])
            : new SessionPrompt(SessionPromptKind.DungeonEvent, ["Which?"], ["1)One", "2)Two", "3)Three"]);
        Assert.Equal(expected, Game1.EventPageChoice(prompt, anyKey, key));
    }

    [OriginalDataFact]
    public void ScrollFrame_IsTheAmigaParchment()
    {
        // 0x1e2c2: 40 columns, corners 54/55 top and 57/56 bottom, random runs, 23 rows of side pieces.
        var s = Amiga.ScrollFrame!;
        Assert.Equal([8, 8, 305, 185], s.Fill);
        var edges = s.Pick(n => n - 1);
        Assert.Equal(40 * 2 + 23 * 2, edges.Count);
        Assert.Contains(new ScrollFrameLayout.Edge(0, 0, 54), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(39, 0, 55), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(0, 24, 57), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(39, 24, 56), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(1, 0, 60), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(1, 24, 62), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(0, 23, 65), edges);
        Assert.Contains(new ScrollFrameLayout.Edge(39, 1, 68), edges);
        Assert.DoesNotContain(edges, e => e.Row is > 0 and < 24 && e.Column is > 0 and < 39);
    }
}
