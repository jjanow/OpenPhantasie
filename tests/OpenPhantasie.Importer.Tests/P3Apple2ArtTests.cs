using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P3Apple2;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s212: Phantasie III's Apple II art step (<see cref="P3Apple2Art"/>, port of <c>extract_a2_p3_graphics.py</c> +
/// <c>build_a2_p3_asset_manifest.py</c>; notes <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c>). Golden: from the
/// original v3.0 WOZ pair the art equals the committed extract and the pack equals the shipped <c>apple2.json</c>. The
/// <c>[cr]</c> v1.0 copy gives the same sprites, shapes and maps but no title or ending (its screens are the cracker's).
/// The step is run on its own here: the <c>p3-apple2</c> profile and pipeline are wired separately.
/// </summary>
public class P3Apple2ArtTests
{
    private const string Images = "reverse-engineering/apple2/original-images/phantasie3/";
    private const string WozA = Images + "woz-original-v3.0/phantasie3-v3.0-disk1-side-a-boot.woz";
    private const string WozB = Images + "woz-original-v3.0/phantasie3-v3.0-disk1-side-b-utilities.woz";
    private const string CrA = Images + "Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk";
    private const string CrB = Images + "Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_B.dsk";
    private const string CrPlayer = Images + "Phantasie_III_1987_SSI_cr_Disk_3_of_3_Side_A_Player.dsk";
    private const string CrDungeon = Images + "Phantasie_III_1987_SSI_cr_Disk_2_of_3_Side_A_Dungeon.dsk";
    private const string ArtRoot = P3Apple2Art.Root;
    private const string Shipped = "src/OpenPhantasie.Data/GameData/phantasie3/graphics/apple2.json";

    private static Container Side(string repoRelative)
    {
        var raw = File.ReadAllBytes(Repo.Path(repoRelative));
        var image = WozImage.IsWoz(raw) ? WozImage.TryDecode(raw)!.Image : raw;
        var vol = RdosVolume.TryOpen(image) ?? throw new InvalidDataException(repoRelative);
        var c = new Container("raw-image", Path.GetFileName(repoRelative), repoRelative);
        foreach (var e in vol.Files)
            c.Files.Add(new ImportFile(c, e.Name, vol.Read(e)));
        return c;
    }

    private static Func<string, byte[]?> Lookup(Container c) => n => c.Files.FirstOrDefault(f => f.Path == n)?.Data;

    private static readonly Lazy<P3Apple2Art> V30 = new(() => Run(WozA, WozB));
    private static readonly Lazy<P3Apple2Art> Cr = new(() => Run(CrA, CrB));

    private static P3Apple2Art Run(string a, string b)
    {
        var art = new P3Apple2Art(null);
        art.Run(Lookup(Side(a)), Lookup(Side(b)));
        return art;
    }

