using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P3C64;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s220: Phantasie III's C64 art (<see cref="P3C64Art"/>; notes <c>reverse-engineering/c64/phantasie3/GRAPHICS.md</c>). From
/// the C64PP v1.0 pair: the art equals the committed extract, the pictures follow the rules read in PHANT.OBJ / DUNGEON.OBJ
/// and seen in VICE, v1.2 (alt) gives the same pictures but its title, and the pack equals the shipped <c>c64.json</c>.
/// </summary>
public class P3C64ArtTests
{
    private const string Images = "reverse-engineering/c64/original-images/phantasie3/c64pp/";
    private const string Side1 = Images + "phantasie_iii_s1[ssi_1987](v1_0)(!).g64";
    private const string Side2 = Images + "phantasie_iii_s2[ssi_1987](v1_0)(!).g64";
    private const string Side1V12 = Images + "phantasie_iii_s1[ssi_1987](v1_2)(alt).g64";
    private const string Side2V12 = Images + "phantasie_iii_s2[ssi_1987](v1_2)(alt).g64";
    private const string ArtRoot = P3C64Art.RootPath;
    private const string Shipped = "src/OpenPhantasie.Data/GameData/phantasie3/graphics/c64.json";

    /// <summary>A file by name, side 1's when both hold one (PSET0 is on both, byte-equal).</summary>
    private static Func<string, byte[]?> Lookup(params string[] paths)
    {
        var sides = paths.Select(p =>
        {
            var raw = File.ReadAllBytes(Repo.Path(p));
            return D64Volume.TryOpen(G64Image.IsG64(raw) ? G64Image.Decode(raw).Image : raw) ?? throw new InvalidDataException(p);
        }).ToList();
        return name => sides.Select(v => v.Files.FirstOrDefault(e => e.Name == name) is { } e ? v.Read(e) : null)
            .FirstOrDefault(d => d is not null);
    }

    private static readonly Lazy<P3C64Art> Art = new(() =>
    {
        var art = new P3C64Art(null);
        art.Run(Lookup(Side1, Side2));
        return art;
    });

    private static RgbaImage Pic(string rel) => Png.Decode(Art.Value.Files[rel]);

    private static (byte, byte, byte, byte) Vic(int c) => (C64Gfx.Colodore[c].R, C64Gfx.Colodore[c].G, C64Gfx.Colodore[c].B, 255);

    /// <summary>The VIC colours a picture uses (opaque pixels only).</summary>
    private static HashSet<int> Colours(RgbaImage img)
    {
        var set = new HashSet<int>();
        for (var i = 0; i < img.Pixels.Length; i += 4)
            if (img.Pixels[i + 3] != 0)
                set.Add(Enumerable.Range(0, 16).First(c => Vic(c) == (img.Pixels[i], img.Pixels[i + 1], img.Pixels[i + 2], img.Pixels[i + 3])));
        return set;
    }

