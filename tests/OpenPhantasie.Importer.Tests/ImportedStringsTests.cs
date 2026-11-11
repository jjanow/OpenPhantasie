using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The games' own lines of the string tables (10-importer.md 7.6): what each version's pipeline resolves from the
/// reference images through <c>strings/en.sources.json</c> must equal the committed (private) <c>en.json</c> exactly,
/// and every id a version is listed for must resolve.
/// </summary>
public class ImportedStringsTests
{
    private const string GameData = "src/OpenPhantasie.Data/GameData/";
    private const string P1Dos = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string P1Amiga = "reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf";
    private const string P2Stx1 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx";
    private const string P2Stx2 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk2.stx";
    private const string P3Dos = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string P3Amiga = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";
    private const string P4Images = "reverse-engineering/x68000/original-images/phantasie4/";
    private const string P4Disk1 = P4Images + "Phantasie IV (1991)(Starcraft)(Disk 1 of 2).dim";
    private const string P4Disk2 = P4Images + "Phantasie IV (1991)(Starcraft)(Disk 2 of 2).dim";

    [RequiresFilesFact(P1Dos)]
    public void P1Dos_ResolvesEveryListedLine() => Check("p1-dos-1987", PipelineHarness.Run("p1-dos-1987", P1Dos));

    [RequiresFilesFact(P1Amiga)]
    public void P1Amiga_ResolvesEveryListedLine() => Check("p1-amiga-1.0", PipelineHarness.Run("p1-amiga-1.0", P1Amiga));

    [RequiresFilesFact(P2Stx1, P2Stx2)]
    public void P2St_ResolvesEveryListedLine() => Check("p2-st-1.0", PipelineHarness.Run("p2-st-1.0", P2Stx1, P2Stx2));

    [RequiresFilesFact(P3Dos)]
    public void P3Dos_ResolvesEveryListedLine() => Check("p3-dos-1988", PipelineHarness.Run("p3-dos-1988", P3Dos));

    [RequiresFilesFact(P3Amiga)]
    public void P3Amiga_ResolvesEveryListedLine() => Check("p3-amiga-1.0", PipelineHarness.Run("p3-amiga-1.0", P3Amiga));

    [RequiresFilesFact(P4Disk1, P4Disk2)]
    public void P4_ResolvesSharedLinesAndTheGamesOwnEnglish()
    {
        var r = PipelineHarness.Run("p4-x68000-1.02", P4Disk1, P4Disk2);
        Check("p4-x68000-1.02", r);
        // IV: the patch (ids whose English differs from the Japanese) + the imported ids = the whole committed en.json.
        var en = Strings(GameData + "phantasie4/strings/en.json");
        var ja = Strings(GameData + "phantasie4/strings/ja.json");
        var imported = Imported(r, "phantasie4", "p4-x68000-1.02");
        var patch = en.Where(kv => !ja.TryGetValue(kv.Key, out var j) || j != kv.Value).Select(kv => kv.Key).ToHashSet();
        Assert.Equal(en.Count - patch.Count, imported.Count);
        foreach (var (id, text) in imported)
        {
            Assert.DoesNotContain(id, patch);
            Assert.Equal(en[id], text);
        }
    }

    private static void Check(string profile, PipelineHarness.Result r)
    {
        var failures = new List<string>();
        foreach (var table in ImportedStrings.Tables)
        {
            var sources = JsonNode.Parse(File.ReadAllBytes(Repo.Path(GameData + table + "/" + ImportedStrings.SourcesFile)))!["strings"]!.AsObject();
            var expected = Strings(GameData + table + "/strings/en.json");
            var got = Imported(r, table, profile);
            // Lines in the version's own wording ("wording": "own", s201 I Amiga's Zeus speech) go to its own-wording
            // file, not this one, and differ from en.json by design: checked by the version's own tests.
            var listed = sources.Where(kv => kv.Value!.AsArray().Any(a => a!["from"]!.GetValue<string>() == profile
                                                                          && a["wording"] is null))
                .Select(kv => kv.Key).ToHashSet();
            foreach (var id in listed)
                if (!got.TryGetValue(id, out var text))
                    failures.Add($"{table} {id}: not resolved");
                else if (text != expected[id])
                    failures.Add($"{table} {id}: '{text}' != '{expected[id]}'");
            foreach (var id in got.Keys.Where(k => !listed.Contains(k) && table != "phantasie4"))
                failures.Add($"{table} {id}: resolved but not listed");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    private static Dictionary<string, string> Imported(PipelineHarness.Result r, string table, string profile) =>
        r.AllOutputs.TryGetValue($"GameData/{table}/{ImportedStrings.OutputFile(profile)}", out var bytes)
            ? JsonNode.Parse(bytes)!["strings"]!.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>())
            : [];

    private static Dictionary<string, string> Strings(string repoRelative) =>
        JsonNode.Parse(File.ReadAllBytes(Repo.Path(repoRelative)))!["strings"]!.AsObject()
            .Where(kv => kv.Value is JsonValue)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>());
}
