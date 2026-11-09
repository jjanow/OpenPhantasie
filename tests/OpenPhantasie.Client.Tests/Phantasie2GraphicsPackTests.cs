using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Phase 09 T3: Phantasie II's Atari ST graphics pack (<c>GameData/phantasie2/graphics/atari-st.json</c>), wired from the
/// extract's <c>pack.json</c>, and the ST's terrain fill patterns (D2-014: the ST patterns are shown, not the manual's
/// orange-wave lava and blue-dot mist).
/// </summary>
public class Phantasie2GraphicsPackTests
{
    private static readonly GraphicsPackCatalog Catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    private static GraphicsPack St => Catalog.Find("atari-st") ?? throw new InvalidOperationException("ST extracts missing");

    [OriginalDataFact]
    public void PhantasieII_OffersTheAtariStPack_AsItsDefault()
    {
        // s158: the Apple II pack sits beside it; the ST stays first in default_order.
        Assert.Equal(["atari-st", "apple2"], Catalog.Available.Select(p => p.Id));
        Assert.Empty(Catalog.Missing);
        Assert.Same(St, Catalog.Default);
        Assert.Equal(("phantasie2", "atari-st"), (St.Game, St.Platform));
        Assert.Equal(PixelFont.Id, St.Font);
        Assert.Equal((ClientSettings.VirtualWidth, ClientSettings.VirtualHeight), (St.Screen.Width, St.Screen.Height));
        // The ST switches palettes per screen and each PNG holds its own screen's colours: nothing is forced on the frame.
        Assert.Empty(St.Palette);
        Assert.Equal(["start", "town", "map", "combat"], St.Palettes.Keys);
        Assert.All(St.Palettes.Values, p => Assert.Equal(16, p.Count));
    }

    [OriginalDataFact]
    public void ColourCycle_RotatesMapRegisters4_7_14_Every14Vbls_OnTheOverworldPicturesOnly()
    {
        // FORMAT.md §10.1: VBL routine 0x1382e, period byte 0x13863 = 14; reg 14 := reg 7, reg 7 := reg 4, reg 4 := reg 14.
        var cycle = St.ColourCycle!;
        var map = St.Palettes["map"];
        Assert.Equal([map[4], map[7], map[14]], cycle.Colours);
        Assert.Equal(14, cycle.PeriodVbl);
        Assert.True(cycle.Covers(PictureNames.OverworldRegion(28)));
        Assert.True(cycle.Covers(PictureNames.OverworldIcon("dungeon")));
        Assert.True(cycle.Covers(PictureNames.OverworldParty));
        Assert.False(cycle.Covers(PictureNames.DungeonGlyph(0xe6))); // 0x10320 stops the cycle in a dungeon
        Assert.False(cycle.Covers(PictureNames.MonsterIdle(1)));     // and 0x7474 on an encounter
        Assert.False(cycle.Covers(PictureNames.TownBackdrop));       // and 0x73aa in town

        Assert.Equal(0, cycle.Phase(0));
        Assert.Equal(1, cycle.Phase(14.0 / cycle.VblHz));
        Assert.Equal(0, cycle.Phase(3 * 14.0 / cycle.VblHz));
        // One step: register 7 now shows register 4's colour, 14 shows 7's, 4 shows 14's.
        Assert.Equal(map[4], cycle.Shown(1, 1));
        Assert.Equal(map[7], cycle.Shown(2, 1));
        Assert.Equal(map[14], cycle.Shown(0, 1));
    }

    [OriginalDataFact]
    public void RecolourForCycle_SwapsOnlyTheCycledColours()
    {
        var cycle = St.ColourCycle!;
        var c4 = VirtualScreen.ParseColor(cycle.Colours[0]);
        var c7 = VirtualScreen.ParseColor(cycle.Colours[1]);
        var c14 = VirtualScreen.ParseColor(cycle.Colours[2]);
        var other = new Microsoft.Xna.Framework.Color(0x24, 0x91, 0x00);
        Microsoft.Xna.Framework.Color[] pixels = [c4, c7, c14, other];
        AssetCatalog.RecolourForCycle(pixels, cycle, 1);
        Assert.Equal([c14, c4, c7, other], pixels);
    }

    [OriginalDataTheory]
    [InlineData(28, "dungeon", 8)] // DNG7's ring of eight entrances (FORMAT.md §10.5)
    [InlineData(12, "castle", 5)]
    public void MultiIconRegions_DrawEveryCell(int region, string kind, int count)
    {
        var gfx = St.MapLayout!.Regions[region];
        Assert.Equal(count, gfx.AllIcons().Count(i => i.Kind == kind));
        Assert.Contains(gfx.Icons[kind], gfx.AllIcons().Where(i => i.Kind == kind).Select(i => (i.X, i.Y)));
    }

    [OriginalDataFact]
    public void AmigaIPack_KeepsOneIconPerKind()
    {
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")!;
        foreach (var gfx in i.MapLayout!.Regions.Values)
        {
            Assert.Null(gfx.IconCells);
            Assert.Equal(gfx.Icons.Select(k => (k.Key, k.Value.X, k.Value.Y)), gfx.AllIcons());
        }
    }

