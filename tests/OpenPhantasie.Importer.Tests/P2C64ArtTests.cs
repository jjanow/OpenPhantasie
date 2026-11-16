using System.Security.Cryptography;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P1C64;
using OpenPhantasie.Importer.Pipelines.P2C64;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s219: Phantasie II's C64 art (<see cref="P2C64Art"/>, I's engine <see cref="C64PhantasieArt"/>; notes
/// <c>reverse-engineering/c64/phantasie2/GRAPHICS.md</c>). From the C64PP v1.2 pair: the art equals the committed extract,
/// a few pictures' pixels equal fixed checksums, the map tiles follow II's own map routine, the files II shares with I
/// give I's pictures, and the pack equals the shipped <c>c64.json</c>.
/// </summary>
public class P2C64ArtTests
{
    private const string Images = "reverse-engineering/c64/original-images/phantasie2/";
    private const string Side1 = Images + "c64pp/phantasie_ii_s1[ssi_1986](v1_2)(never_used).g64";
    private const string Side2 = Images + "c64pp/phantasie_ii_s2[ssi_1986](v1_2)(never_used)(!).g64";
    private const string D64A = Images + "Phantasie_II_1986_SSI_Side_A.d64";
    private const string D64B = Images + "Phantasie_II_1986_SSI_Side_B.d64";
    private const string ArtRoot = P2C64Art.RootPath;
    private const string IArtRoot = P1C64Art.RootPath;
    private const string Shipped = "src/OpenPhantasie.Data/GameData/phantasie2/graphics/c64.json";

    /// <summary>Side 1's file when both hold one (the pipeline's <c>P2C64Source.Either</c>).</summary>
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

    private static readonly Lazy<P2C64Art> Art = new(() =>
    {
        var art = new P2C64Art(null);
        art.Run(Lookup(Side1, Side2));
        return art;
    });

    private static (byte R, byte G, byte B, byte A) Px(RgbaImage img, int x, int y)
    {
        var i = (y * img.Width + x) * 4;
        return (img.Pixels[i], img.Pixels[i + 1], img.Pixels[i + 2], img.Pixels[i + 3]);
    }

    private static (byte, byte, byte, byte) Vic(int c) => (C64Gfx.Colodore[c].R, C64Gfx.Colodore[c].G, C64Gfx.Colodore[c].B, 255);

    private static IEnumerable<(byte, byte, byte, byte)> Pixels(RgbaImage img) =>
        Enumerable.Range(0, img.Width * img.Height).Select(i => Px(img, i % img.Width, i / img.Width));

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
        // monsters idle + attacking, portraits (2 x 15), title, town, 13 tile classes x 2, 4 map sprites, 9 dungeon
        // glyphs, 14 item pictures, 3 character-set sheets: I's count
        Assert.Equal(80 + 80 + 30 + 1 + 1 + 26 + 4 + 9 + 14 + 3, n);
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Pictures_MatchTheirPixelChecksums()
    {
        var files = Art.Value.Files;
        string Sum(string rel) => Convert.ToHexString(SHA256.HashData(Png.Decode(files[rel]).Pixels))[..16];
        // Fixed checksums of the RGBA pixels (s219): a change in a decoder shows here even when the extract is regenerated.
        Assert.Equal("E1A64360271EC91B", Sum("screens/title.png"));
        Assert.Equal("8B3C9DC464CFB3DA", Sum("monsters/idle/00-skeleton.png"));
        Assert.Equal("33AFE13B055EF4A6", Sum("monsters/idle/79-pluto.png"));
        Assert.Equal("8334551A212C8D37", Sum("monsters/idle/17-pluto-s-giant-constr.png"));
        Assert.Equal("DBDE8DD6EC6E7831", Sum("overworld/tiles/class-04-even.png"));
        Assert.Equal((144, 84), Art.Value.LargestMonster); // PLUTO'S GIANT CONSTR. / FIRE ELEMENTAL / PLUTO'S DRAGON DEMON
        Assert.Equal(80, Art.Value.Attacking.Count);
    }