    [RequiresFilesFact(WozA, WozB, ArtRoot)]
    public void Art_EqualsTheCommittedExtract()
    {
        var files = V30.Value.Files;
        var dir = Repo.Path(ArtRoot);
        int png = 0, other = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            Assert.True(files.TryGetValue(rel, out var bytes), $"{rel}: committed but not produced");
            if (rel.EndsWith(".png", StringComparison.Ordinal))
            {
                Golden.PngEqualsFile(bytes!, $"{ArtRoot}/{rel}");
                png++;
            }
            else
            {
                Assert.True(Golden.BytesEqualFile(bytes!, $"{ArtRoot}/{rel}"), $"{rel}: not byte-equal");
                other++;
            }
        }
        Assert.Equal(files.Count, png + other);
        Assert.Equal(2, other); // extraction-index.json, font/apple2-p3-hires.json
        Assert.Equal(
            48 + 24 + 160 + 20 + 32 + 12 + 12 + 256 + 1 + 9 + 7 + 4 + 5 + 2,
            png); // shapes, sprites, monster poses, figures, portraits, tile classes (even / odd), tiles, party, dungeon, effects, screens, views, font sheets
    }

    [RequiresFilesFact(WozA, WozB, Shipped)]
    public void Pack_IsByteEqualToTheShippedApple2Json()
    {
        var bytes = PyJson.SerializeUtf8(P3Apple2ArtManifest.Pack(V30.Value.Index), 2, ensureAscii: true);
        Assert.True(Golden.BytesEqualFile(bytes, Shipped));
    }

    [RequiresFilesFact(WozA, WozB)]
    public void V30_HasTheTitleAndTheEnding_FromItsOwnFiles()
    {
        var index = V30.Value.Index;
        Assert.Equal("3.0", (string)index["version"]!);
        var screens = index["screens"]!.AsObject();
        Assert.True(screens.ContainsKey("title") && screens.ContainsKey("title-picture") && screens.ContainsKey("ending"));
        var pictures = P3Apple2ArtManifest.Pack(index)["pictures"]!.AsObject();
        Assert.Equal(["title.cover", "title.credits", "scene.reward.good", "scene.reward.evil", "overworld.party"],
            pictures.Select(p => p.Key));
        // The ending is the decoded gods' room: the raw WIN PIC page is noise, the decoded one mostly black.
        var raw = Lookup(Side(WozB))("WIN PIC")!;
        var decoded = P3Apple2Art.DecodeWinPic(raw);
        Assert.True(decoded.Count(b => b == 0) > 4 * raw.Count(b => b == 0));
    }

    [RequiresFilesFact(CrA, CrB)]
    public void CrackedCopy_GivesNoTitleOrEnding_ButTheSameArt()
    {
        var cr = Cr.Value;
        Assert.Equal("1.0", (string)cr.Index["version"]!);
        var screens = cr.Index["screens"]!.AsObject();
        Assert.False(screens.ContainsKey("title") || screens.ContainsKey("ending"));
        Assert.True(screens.ContainsKey("_title") && screens.ContainsKey("_ending"));
        var pack = P3Apple2ArtManifest.Pack(cr.Index);
        Assert.Null(pack["title_sequence"]);
        Assert.Equal(["overworld.party"], pack["pictures"]!.AsObject().Select(p => p.Key));
        if (!Repo.Has(WozA, WozB))
            return;
        // Monsters, figures, tiles and marks are the same bytes on both copies (MONSTER SHAPES, SHAPES, NIKADEMUS equal).
        foreach (var (rel, bytes) in cr.Files.Where(f => f.Key.StartsWith("monsters/", StringComparison.Ordinal)
                     || f.Key.StartsWith("figures/", StringComparison.Ordinal) || f.Key.StartsWith("overworld/", StringComparison.Ordinal)
                     || f.Key.StartsWith("dungeon/", StringComparison.Ordinal)))
            Assert.Equal(V30.Value.Files[rel], bytes);
        // v1.0's IMPTEXT has thin strokes, v3.0's are doubled.
        Assert.NotEqual(V30.Value.Files["font/imptext.png"], cr.Files["font/imptext.png"]);
    }

    [RequiresFilesFact(CrA, CrB, CrPlayer, CrDungeon, WozA, WozB)]
    public void Sides_PreferTheOriginalCopy()
    {
        var cr = new[] { CrA, CrB, CrPlayer, CrDungeon }.Select(Side).ToList();
        var (a, b) = P3Apple2Art.Sides(cr);
        Assert.Equal("Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk", a.Description); // 1A before 3A (same files)
        Assert.Equal("Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_B.dsk", b.Description);
        var all = cr.Concat([Side(WozA), Side(WozB)]).ToList();
        (a, b) = P3Apple2Art.Sides(all);
        Assert.Equal(Path.GetFileName(WozA), a.Description);
        Assert.Equal(Path.GetFileName(WozB), b.Description);
    }

    [RequiresFilesFact(WozA, WozB, ArtRoot)]
    public void Sides_ResolveOnTheScannersOwnContainers_AndGiveTheCommittedIndex()
    {
        // What the pipeline will hand Import: the importer's own scan of the v3.0 WOZ pair (decode, RDOS 3.3 catalog with
        // names holding spaces and a deleted entry).
        var scan = ImportRunner.Scan([Repo.Path(Images + "woz-original-v3.0")]);
        var (a, b) = P3Apple2Art.Sides(scan.Containers);
        var art = new P3Apple2Art(null);
        art.Run(n => a.Files.FirstOrDefault(f => f.Path == n)?.Data, n => b.Files.FirstOrDefault(f => f.Path == n)?.Data);
        Assert.True(Golden.BytesEqualFile(art.Files["extraction-index.json"], $"{ArtRoot}/extraction-index.json"));
        // A missing side is an error the pipeline decides on.
        Assert.Throws<FileNotFoundException>(() => P3Apple2Art.Sides([a]));
    }

    [RequiresFilesFact(CrA, "src/OpenPhantasie.Data/GameData/phantasie3/monsters.json")] // not in the public repo
    public void Monsters_FollowTheRemastersOrder()
    {
        var mons = P3Apple2Art.Monsters(Lookup(Side(CrA)));
        Assert.Equal(80, mons.Count);
        var gameData = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Repo.GameData, "phantasie3", "monsters.json")))!["monsters"]!.AsArray();
        for (var i = 0; i < 80; i++)
        {
            var name = ((string)gameData[i]!["name"]!).ToUpperInvariant();
            Assert.Equal(i, mons[i].Index);
            Assert.Equal(name.Replace("FAIRY", "FIARY", StringComparison.Ordinal), mons[i].Name); // the Apple spells it FIARY
        }
        Assert.Equal(21, mons[77].Shape); // Nikademus: a giant, drawn from the NIKADEMUS overlay
        Assert.All(mons, m => Assert.True(m.Shape is >= 1 and <= 21 && m.Shape % 2 == 1, $"{m.Name}: S% {m.Shape}"));
    }

    [Fact]
    public void TerrainTables_CoverEveryMapByte_AndTheNetherworldSwaps()
    {
        for (var b = 0; b < 256; b++)
            Assert.NotNull(P3Apple2Art.ClassOf(b, P3Apple2Art.DosToApple));
        var covered = P3Apple2Art.DosToApple.Sum(r => r[1] - r[0] + 1);
        Assert.Equal(256, covered); // no overlaps
        Assert.Equal(7, P3Apple2Art.ClassOf(61, P3Apple2Art.DosToApple)); // Dense Fog: the Dark plane, Apple byte 74
        Assert.Equal(11, P3Apple2Art.ClassOf(70, P3Apple2Art.DosToApple)); // Bright Mist: the Light plane, Apple 114
        Assert.Equal(8, P3Apple2Art.ClassOf(197, P3Apple2Art.Nether));    // Lava in the Netherworld
        Assert.Equal(0, P3Apple2Art.ClassOf(197, P3Apple2Art.DosToApple)); // Meadows on Scandor
        Assert.Equal((26, 25, 19, 27), (P3Apple2Art.PartyBodyShape(7, 3), P3Apple2Art.PartyBodyShape(1, 3),
            P3Apple2Art.PartyBodyShape(0, 3), P3Apple2Art.PartyBodyShape(2, 5)));
    }

    [Fact]
    public void Font_MapsTheGamesCodesToTheCharactersTheyShow()
    {
        // COMMAND@ prints "COMMAND:", REDO> "REDO?", WILDERNESS< "WILDERNESS,", '=' a space (GRAPHICS.md section 7).
        Assert.Equal(':', P3Apple2Art.FontMap[0xC0]);
        Assert.Equal('?', P3Apple2Art.FontMap[0xBE]);
        Assert.Equal(',', P3Apple2Art.FontMap[0xBC]);
        Assert.Equal(' ', P3Apple2Art.FontMap[0xBD]);
        // The glyph of 'A' ($C1) sits at $1908, file offset $E0 of IMPTEXT ($1828).
        var font = new byte[472];
        font[0xE0] = 0x5A;
        Assert.Equal(0x5A, P3Apple2Art.FontGlyph(font, 0xC1)[0]);
        Assert.Equal(new byte[8], P3Apple2Art.FontGlyph(font, 0xA0)); // $1800: before the file
    }
}