    [OriginalDataFact]
    public void AmigaIPack_WaterCycle_RotatesMapPens4_7_14_EveryFifthOfASecond_OnTheOverworldOnly()
    {
        // s128: h77 +0x178 rotates h78 +6/+8/+a (08B, 08A, 26A) into pens 4, 7, 14 every 200000 us (h80 +0xd2a);
        // seen on FS-UAE: a pen-4 pixel shows 08B, then 26A, then 08A.
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")!;
        var cycle = i.ColourCycle!;
        var map = i.Palettes["map"];
        Assert.Equal(["#0088BB", "#0088AA", "#2266AA"], cycle.Colours);
        Assert.Equal([map[4], map[7], map[14]], cycle.Colours);
        Assert.Equal(0.2, (double)cycle.PeriodVbl / cycle.VblHz, 9);
        Assert.Equal(1, cycle.Phase(0.2));
        Assert.Equal(0, cycle.Phase(0.6));
        Assert.Equal(["#0088BB", "#2266AA", "#0088AA"], Enumerable.Range(0, 3).Select(p => cycle.Shown(0, p)));
        Assert.True(cycle.Covers(PictureNames.OverworldRegion(1)));
        Assert.True(cycle.Covers(PictureNames.OverworldIcon("inn")));
        Assert.False(cycle.Covers(PictureNames.OverworldParty));    // a hardware sprite: its own pens
        Assert.False(cycle.Covers(PictureNames.DungeonGlyph(0xe6))); // h62 +0xa stops it off the wilderness
        Assert.False(cycle.Covers(PictureNames.TownBackdrop));
    }

    [OriginalDataFact]
    public void WiredManifest_IsTheExtractsPackJson()
    {
        var wired = Path.Combine(GameDataRepository.DefaultPathFor(GameId.P2), "graphics", "atari-st.json");
        var extract = Path.Combine(St.RootDirectory, "pack.json");
        Assert.Equal(File.ReadAllText(extract), File.ReadAllText(wired)); // build_st_p2_asset_manifest.py writes both
    }

    [OriginalDataFact]
    public void StPack_ResolvesEveryMonsterPortraitRegionAndScreen()
    {
        for (var id = 0; id < 80; id++)
            Assert.NotNull(St.Resolve(PictureNames.MonsterIdle(id)));
        for (var race = 0; race < 16; race++)
            Assert.NotNull(St.Resolve(PictureNames.PortraitIdle(race)));
        foreach (var region in P2Regions())
            Assert.NotNull(St.Resolve(PictureNames.OverworldRegion(region)));
        foreach (var name in new[] { PictureNames.TitleCover, PictureNames.TownBackdrop, PictureNames.Decor,
                     PictureNames.OverworldParty, PictureNames.DungeonCompass })
            Assert.NotNull(St.Resolve(name));
        Assert.NotNull(St.MapLayout);
        Assert.Equal((12, 9), (St.MapLayout!.CellWidth, St.MapLayout.CellHeight));
    }

    [OriginalDataTheory]
    [InlineData(7)]  // dark voids: solid black
    [InlineData(8)]  // molten lava: the blue / red swirl
    [InlineData(11)] // mist: sparse grey dots
    [InlineData(9)]  // the ST's only orange pattern: ordinary land
    [InlineData(10)] // water
    public void D2014_TerrainClasses_DrawWithTheStsOwnFillPatterns(int cellClass)
    {
        // terrain.md §5: pattern number = cell class. The pattern files are the ST's vsf_udpat patterns.
        var path = St.Resolve(PictureNames.OverworldPattern(cellClass));
        Assert.NotNull(path);
        Assert.Equal($"{cellClass:D2}.png", Path.GetFileName(path));
        Assert.Contains(Path.Combine("maps", "overworld", "patterns"), path);
    }

    [OriginalDataFact]
    public void D2014_TerrainKey_ListsTheManualsRows_WithTheStsPictures()
    {
        // s171 QoL TERRAIN KEY: the rulebook's key (Apple II page + p.27), each row's swatches from the ST pack.
        var key = TerrainKey.For(GameId.P2);
        var table = StringTable.Load(GameDataLocator.GameFolder(GameId.P2));
        Assert.Equal(
            ["PARTY", "TOWN/INN", "DUNGEON", "PATH", "DESERT", "GRASSLAND/FOREST", "MOUNTAINS/HILLS", "RIVER/LAKE",
                "MOLTEN LAVA", "MIST", "DARK VOIDS"],
            key.Select(e => table.Get(e.Label)));
        Assert.All(key, e => Assert.NotEmpty(e.Pictures));
        Assert.All(key.SelectMany(e => e.Pictures), p => Assert.NotNull(St.Resolve(p)));
        // terrain.md section 5: lava = class 8, mist = 11, dark voids = 7, water = 10.
        Assert.Equal([PictureNames.OverworldPattern(8)], key.Single(e => e.Label.EndsWith("molten_lava")).Pictures);
        Assert.Equal([PictureNames.OverworldPattern(11)], key.Single(e => e.Label.EndsWith(".mist")).Pictures);
        Assert.Equal([PictureNames.OverworldPattern(7)], key.Single(e => e.Label.EndsWith("dark_voids")).Pictures);
        Assert.Equal([PictureNames.OverworldPattern(10)], key.Single(e => e.Label.EndsWith("river_lake")).Pictures);
        Assert.Empty(TerrainKey.For(GameId.P1)); // the key is II's (the ST shows none; I has no such file)
    }

    [OriginalDataFact]
    public void AmigaIPack_HasNoPatterns_SoNoBorrowing()
    {
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")!;
        Assert.Empty(i.MapLayout!.PatternImages);
        Assert.Null(i.Resolve(PictureNames.OverworldPattern(8)));
    }

    private static IEnumerable<int> P2Regions() => GameTables.Load(GameId.P2).OverworldRegions.Select(r => r.RegionId);
}
