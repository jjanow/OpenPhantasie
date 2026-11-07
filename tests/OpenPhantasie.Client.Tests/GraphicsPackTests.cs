using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// T1.5 graphics packs: the Client asks for named pictures; one manifest per game and platform maps names to files.
/// Amiga is Phantasie I's default, DOS CGA is offered but never the default, packs without their files are not offered,
/// and a pack never borrows another pack's art.
/// </summary>
public class GraphicsPackTests
{
    private static readonly GraphicsPackCatalog Catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path);

    private static GraphicsPack Amiga => Catalog.Find("amiga") ?? throw new InvalidOperationException("Amiga extracts missing");
    private static GraphicsPack Cga => Catalog.Find("dos-cga") ?? throw new InvalidOperationException("DOS extracts missing");

    [OriginalDataFact]
    public void PhantasieI_OffersAmigaAndCga_AmigaIsTheDefault()
    {
        Assert.Equal(["amiga", "dos-cga"], Catalog.Available.Select(p => p.Id));
        Assert.Empty(Catalog.Missing);
        Assert.Same(Amiga, Catalog.Default);
        Assert.True(Amiga.AutoSelect);
        Assert.False(Cga.AutoSelect); // CGA is drawn only when chosen explicitly
        Assert.All(Catalog.Available, p => Assert.Equal("phantasie1", p.Game));
    }

    [Fact]
    public void EveryPack_UsesAKnownFont_AndTheSharedLayoutSize()
    {
        foreach (var pack in Catalog.Available)
        {
            Assert.Equal(PixelFont.Id, pack.Font);
            Assert.Equal((ClientSettings.VirtualWidth, ClientSettings.VirtualHeight), (pack.Screen.Width, pack.Screen.Height));
            Assert.Equal((1, 1), (pack.Screen.PixelAspectX, pack.Screen.PixelAspectY)); // square, as before T1.5
        }
    }

    [OriginalDataFact]
    public void AmigaPack_KeepsTheRemasterScreen()
    {
        Assert.Equal(ScreenSpec.Default, Amiga.Screen);
        var c = VirtualScreen.ParseColor(Amiga.Screen.Background);
        Assert.Equal((16, 18, 28), (c.R, c.G, c.B)); // the pre-T1.5 clear colour
    }

    /// <summary>The pre-T1.5 AssetCatalog lookups, kept here to prove the Amiga pack resolves the very same files.</summary>
    private static string? LegacyPrefixed(string root, string folder, int id)
    {
        var dir = Path.Combine(root, folder, "idle");
        var prefix = $"{id:D2}-";
        return Directory.EnumerateFiles(dir, "*.png")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void AmigaPack_ResolvesExactlyTheFilesThePreviousCatalogLoaded()
    {
        var root = Amiga.RootDirectory;
        for (var id = 0; id < 120; id++)
            Assert.Equal(LegacyPrefixed(root, "monsters", id), Amiga.Resolve(PictureNames.MonsterIdle(id)));
        for (var race = 0; race < 20; race++)
            Assert.Equal(LegacyPrefixed(root, "portraits", race), Amiga.Resolve(PictureNames.PortraitIdle(race)));

        Assert.Equal(Path.Combine(root, "ui", "cover1.png"), Amiga.Resolve(PictureNames.TitleCover));
        Assert.Equal(Path.Combine(root, "ui", "pelnor-town.png"), Amiga.Resolve(PictureNames.TownBackdrop));
        Assert.Equal(Path.Combine(root, "ui", "decor.png"), Amiga.Resolve(PictureNames.Decor));
        Assert.Equal(Path.Combine(root, "ui", "final.png"), Amiga.Resolve(PictureNames.EndingFinal));

        var maps = Amiga.MapLayout ?? throw new InvalidOperationException("maps.json missing");
        var mapsRoot = Path.Combine(root, "maps");
        foreach (var (id, region) in maps.Regions)
            Assert.Equal(Path.Combine(mapsRoot, region.Image), Amiga.Resolve(PictureNames.OverworldRegion(id)));
        foreach (var (kind, image) in maps.IconImages)
            Assert.Equal(Path.Combine(mapsRoot, image), Amiga.Resolve(PictureNames.OverworldIcon(kind)));
        foreach (var (glyph, image) in maps.GlyphImages)
            Assert.Equal(Path.Combine(mapsRoot, image), Amiga.Resolve(PictureNames.DungeonGlyph(glyph)));
        Assert.Equal(Path.Combine(mapsRoot, maps.PartyImage), Amiga.Resolve(PictureNames.OverworldParty));
        Assert.Equal(Path.Combine(mapsRoot, maps.CompassImage), Amiga.Resolve(PictureNames.DungeonCompass));
        Assert.NotNull(Amiga.Resolve("ui.logo")); // s156: the About Phantasie requester's 176 x 80 logo (was a bad 320 x 44 extract)
    }

    [OriginalDataFact]
    public void CgaPack_HasTheDosArtOnly_NeverBorrowsAmiga()
    {
        Assert.NotNull(Cga.Resolve(PictureNames.TownBackdrop));
        Assert.NotNull(Cga.Resolve(PictureNames.OverworldFog));
        for (var p = 0; p < 15; p++)
            Assert.NotNull(Cga.Resolve(PictureNames.OverworldPattern(p)));
        for (var g = 0xe6; g <= 0xed; g++)
            Assert.NotNull(Cga.Resolve(PictureNames.DungeonGlyph(g)));

        // DOS shows no such pictures: the CGA pack has none, even though the Amiga pack does.
        Assert.NotNull(Amiga.Resolve(PictureNames.MonsterIdle(42)));
        Assert.Null(Cga.Resolve(PictureNames.MonsterIdle(42)));
        Assert.Null(Cga.Resolve(PictureNames.PortraitIdle(0)));
        Assert.Null(Cga.Resolve(PictureNames.TitleCover));
        Assert.Null(Cga.Resolve(PictureNames.OverworldRegion(1)));
        Assert.Null(Cga.MapLayout);
        Assert.All(Cga.FixedPictureNames.Select(Cga.Resolve), path => Assert.StartsWith(Cga.RootDirectory, path!));
    }

    [OriginalDataFact]
    public void CgaFiles_MatchTheDosTileRule()
    {
        // The map_tiles rule (asset-manifest.json) and the CGA pack name the same images.
        var tiles = GameDataRepository.LoadPhantasie1().MapTiles!;
        foreach (var (pattern, image) in tiles.PatternImages)
            Assert.Equal(Path.GetFullPath(Path.Combine(Cga.RootDirectory, "tiles", image)),
                Path.GetFullPath(Cga.Resolve(PictureNames.OverworldPattern(pattern))!));
        foreach (var (glyph, image) in tiles.GlyphImages)
            Assert.Equal(Path.GetFullPath(Path.Combine(Cga.RootDirectory, "tiles", image)),
                Path.GetFullPath(Cga.Resolve(PictureNames.DungeonGlyph(glyph))!));
    }

    private static string TempGame(string indexJson, params (string File, string Json)[] packs)
    {
        var dir = Path.Combine(Path.GetTempPath(), "phantasie-packs-" + Guid.NewGuid().ToString("N"), "testgame");
        Directory.CreateDirectory(Path.Combine(dir, "graphics"));
        File.WriteAllText(Path.Combine(dir, "graphics", "index.json"), indexJson);
        foreach (var (file, json) in packs)
            File.WriteAllText(Path.Combine(dir, "graphics", file), json);
        return dir;
    }

    [Fact]
    public void PackWithoutItsFiles_IsNotOffered_AndAnOptInPackNeverBecomesTheDefault()
    {
        var dir = TempGame(
            """{ "game": "testgame", "default_order": ["absent", "optin"], "packs": ["absent.json", "optin.json"] }""",
            ("absent.json", """{ "id": "absent", "game": "testgame", "platform": "x", "root": "no/such/folder", "requires": [] }"""),
            ("optin.json", """{ "id": "optin", "game": "testgame", "platform": "y", "auto_select": false, "root": "art", "requires": ["a.png"], "pictures": { "town.backdrop": "a.png" } }"""));
        Directory.CreateDirectory(Path.Combine(dir, "art"));
        File.WriteAllBytes(Path.Combine(dir, "art", "a.png"), [0]);
        try
        {
            var catalog = GraphicsPackCatalog.Load(dir, searchFrom: dir);
            Assert.Equal(["absent"], catalog.Missing);
            Assert.Equal(["optin"], catalog.Available.Select(p => p.Id));
            Assert.Null(catalog.Default); // only auto-select packs may be the default: no art rather than opt-in art
            Assert.Equal(Path.Combine(dir, "art", "a.png"), catalog.Find("optin")!.Resolve(PictureNames.TownBackdrop));
            Assert.Null(catalog.Find("absent"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void GameWithoutAnIndex_HasNoPacks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "phantasie-packs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var catalog = GraphicsPackCatalog.Load(dir);
            Assert.Empty(catalog.Available);
            Assert.Null(catalog.Default);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