    [RequiresFilesFact(Side1, Side2)]
    public void MapTiles_FollowIIsMapRoutine()
    {
        var files = Art.Value.Files;
        RgbaImage Tile(string name, string parity) => Png.Decode(files[$"overworld/tiles/{name}-{parity}.png"]);
        // II's $C68A colours where I's differ: class 4 grey (12), class 5 yellow (7), class 7 black (the dark voids).
        Assert.Contains(Pixels(Tile("class-04", "even")), p => p == Vic(12));
        Assert.Contains(Pixels(Tile("class-05", "odd")), p => p == Vic(7));
        Assert.All(Pixels(Tile("class-07", "even")), p => Assert.Equal(Vic(0), p));
        // $C697: classes 8 and 10 print the same character ($74), brown and blue; 9 and 11 likewise ($72).
        foreach (var (a, b, ca, cb) in new[] { ("class-08", "class-10", 9, 6), ("class-09", "class-11", 9, 6) })
        foreach (var parity in new[] { "even", "odd" })
        {
            var (ta, tb) = (Tile(a, parity), Tile(b, parity));
            Assert.Equal(Pixels(ta).Select(p => p == Vic(ca)), Pixels(tb).Select(p => p == Vic(cb)));
            Assert.Contains(Pixels(ta), p => p == Vic(ca));
        }
        Assert.All(Pixels(Tile("fog", "odd")), p => Assert.Equal(Vic(0), p));
    }

    /// <summary>II's FONT+HER, HSPRIT and item pictures are I's byte for byte: the pictures made from them are I's.</summary>
    [RequiresFilesFact(Side1, Side2, IArtRoot)]
    public void SharedFiles_GiveIsPictures()
    {
        var files = Art.Value.Files;
        var shared = files.Keys.Where(k => k.StartsWith("portraits/", StringComparison.Ordinal)
                                           || k.StartsWith("items/", StringComparison.Ordinal)
                                           || k.StartsWith("dungeon/", StringComparison.Ordinal)
                                           || k is "screens/town.png" or "overworld/party.png" or "overworld/town.png"
                                               or "overworld/inn.png" or "overworld/dungeon.png"
                                               or "font/font-her-d000.png" or "font/font-her-d800-town.png").ToList();
        Assert.Equal(30 + 14 + 9 + 5 + 2, shared.Count);
        foreach (var rel in shared)
            Golden.PngEqualsFile(files[rel], $"{IArtRoot}/{rel}");
    }

    /// <summary>The held .d64 pair (a played v1.2 the profile also takes) gives the same art.</summary>
    [RequiresFilesFact(Side1, Side2, D64A, D64B)]
    public void TheHeldD64Pair_GivesTheSameArt()
    {
        var other = new P2C64Art(null);
        other.Run(Lookup(D64A, D64B));
        Assert.Equal(Art.Value.Files.Keys.Order(StringComparer.Ordinal), other.Files.Keys.Order(StringComparer.Ordinal));
        foreach (var (rel, bytes) in Art.Value.Files)
            Assert.True(Png.Decode(bytes).PixelEquals(Png.Decode(other.Files[rel])), $"{rel}: the .d64 pair's differs");
    }

    [RequiresFilesFact(Shipped)]
    public void Pack_IsByteEqualToTheShippedC64Json()
    {
        var bytes = PyJson.SerializeUtf8(P2C64ArtManifest.Pack(), 2, ensureAscii: true);
        Assert.True(Golden.BytesEqualFile(bytes, Shipped), "c64.json differs from the import's");
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Import_WritesTheArtAndThePack()
    {
        var run = PipelineHarness.Run("p2-c64", Side1, Side2);
        Assert.Contains("GameData/phantasie2/graphics/c64.json", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/monsters/idle/79-pluto.png", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/overworld/party.png", run.AllOutputs.Keys);
    }
}
