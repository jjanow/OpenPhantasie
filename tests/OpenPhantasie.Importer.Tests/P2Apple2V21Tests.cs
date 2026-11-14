using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s212: Phantasie II, Apple II v2.1 8/19/86 (profile <c>p2-apple2-2.1</c>; the A2CAN original pair, RDOS 3.3, a
/// played copy). Its data files are v1.0's (FORMAT.md section 4), so an import of it, with the start fog restored
/// (D2-052), is the unplayed Memorial Set's v1.0 import in every table, art file and string line; only the version's
/// name in the provenance lines differs. With v1.0 in the same run, v2.1's tables win.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class P2Apple2V21Tests
{
    private const string Profile = "p2-apple2-2.1";
    private const string V10 = "p2-apple2";
    private const string V21A = "reverse-engineering/apple2/original-images/phantasie2/v2.1-a2can/phantasie2-v2.1-side-a-boot.dsk";
    private const string V21B = "reverse-engineering/apple2/original-images/phantasie2/v2.1-a2can/phantasie2-v2.1-side-b-dungeon.dsk";
    private const string MemA = "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2A.DSK";
    private const string MemB = "reverse-engineering/apple2/original-images/phantasie2/memorial-set-gog/PHANT2B.DSK";
    private const string SideA = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-a.dsk";
    private const string SideB = "reverse-engineering/apple2/original-images/phantasie2/4am-crack/phantasie2-4am-side-b.dsk";

    private static ScanResult ScanV21() => ImportRunner.Scan([Repo.Path(V21A), Repo.Path(V21B)]);

    [RequiresFilesFact(V21A, V21B)]
    public void Detection_IsReady_APlayedCopy_ItsMapsRestorable()
    {
        var scan = ScanV21();
        var d = Assert.Single(scan.Detections);
        Assert.Equal(Profile, d.Profile.Id);
        Assert.Equal(DetectionStatus.Ready, d.Status);
        Assert.Empty(scan.Notes); // no "another build" of v1.0
        // FORMAT.md section 4: ten regions stored back (OUTT1 cut to 756 B, its bytes unchanged), six towns saved.
        string[] played = ["OUTT1", "OUTT3", "OUTT5", "OUTT6", "OUTT7", "OUTT8", "OUTT11", "OUTT12", "OUTT17", "OUTT18",
                           "TWN21", "TWN22", "TWN23", "TWN24", "TWN25", "TWN26"];
        Assert.Equal(played.Order(StringComparer.Ordinal), d.PlayedFiles.Order(StringComparer.Ordinal));
        Assert.True(ImportRunner.CanRestoreMaps(d));
        Assert.False(ImportRunner.MapsHoldFights(d));
        Assert.Equal([Profile], ImportRunner.RestorableMaps(scan).Select(x => x.Profile.Id));
        Assert.IsType<Pipelines.P2Apple2.P2Apple2V21Pipeline>(PipelineRegistry.For(Profile));
    }

    /// <summary>v1.0's pairs stay v1.0 alone: v2.1's key-file names on their disks (other sha1s) add no detection.</summary>
    [RequiresFilesTheory(SideA, SideB, MemA, MemB)]
    [InlineData(SideA, SideB)]
    [InlineData(MemA, MemB)]
    public void V10Pairs_AreNotV21(string a, string b)
    {
        var scan = ImportRunner.Scan([Repo.Path(a), Repo.Path(b)]);
        var d = Assert.Single(scan.Detections);
        Assert.Equal(V10, d.Profile.Id);
        Assert.Equal(DetectionStatus.Ready, d.Status);
        Assert.Empty(scan.Notes);
    }

    /// <summary>The profile's pristine sha1s are v1.0's for the regions and towns (FORMAT.md section 4), none for the saves.</summary>
    [Fact]
    public void Profile_PristineRegionsAndTownsAreV10s()
    {
        var v21 = VersionProfiles.All.Single(p => p.Id == Profile);
        var v10 = VersionProfiles.All.Single(p => p.Id == V10);
        Assert.True(v21.IsSupported);
        Assert.Equal(v10.Feeds, v21.Feeds);
        Assert.Equal(v10.MutableFiles.Where(f => f.Name.StartsWith("OUTT", StringComparison.Ordinal) || f.Name.StartsWith("TWN2", StringComparison.Ordinal))
                         .Select(f => (f.Name, f.Sha1)),
                     v21.MutableFiles.Select(f => (f.Name, f.Sha1)));
        Assert.DoesNotContain(v21.MutableFiles, f => f.Name is "CHAR2" or "DNGSAV" or "DNGCHK");
    }

    /// <summary>
    /// v2.1 restored = the Memorial Set's unplayed v1.0: every GameData table, the art and the resolved string lines,
    /// but for the version's name (<c>_source</c>, game.json <c>profile</c>, the string files' notes and names) and each
    /// region's file length (756 B once stored back).
    /// </summary>
    [RequiresFilesFact(V21A, V21B, MemA, MemB)]
    public void RestoredImport_EqualsTheUnplayedV10Import_ButForTheVersionsName()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p2a2-v21-").FullName;
        try
        {
            var memRoot = Path.Combine(dir, "v10");
            var memReport = new ImportRunner(memRoot, Repo.GameData).Run(ImportRunner.Scan([Repo.Path(MemA), Repo.Path(MemB)]));
            Assert.True(memReport.Committed);
            Assert.Empty(memReport.Skipped);
            var v21Root = Path.Combine(dir, "v21");
            var report = new ImportRunner(v21Root, Repo.GameData).Run(ScanV21(), restoreMaps: new HashSet<string> { Profile });
            Assert.Empty(report.Skipped);
            var version = Assert.Single(report.Versions);
            Assert.True(version.Ok, version.Error);
            Assert.Contains("were restored as on a fresh copy: the fog is back.", version.Warnings[0]);

            var memFiles = Files(memRoot, V10);
            var v21Files = Files(v21Root, Profile);
            Assert.Equal(memFiles.Keys.Order(StringComparer.Ordinal), v21Files.Keys.Order(StringComparer.Ordinal));
            Assert.Contains("GameData/phantasie2/strings/en.PROFILE.json", memFiles.Keys);
            Assert.Contains("GameData/phantasie2/strings/en.own.PROFILE.json", memFiles.Keys);
            Assert.Contains("GameData/phantasie1/strings/en.PROFILE.json", memFiles.Keys); // most moved OUTDOORS2 lines
            Assert.Contains("GameData/shared/strings/en.PROFILE.json", memFiles.Keys);
            foreach (var (key, bytes) in memFiles)
            {
                if (!key.EndsWith(".json", StringComparison.Ordinal))
                {
                    Assert.True(bytes.AsSpan().SequenceEqual(v21Files[key]), key);
                    continue;
                }
                var a = Normalise(JsonNode.Parse(bytes)!);
                var b = Normalise(JsonNode.Parse(v21Files[key])!);
                Assert.True(JsonNode.DeepEquals(a, b), key);
            }

            var game = JsonNode.Parse(File.ReadAllBytes(Path.Combine(v21Root, "GameData", "phantasie2", "game.json")))!;
            Assert.Equal(Profile, (string?)game["profile"]);
            Assert.Contains("v2.1 8/19/86", (string?)game["_source"]);
            var monsters = File.ReadAllText(Path.Combine(v21Root, "GameData", "phantasie2", "monsters.json"));
            Assert.DoesNotContain("v1.0 2/5/86", monsters);
            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(v21Root, ImportRunner.ManifestName)))!;
            Assert.Equal(Profile, (string?)manifest["games"]!["phantasie2"]!["tables"]);
            Assert.Equal([Profile], ImportRunner.RestoredMaps(v21Root));

            // It plays: a new game starts, with the start fog (OUT1 / OUT2 open, the rest fogged).
            GameDataLocator.UseImport(v21Root);
            var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));
            GameText.Source = StringTable.ForGame(GameId.P2);
            var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
            s.StartNewGame(GameOptions.ForPreset(GameId.P2));
            var world = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())).Wilderness!.RegionTiles!;
            Assert.Equal(17, world.Count);
            Assert.True(world.All(r => r.Value.All(t => t >= OverworldRegion.FogOffset == (r.Key is not (1 or 2)))));
        }
        finally
        {
            GameDataLocator.UseImport(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Kept as played, the fog stays lifted on the ten stored-back regions and the warning says so.</summary>
    [RequiresFilesFact(V21A, V21B)]
    public void KeptImport_WarnsTheFogIsLifted()
    {
        var r = PipelineHarness.Run(Profile, V21A, V21B);
        var warning = ImportRunner.PlayedWarning(r.Detection);
        Assert.Contains("fog is already lifted", warning);
        Assert.Contains("OUTT3", warning);
        Assert.DoesNotContain("fixed fights", warning);
        Assert.DoesNotContain(r.Warnings, w => w.Contains("is neither", StringComparison.Ordinal)); // every ST cell applied
    }

    /// <summary>Both Apple versions in one run: v2.1 runs first and its tables win; v1.0 adds only its art pack.</summary>
    [RequiresFilesFact(V21A, V21B, SideA, SideB)]
    public void WithV10_V21sTablesWin_OnlyV21IsAskedAboutItsMaps()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p2a2-both-").FullName;
        try
        {
            var scan = ImportRunner.Scan([Repo.Path(SideA), Repo.Path(SideB), Repo.Path(V21A), Repo.Path(V21B)]);
            Assert.Equal([V10, Profile], scan.Importable.Select(d => d.Profile.Id).Order(StringComparer.Ordinal));
            Assert.Equal([Profile], ImportRunner.RestorableMaps(scan).Select(d => d.Profile.Id));
            var root = Path.Combine(dir, "import");
            var report = new ImportRunner(root, Repo.GameData).Run(scan, restoreMaps: new HashSet<string> { Profile });
            Assert.Equal([Profile, V10], report.Versions.Select(v => v.ProfileId));
            Assert.All(report.Versions, v => Assert.True(v.Ok, v.Error));
            Assert.Contains(report.Versions[1].Warnings, w => w.Contains("Apple II v2.1 copy", StringComparison.Ordinal));
            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ImportRunner.ManifestName)))!;
            Assert.Equal(Profile, (string?)manifest["games"]!["phantasie2"]!["tables"]);
            var game = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "GameData", "phantasie2", "game.json")))!;
            Assert.Equal(Profile, (string?)game["profile"]);
            Assert.False(File.Exists(Path.Combine(root, "GameData", "phantasie2", "strings", $"en.own.{V10}.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Every p2-apple2 alternative has a v2.1 twin right after it, with the same text kind.</summary>
    [Fact]
    public void Sources_EveryAppleAlternativeHasAV21Twin()
    {
        foreach (var table in ImportedStrings.Tables)
        {
            var path = Path.Combine(Repo.GameData, table, ImportedStrings.SourcesFile);
            if (!File.Exists(path))
                continue;
            foreach (var (id, alts) in JsonNode.Parse(File.ReadAllBytes(path))!["strings"]!.AsObject())
            {
                var list = alts!.AsArray();
                for (var i = 0; i < list.Count; i++)
                {
                    if ((string?)list[i]!["from"] != V10)
                        continue;
                    var twin = list[i + 1]!;
                    Assert.Equal(Profile, (string?)twin["from"]);
                    Assert.Equal((string?)list[i]!["wording"], (string?)twin["wording"]);
                    Assert.Equal(list[i]!["parts"]!.AsArray().Count, twin["parts"]!.AsArray().Count);
                }
                Assert.Equal(list.Count(a => (string?)a!["from"] == V10), list.Count(a => (string?)a!["from"] == Profile));
                Assert.True((string?)list[0]!["from"] != Profile, id); // the public mask reads the first alternative
            }
        }
    }

    /// <summary>Every file under the import's GameData/phantasie2 and files/, keyed with the profile id as PROFILE.</summary>
    private static Dictionary<string, byte[]> Files(string root, string profile)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var sub in new[] { Path.Combine("GameData", "phantasie2"), Path.Combine("GameData", "phantasie1", "strings"),
                     Path.Combine("GameData", "shared", "strings"), "files" })
        {
            var dir = Path.Combine(root, sub);
            if (!Directory.Exists(dir))
                continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var key = Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/');
                map[key.Replace("." + profile + ".", ".PROFILE.", StringComparison.Ordinal)] = File.ReadAllBytes(f);
            }
        }
        return map;
    }

    /// <summary>Drops what names the version: <c>_source</c> / <c>_note</c> lines, game.json's profile, a region's size.</summary>
    private static JsonNode Normalise(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in new[] { "_source", "_note", "profile", "size" })
                    o.Remove(key);
                foreach (var (_, child) in o)
                    if (child is not null)
                        Normalise(child);
                break;
            case JsonArray a:
                foreach (var child in a)
                    if (child is not null)
                        Normalise(child);
                break;
        }
        return node;
    }
}
