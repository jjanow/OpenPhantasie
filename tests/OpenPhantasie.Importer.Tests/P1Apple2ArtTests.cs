using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P1Apple2;
using OpenPhantasie.Importer.Pipelines.P2Apple2;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s223: Phantasie I's Apple II art (<see cref="P1Apple2Art"/>; notes <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c>).
/// I's engine is II's, so II's shape steps draw it: from the 4am pair the art equals the committed extract, the
/// original <c>.woz</c> pair gives the same, the pack equals the shipped <c>apple2.json</c>, and a side without
/// <c>SHAPES</c> warns and keeps the tables.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P1Apple2ArtTests
{
    private const string Images = "reverse-engineering/apple2/original-images/";
    private const string DskA = Images + "phantasie1/4am-crack/phantasie1-4am-side-a-boot.dsk";
    private const string DskB = Images + "phantasie1/4am-crack/phantasie1-4am-side-b-dungeon.dsk";
    private const string WozA = Images + "phantasie1/woz-original/phantasie1-boot.woz";
    private const string WozB = Images + "phantasie1/woz-original/phantasie1-dungeon.woz";
    private const string P2DskA = Images + "phantasie2/4am-crack/phantasie2-4am-side-a.dsk";
    private const string P2DskB = Images + "phantasie2/4am-crack/phantasie2-4am-side-b.dsk";
    private const string ArtRoot = P1Apple2Art.Root;
    private const string Shipped = "src/OpenPhantasie.Data/GameData/phantasie1/graphics/apple2.json";

    private static IReadOnlyDictionary<string, byte[]> ArtOf(params string[] sources)
    {
        var scan = ImportRunner.Scan(sources.Select(Repo.Path).ToList());
        var detection = scan.Detections.Single(d => d.Profile.Id == "p1-apple2");
        return P1Apple2Art.Build(detection.Files!, null).Files;
    }

    private static readonly Lazy<IReadOnlyDictionary<string, byte[]>> Art = new(() => ArtOf(DskA, DskB));

    [RequiresFilesFact(DskA, DskB, ArtRoot)]
    public void Art_EqualsTheCommittedExtract()
    {
        var files = Art.Value;
        var dir = Repo.Path(ArtRoot);
        var n = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            Assert.True(files.TryGetValue(rel, out var bytes), $"{rel}: committed but not produced");
            if (rel.EndsWith(".png", StringComparison.Ordinal))
                Golden.PngEqualsFile(bytes!, $"{ArtRoot}/{rel}");
            else
                Assert.True(Golden.BytesEqualFile(bytes!, $"{ArtRoot}/{rel}"), $"{rel} differs");
            n++;
        }
        Assert.Equal(files.Count, n);
        // 58 shapes, 80 monsters x 2 poses, 4 bodies x 3 poses, 16 races x 2 poses, 256 tiles + 12 odd-column looks +
        // the party block, 7 dungeon marks + the blank, 2 effects, the title, the index
        Assert.Equal(58 + 160 + 12 + 32 + 256 + 12 + 1 + 8 + 2 + 1 + 1, n);
    }

    [RequiresFilesFact(DskA, DskB, WozA, WozB)]
    public void WozPair_GivesTheSameArt()
    {
        var dsk = Art.Value;
        var woz = ArtOf(WozA, WozB);
        Assert.Equal(dsk.Keys.Order(StringComparer.Ordinal), woz.Keys.Order(StringComparer.Ordinal));
        foreach (var (rel, bytes) in dsk)
            Assert.True(bytes.AsSpan().SequenceEqual(woz[rel]), $"{rel} differs");
    }

    [RequiresFilesFact(DskA, DskB)]
    public void Pictures_AreDrawnAsTheProgramsDrawThem()
    {
        var files = Art.Value;
        // The title is HPIC, a whole hi-res page.
        var title = Png.Decode(files[P1Apple2Art.TitleFile]);
        Assert.Equal((280, 192), (title.Width, title.Height));
        // Every monster has both poses (pose B = shape S% + 1; SPLAT BEAST's S% = 6 makes its pose B DRIP SLIME's pose A, 7).
        for (var m = 0; m < 80; m++)
        {
            Assert.Single(files.Keys, k => k.StartsWith($"monsters/idle/{m:D2}-", StringComparison.Ordinal));
            Assert.Single(files.Keys, k => k.StartsWith($"monsters/attacking/{m:D2}-", StringComparison.Ordinal));
        }
        // Terrain classes 6 (HCOLOR 4, black) and 7 (shape 44, no vectors) draw nothing; fog (120+) is black too.
        foreach (var tile in new[] { "overworld/tiles/060.class-06.png", "overworld/tiles/070.class-07.png", "overworld/tiles/120.fog.png" })
        {
            var img = Png.Decode(files[tile]);
            Assert.Equal((7, 6), (img.Width, img.Height));
            for (var i = 0; i < img.Pixels.Length; i += 4)
                Assert.Equal(0, img.Pixels[i] | img.Pixels[i + 1] | img.Pixels[i + 2]);
        }
        // GRASS (class 0, HCOLOR 1) shows green.
        var grass = Png.Decode(files["overworld/tiles/000.class-00.png"]);
        Assert.Contains(Enumerable.Range(0, grass.Width * grass.Height),
            p => grass.Pixels[4 * p] == 20 && grass.Pixels[4 * p + 1] == 245 && grass.Pixels[4 * p + 2] == 60);
        Assert.DoesNotContain("effects/59-rock.png", files.Keys); // I has no shape 59
    }

    [RequiresFilesFact(DskA, P2DskA, P2DskB)]
    public void Shapes_AreIIsButForThreeTerrainTiles()
    {
        // I's SHAPES (58 shapes) against II's (59): the same bytes but shapes 45, 46, 48 (terrain classes 8, 9, 11).
        static byte[] Shapes(params string[] sources)
        {
            var scan = ImportRunner.Scan(sources.Select(Repo.Path).ToList());
            return scan.Detections.SelectMany(d => d.Files?.Containers ?? []).SelectMany(c => c.Files)
                .First(f => f.Path == "SHAPES").Data;
        }
        var one = new ShapeTable(Shapes(DskA, DskB));
        var two = new ShapeTable(Shapes(P2DskA, P2DskB));
        Assert.Equal((58, 59), (one.Count, two.Count));
        var differ = Enumerable.Range(1, 58).Where(n => !one.Vectors(n).SequenceEqual(two.Vectors(n))).ToList();
        Assert.Equal([45, 46, 48], differ);
    }

    [RequiresFilesFact(Shipped)]
    public void Pack_IsByteEqualToTheShippedAppleJson()
    {
        var bytes = PyJson.SerializeUtf8(P1Apple2ArtManifest.Pack(), 2, ensureAscii: true);
        Assert.True(Golden.BytesEqualFile(bytes, Shipped), "apple2.json differs from the import's");
    }

    [RequiresFilesFact(DskA, DskB)]
    public void Import_WritesTheArtAndThePack()
    {
        var run = PipelineHarness.Run("p1-apple2", DskA, DskB);
        Assert.Contains("GameData/phantasie1/graphics/apple2.json", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/monsters/idle/00-skeleton.png", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/overworld/party.png", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/{P1Apple2Art.TitleFile}", run.AllOutputs.Keys);
        Assert.DoesNotContain(run.Warnings, w => w.StartsWith("No Apple II art", StringComparison.Ordinal));
    }

    [RequiresFilesFact(DskA, DskB)]
    public void WithoutShapes_TheArtStepWarns_AndTheTablesStay() =>
        ArtStepWarns(files => files.RemoveAll(f => f.Path == "SHAPES"), "No Apple II art: SHAPES");

    [RequiresFilesFact(DskA, DskB)]
    public void ABadDrawingField_TheArtStepWarns_AndTheTablesStay()
    {
        // MN0 record 0 (SKELETON): field 13 (SCALE) "1" becomes "X". The tables never read it; the art does.
        ArtStepWarns(files =>
        {
            var i = files.FindIndex(f => f.Path == "MN0");
            if (i < 0)
                return;
            var data = (byte[])files[i].Data.Clone();
            var p = 0;
            for (var cr = 0; cr < 13; p++)
                if ((data[p] & 0x7F) == 0x0D)
                    cr++;
            Assert.Equal((byte)'1', (byte)(data[p] & 0x7F));
            data[p] = (byte)('X' | (data[p] & 0x80));
            files[i] = new Containers.ImportFile(files[i].Container, "MN0", data);
        }, "No Apple II art: MN0: record 0 field 13 'X' is not a number");
    }

    private static void ArtStepWarns(Action<List<Containers.ImportFile>> spoil, string warning)
    {
        var scan = ImportRunner.Scan([Repo.Path(DskA), Repo.Path(DskB)]);
        var detection = scan.Detections.Single(d => d.Profile.Id == "p1-apple2");
        foreach (var c in detection.Files!.Containers)
            spoil(c.Files);
        var staging = Directory.CreateTempSubdirectory("phantasie-import-test-").FullName;
        try
        {
            var session = new ImportSession { ShippedGameDataRoot = Repo.GameData, StagingRoot = staging, Detections = scan.Detections };
            var ctx = new ImportContext(session, detection);
            new P1Apple2Pipeline().Run(ctx);
            Assert.Contains(ctx.Warnings, w => w.StartsWith(warning, StringComparison.Ordinal));
            Assert.Contains("GameData/phantasie1/monsters.json", ctx.Pending.Keys);
            Assert.DoesNotContain("GameData/phantasie1/graphics/apple2.json", ctx.Pending.Keys);
            Assert.DoesNotContain(ctx.Pending.Keys, k => k.StartsWith($"files/{ArtRoot}/", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }
}
