using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P4X68k;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Phantasie IV X68000 Ver.1.02 from the two reference <c>.dim</c> images: every table JSON-equal to the committed
/// GameData, every PNG of the art root pixel-equal, the raw audio files byte-equal.
/// </summary>
public class P4X68kPipelineTests
{
    private const string Images = "reverse-engineering/x68000/original-images/phantasie4/";
    private const string Disk1 = Images + "Phantasie IV (1991)(Starcraft)(Disk 1 of 2).dim";
    private const string Disk2 = Images + "Phantasie IV (1991)(Starcraft)(Disk 2 of 2).dim";
    private const string GameDataDir = "src/OpenPhantasie.Data/GameData/phantasie4/";

    /// <summary>Committed IV GameData files the importer does not write, and why.</summary>
    private static readonly Dictionary<string, string> NotProduced = new()
    {
        ["creation-tables.json"] = "class (a): TABL numbers in Phantasie I's shape, ships",
        ["game.json"] = "class (a): manifest, ships",
        ["rules.json"] = "class (a): rules profile, ships",
        ["graphics/index.json"] = "class (a): hand-written pack index, ships",
        ["graphics/x68000.json"] = "class (a): hand-written pack + x68k_p4_anim.py's numbers, ships",
        ["strings/en.json"] = "class (c): the id-keyed English patch, ships (10-importer.md 4.6); the importer reads it",
    };

    private static PipelineHarness.Result Run() => PipelineHarness.Run("p4-x68000-1.02", Disk1, Disk2);

    [RequiresFilesFact(Disk1, Disk2)]
    public void Tables_EqualCommittedGameData()
    {
        var r = Run();
        var failures = new List<string>();
        var produced = new HashSet<string>();
        foreach (var (key, bytes) in r.Outputs.Where(o => o.Key.StartsWith("GameData/phantasie4/", StringComparison.Ordinal)))
        {
            var rel = key["GameData/phantasie4/".Length..];
            produced.Add(rel);
            var path = GameDataDir + rel;
            if (!File.Exists(Repo.Path(path)))
            {
                failures.Add($"{rel}: no committed file");
                continue;
            }
            var diff = Golden.FirstDifference(JsonNode.Parse(File.ReadAllBytes(Repo.Path(path))), JsonNode.Parse(bytes), "$");
            if (diff is not null)
                failures.Add($"{rel}: {diff}");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));

