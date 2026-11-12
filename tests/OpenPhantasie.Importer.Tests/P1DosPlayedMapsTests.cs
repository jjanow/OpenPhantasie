using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Pipelines;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s205 (user): a played DOS I copy's maps (fog lifted, fixed fights spent) are imported as they are (decision 6) or,
/// when the player says so, restored by DOS I's own Reset rule, which gives a fresh copy's cells back.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P1DosPlayedMapsTests
{
    private const string Image = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string Profile = "p1-dos-1987";

    [Fact]
    public void RestoredRegion_FogsAndRearms_ButLeavesOut1Out5AndTheRestOfTheFile()
    {
        var file = new byte[1024];
        file[0] = 7;          // an unfogged plain cell
        file[1] = 18;         // a spent fixed fight (x8), unfogged
        file[2] = 120 + 28;   // a spent fight under fog
        file[519] = 125;      // already fogged
        file[520] = 33;       // feature records: not grid
        var files = new Dictionary<string, byte[]>
        {
            ["OUT2.DAT"] = file, ["OUT1.DAT"] = (byte[])file.Clone(), ["OUT5.DAT"] = (byte[])file.Clone(), ["X"] = [1, 2],
        };
        var read = P1DosPipeline.RestoredRegion(n => files[n]);

        var r = read("OUT2.DAT");
        Assert.Equal([127, 137, 147], r[..3]);
        Assert.Equal(125, r[519]);
        Assert.Equal(33, r[520]);
        Assert.Equal(7, file[0]); // the source is not changed
        Assert.Same(files["OUT1.DAT"], read("OUT1.DAT"));
        Assert.Same(files["OUT5.DAT"], read("OUT5.DAT"));
        Assert.Same(files["X"], read("X"));
    }

    [RequiresFilesFact(Image)]
    public void APlayedCopy_IsAsked_AndRestoredItImportsAsAFreshCopy()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1-played-maps-").FullName;
        try
        {
            // A played copy: the reference image's files in a folder with one region's fog lifted and a fight spent.
            var source = Path.Combine(dir, "game");
            Directory.CreateDirectory(source);
            var image = ImportRunner.Scan([Repo.Path(Image)]).Containers.Single(c => c.Files.Any(f => f.Name == "OUT2.DAT"));
            foreach (var f in image.Files)
                File.WriteAllBytes(Path.Combine(source, f.Name), f.Data);
            static int Fight(byte[] b) => Array.FindIndex(b[..520], c => c >= 120 && (c - 120) % 10 == 7);
            var region = Enumerable.Range(2, 18).Where(n => n != 5 && n != 17)
                .Select(n => $"OUT{n}.DAT").First(n => Fight(File.ReadAllBytes(Path.Combine(source, n))) >= 0);
            var outFile = Path.Combine(source, region);
            var bytes = File.ReadAllBytes(outFile);
            var fight = Fight(bytes);
            for (var i = 0; i < 520; i++)
                if (bytes[i] >= 120)
                    bytes[i] -= 120;
            bytes[fight]++;
            File.WriteAllBytes(outFile, bytes);

            var fresh = ImportRunner.Scan([Repo.Path(Image)]);
            var played = ImportRunner.Scan([source]);
            var d = played.Detections.Single(x => x.Profile.Id == Profile);
            Assert.Contains(region, d.PlayedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.True(ImportRunner.CanRestoreMaps(d));
            Assert.Equal([Profile], ImportRunner.RestorableMaps(played).Select(x => x.Profile.Id));
            Assert.Empty(ImportRunner.RestorableMaps(fresh));

            JsonNode Overworld(string root) =>
                JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie1", "overworld.json")))!;

            var freshRoot = Path.Combine(dir, "fresh");
            Assert.True(new ImportRunner(freshRoot, Repo.GameData).Run(fresh).Committed);

            var keptRoot = Path.Combine(dir, "kept");
            var kept = new ImportRunner(keptRoot, Repo.GameData).Run(played);
            Assert.Contains("fog is already lifted", kept.Versions.Single().Warnings[0]);
            Assert.False(JsonNode.DeepEquals(Overworld(freshRoot), Overworld(keptRoot)));
            Assert.Empty(ImportRunner.RestoredMaps(keptRoot));

            var restoredRoot = Path.Combine(dir, "restored");
            var restored = new ImportRunner(restoredRoot, Repo.GameData).Run(played, restoreMaps: new HashSet<string> { Profile });
            Assert.Contains("were restored as on a fresh copy", restored.Versions.Single().Warnings[0]);
            Assert.True(JsonNode.DeepEquals(Overworld(freshRoot), Overworld(restoredRoot)));
            Assert.Equal([Profile], ImportRunner.RestoredMaps(restoredRoot));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