    [RequiresFilesFact(Side1, Side2, ArtRoot)]
    public void Art_EqualsTheCommittedExtract()
    {
        var files = Art.Value.Files;
        var dir = Repo.Path(ArtRoot);
        var n = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            Assert.True(files.TryGetValue(rel, out var bytes), $"{rel}: committed but not produced");
            Golden.PngEqualsFile(bytes!, $"{ArtRoot}/{rel}");
            n++;
        }
        Assert.Equal(files.Count, n);
        // title, town, 3 endings; 80 monsters idle + attacking; 16 races and 4 classes x 3 poses; 3 elementals x 2; 256
        // map tiles + the party; 7 dungeon glyphs; 2 character-set sheets
        Assert.Equal(5 + 160 + 48 + 12 + 6 + 257 + 7 + 2, n);
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Monsters_FollowPhantObjsColourTablesAndTheGiantRule()
    {
        // Wild Cat (picture $88): hires in brown (8), seen live s220.
        Assert.Equal([8], Colours(Pic("monsters/idle/04-wild-cat.png")));
        Assert.Equal((24, 21), (Pic("monsters/idle/04-wild-cat.png").Width, Pic("monsters/idle/04-wild-cat.png").Height));
        // Sting Beetle: multicolour, sprite colour 6, $D025 15, $D026 14 ($37C0 = $EF, $3810 = 6).
        Assert.Equal([6, 14, 15], Colours(Pic("monsters/idle/16-sting-beetle.png")).Order());
        // A giant ($A8, $BA..$CD) is two sprites stacked; its attacking frame is the next pair.
        var giant = Pic("monsters/idle/43-valley-giant.png");
        Assert.Equal((24, 42), (giant.Width, giant.Height));
        Assert.False(giant.PixelEquals(Pic("monsters/attacking/43-valley-giant.png")));
        Assert.Equal((24, 42), Art.Value.LargestMonster);
        // Boo Boo Kitty's 126 / 127 are PTOWN1's last two blocks: a picture, not an empty one.
        Assert.Equal(126, Art.Value.PictureOf[76]);
        Assert.NotEmpty(Colours(Pic("monsters/idle/76-boo-boo-kitty.png")));
        Assert.Equal(80, Art.Value.Files.Keys.Count(k => k.StartsWith("monsters/attacking/", StringComparison.Ordinal)));
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Figures_GoByClassForFourClassesAndByRaceOtherwise()
    {
        // Seen live (s220): CHUMLEY Troll Fighter -> race set 12 in green; TRACKER Human Ranger -> class set 7 in green;
        // FINGERS Sprite Thief -> race set 16 in yellow; GRASSHOPPER Elf Monk and SAURIAN Elf Wizard -> set 6 in light blue.
        Assert.Equal([5], Colours(Pic("portraits/idle/09-troll.png")));
        Assert.Equal([5], Colours(Pic("portraits/class/idle/2-ranger.png")));
        Assert.Equal([7], Colours(Pic("portraits/idle/06-sprite.png")));
        Assert.Equal([4], Colours(Pic("portraits/class/idle/4-priest.png")));
        Assert.True(Pic("portraits/class/idle/1-monk.png").PixelEquals(Pic("portraits/class/idle/5-wizard.png")));
        Assert.False(Pic("portraits/idle/00-human.png").PixelEquals(Pic("portraits/class/idle/2-ranger.png")));
        // The elementals: the Mud Elemental's monster picture in Air 1, Mist 14, Mud 8.
        Assert.Equal([1], Colours(Pic("portraits/elemental/air-idle.png")));
        Assert.Equal([14], Colours(Pic("portraits/elemental/mist-idle.png")));
        Assert.Equal([8], Colours(Pic("portraits/elemental/mud-idle.png")));
    }

    [RequiresFilesFact(Side1, Side2)]
    public void MapTownAndDungeon_FollowTheLiveScreens()
    {
        // Tile 197 = PSET0's character $41 four times (wild0 dump); the map band's colours are 0 / 1 / 5 and colour RAM 15.
        Assert.Equal((16, 16), (Pic("overworld/tiles/197.png").Width, Pic("overworld/tiles/197.png").Height));
        Assert.Subset(new HashSet<int> { 0, 1, 5, 7 }, Colours(Pic("overworld/tiles/197.png")));
        Assert.Contains(3, Colours(Pic("overworld/party.png"))); // colour RAM 11: cyan
        var town = Pic("screens/town.png");
        Assert.Equal((320, 200), (town.Width, town.Height));
        Assert.Subset(new HashSet<int> { 0, 1, 3, 5, 7, 9, 15 }, Colours(town));
        Assert.Equal([12], Colours(Pic("dungeon/glyph-1.png")));
        Assert.Equal([0], Colours(Pic("dungeon/glyph-0.png")));
        Assert.Equal([0, 5, 7], Colours(Pic("dungeon/glyph-6.png")).Order());
        foreach (var (name, mc1, mc2) in new[] { ("astral-plane", 8, 9), ("reward-good", 14, 10), ("reward-evil", 11, 8) })
        {
            var s = Colours(Pic($"screens/{name}.png"));
            Assert.Contains(mc1, s);
            Assert.Contains(mc2, s);
        }
    }

    /// <summary>v1.2 (alt): its own PHANT.OBJ (tables 2 bytes later) and TITLE; every other picture is v1.0's.</summary>
    [RequiresFilesFact(Side1, Side2, Side1V12, Side2V12)]
    public void V12_GivesTheSameArtButTheTitle()
    {
        var other = new P3C64Art(null);
        other.Run(Lookup(Side1V12, Side2V12));
        Assert.Equal("v1.2 (alt)", other.Version!.Name);
        Assert.Equal(Art.Value.Files.Keys.Order(StringComparer.Ordinal), other.Files.Keys.Order(StringComparer.Ordinal));
        foreach (var (rel, bytes) in Art.Value.Files)
            Assert.True(rel == "screens/title.png" || Png.Decode(bytes).PixelEquals(Png.Decode(other.Files[rel])), $"{rel}: v1.2 differs");
    }

    [Fact]
    public void Pack_MapsScandorsBytesAndTheClassPortraits()
    {
        var pack = P3C64ArtManifest.Pack();
        var swaps = pack["tile_maps"]!["tile_swaps"]!.AsArray().Select(s => (int)s![1]! * 1000 + (int)s[2]!).ToHashSet();
        Assert.Contains(0 * 1000 + 1, swaps); // water: DOS 0 -> C64 1
        Assert.Contains(38 * 1000 + 32, swaps); // the town tiles permuted
        Assert.All(pack["tile_maps"]!["tile_swaps"]!.AsArray(), s => Assert.Equal(P3C64ArtManifest.ScandorRegion, (int)s![0]!));
        Assert.Equal([4, 4], pack["tile_maps"]!["overworld_party_cell"]!.AsArray().Select(n => (int)n!));
        var pictures = pack["pictures"]!.AsObject();
        foreach (var c in new[] { 1, 2, 4, 5 })
            Assert.True(pictures.ContainsKey($"portrait.class.{c}.idle"));
        Assert.False(pictures.ContainsKey("portrait.class.0.idle"));
        Assert.False(pictures.ContainsKey("portrait.class.3.idle"));
    }

    [RequiresFilesFact(Shipped)]
    public void Pack_IsByteEqualToTheShippedC64Json()
    {
        var bytes = PyJson.SerializeUtf8(P3C64ArtManifest.Pack(), 2, ensureAscii: true);
        Assert.True(Golden.BytesEqualFile(bytes, Shipped), "c64.json differs from the import's");
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Import_WritesTheArtAndThePack()
    {
        var run = PipelineHarness.Run("p3-c64", Side1, Side2);
        Assert.Contains("GameData/phantasie3/graphics/c64.json", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/monsters/idle/76-boo-boo-kitty.png", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/overworld/party.png", run.AllOutputs.Keys);
    }
}