        var committed = Directory.EnumerateFiles(Repo.Path(GameDataDir), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Repo.Path(GameDataDir), f).Replace('\\', '/')).ToHashSet();
        var missing = committed.Except(produced).Order().ToList();
        Assert.Equal(NotProduced.Keys.Order(), missing);
        Assert.Empty(produced.Except(committed));
        Assert.Equal(16, produced.Count);
    }

    [RequiresFilesFact(Disk1, Disk2)]
    public void Tables_AreByteEqualToThePythonOutput()
    {
        // build_p4_gamedata.py and the font / panel scripts write json.dumps(indent=1, ensure_ascii=False) + "\n".
        var r = Run();
        var differ = r.Outputs.Where(o => o.Key.StartsWith("GameData/phantasie4/", StringComparison.Ordinal))
            .Where(o => !Golden.BytesEqualFile(o.Value, GameDataDir + o.Key["GameData/phantasie4/".Length..]))
            .Select(o => o.Key).ToList();
        Assert.True(differ.Count == 0, "not byte-equal: " + string.Join(", ", differ));
    }

    [RequiresFilesFact(Disk1, Disk2)]
    public void Art_EveryPngPixelEqual_RawFilesByteEqual()
    {
        var r = Run();
        var art = P4X68kPipeline.ArtRoot + "/";
        var failures = new List<string>();
        var pngs = 0;
        foreach (var (key, bytes) in r.Outputs.Where(o => o.Key.StartsWith("files/", StringComparison.Ordinal)))
        {
            var path = key["files/".Length..];
            if (!File.Exists(Repo.Path(path)))
            {
                failures.Add($"{path}: no committed file");
                continue;
            }
            if (path.EndsWith(".png", StringComparison.Ordinal))
            {
                Assert.StartsWith(art, path);
                pngs++;
                var expected = Png.Decode(File.ReadAllBytes(Repo.Path(path)));
                var got = Png.Decode(bytes);
                if (expected.Width != got.Width || expected.Height != got.Height || !expected.PixelEquals(got))
                    failures.Add($"{path}: pixels differ");
            }
            else if (!Golden.BytesEqualFile(bytes, path))
                failures.Add($"{path}: bytes differ");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));

        // Every committed PNG of the pack root has a generator.
        var committed = Directory.EnumerateFiles(Repo.Path(art), "*.png", SearchOption.AllDirectories)
            .Select(f => art + Path.GetRelativePath(Repo.Path(art), f).Replace('\\', '/')).ToList();
        var notMade = committed.Where(p => !r.Outputs.ContainsKey("files/" + p)).ToList();
        Assert.True(notMade.Count == 0, "committed PNGs not produced: " + string.Join(", ", notMade));
        Assert.Equal(committed.Count, pngs);
        foreach (var raw in P4X68kPipeline.RawFiles)
            Assert.True(r.Outputs.ContainsKey($"files/{P4X68kPipeline.RawRoot}/{raw}"), raw);
    }

    /// <summary>
    /// What the public repo ships is enough: with the shipped en.json cut down to the ids whose English differs from
    /// the Japanese (10-importer.md 4.6) and the rule-tables overlay stripped of every value the pipeline reads from the
    /// player's files, the tables come out the same (JSON-equal; rule-tables' key order may differ, the rest byte-equal).
    /// </summary>
    [RequiresFilesFact(Disk1, Disk2)]
    public void PublicOverlays_GiveTheSameTables()
    {
        var full = Run();
        var shipped = Directory.CreateTempSubdirectory("phantasie-p4-shipped-").FullName;
        var staging = Directory.CreateTempSubdirectory("phantasie-p4-staging-").FullName;
        try
        {
            var dir = Path.Combine(shipped, "phantasie4");
            Directory.CreateDirectory(Path.Combine(dir, "strings"));
            // s163: the shipped common fallback (shared / I lines), as the public repo ships it.
            Directory.CreateDirectory(Path.Combine(shipped, "phantasie1", "strings"));
            File.Copy(Repo.Path("src/OpenPhantasie.Data/GameData/phantasie1/strings/fallback.en.json"),
                Path.Combine(shipped, "phantasie1", "strings", "fallback.en.json"));
            var rules = JsonNode.Parse(File.ReadAllBytes(Repo.Path(GameDataDir + "rule-tables.json")))!.AsObject();
            foreach (var path in P4GameData.RuleTablePathsFromCopy)
            {
                var parts = path.Split('.');
                var o = rules;
                foreach (var p in parts[..^1])
                    o = o[p]!.AsObject();
                Assert.True(o.Remove(parts[^1]), path);
            }
            File.WriteAllBytes(Path.Combine(dir, "rule-tables.json"), PyJson.SerializeUtf8(rules, 1));
            var ja = JsonNode.Parse(File.ReadAllBytes(Repo.Path(GameDataDir + "strings/ja.json")))!["strings"]!.AsObject();
            var en = JsonNode.Parse(File.ReadAllBytes(Repo.Path(GameDataDir + "strings/en.json")))!.AsObject();
            var strings = en["strings"]!.AsObject();
            var same = strings.Where(kv => ja[kv.Key] is { } j && (string?)j == (string?)kv.Value).Select(kv => kv.Key).ToList();
            foreach (var k in same)
                strings.Remove(k);
            Assert.True(same.Count > 600, $"only {same.Count} ids equal their Japanese");
            File.WriteAllBytes(Path.Combine(dir, "strings", "en.json"), PyJson.SerializeUtf8(en, 1));

            var scan = ImportRunner.Scan([Repo.Path(Disk1), Repo.Path(Disk2)]);
            var detection = scan.Detections.Single(d => d.Profile.Id == "p4-x68000-1.02");
            var session = new ImportSession { ShippedGameDataRoot = shipped, StagingRoot = staging, Detections = scan.Detections };
            var ctx = new ImportContext(session, detection);
            new P4X68kPipeline().Run(ctx);
            var differ = new List<string>();
            foreach (var (key, bytes) in full.Outputs.Where(o => o.Key.StartsWith("GameData/", StringComparison.Ordinal)))
            {
                if (!ctx.Pending.TryGetValue(key, out var b))
                    differ.Add(key + ": not written");
                else if (key.EndsWith("rule-tables.json", StringComparison.Ordinal))
                {
                    if (Golden.FirstDifference(JsonNode.Parse(bytes), JsonNode.Parse(b), "$") is { } d)
                        differ.Add(d);
                }
                else if (!b.AsSpan().SequenceEqual(bytes))
                    differ.Add(key);
            }
            Assert.True(differ.Count == 0, "differ with the public overlays: " + string.Join("\n", differ));
        }
        finally
        {
            Directory.Delete(shipped, recursive: true);
            Directory.Delete(staging, recursive: true);
        }
    }
}
