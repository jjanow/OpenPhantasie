using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P3Dos;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Golden tests for <see cref="P3DosPipeline"/> (Phantasie III DOS): every output JSON-equal to the committed
/// <c>GameData/phantasie3</c> file; the extract stage JSON-equal to <c>reverse-engineering/dos/phantasie3/extracted/</c>.
/// Nine outputs are also byte-identical to the committed files; rule-tables.json is JSON-equal only, because the blocks
/// added to it by hand (creation_age, armory_restock …) were written with inline number lists, not json.dumps(indent=1).
/// </summary>
public class P3DosPipelineTests
{
    private const string Image = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string WizardWorks =
        "reverse-engineering/dos/original-images/phantasie3/wizardworks-1991/phantasie3-1991-wizardworks-360k.ima";
    private const string Extracted = "reverse-engineering/dos/phantasie3/extracted/";

    private static readonly string[] Files =
    [
        "dungeons.json", "elementals.json", "items.json", "monsters.json", "names.json", "overworld.json",
        "rule-tables.json", "scrolls.json", "spells.json", "towns.json",
    ];

    public static readonly TheoryData<string> Outputs = new(Files);

    private static PipelineHarness.Result Run() => PipelineHarness.Run("p3-dos-1988", Image);

    [RequiresFilesFact(Image)]
    public void WritesExactlyTheTenTables()
    {
        var keys = Run().Outputs.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(Files.Select(o => "GameData/phantasie3/" + o).OrderBy(k => k, StringComparer.Ordinal), keys);
    }

    [RequiresFilesTheory(Image)]
    [MemberData(nameof(Outputs))]
    public void OutputEqualsCommittedGameData(string file)
    {
        var bytes = Run().Outputs["GameData/phantasie3/" + file];
        Golden.JsonEqualsFile(bytes, "src/OpenPhantasie.Data/GameData/phantasie3/" + file);
        if (file != "rule-tables.json")
            Assert.True(Golden.BytesEqualFile(bytes, "src/OpenPhantasie.Data/GameData/phantasie3/" + file),
                $"{file}: JSON-equal but not byte-identical");
    }

    [RequiresFilesFact(Image, Extracted + "items.json")]
    public void ExtractStageEqualsPythonExtracts()
    {
        var x = new P3Extract(Run().Detection.Files!);
        var stage = new Dictionary<string, JsonObject>
        {
            ["strings.json"] = x.StringsJson(), ["names.json"] = x.Names(), ["items.json"] = x.Items(),
            ["spells.json"] = x.Spells(), ["monsters.json"] = x.Monsters(), ["dungeons.json"] = x.Dungeons(),
            ["rule-tables.json"] = x.RuleTables(), ["scrolls.json"] = x.Scrolls(), ["towns.json"] = x.Towns(),
            ["world.json"] = x.World(),
        };
        foreach (var (file, node) in stage)
            Golden.JsonEqualsFile(Formats.PyJson.SerializeUtf8(node, 1), Extracted + file);
    }

    [RequiresFilesFact(WizardWorks)]
    public void WizardWorks1991IsDetectedAsTheSameBuildAndImportsTheSame()
    {
        var ww = PipelineHarness.Run("p3-dos-1988", WizardWorks);
        Assert.Empty(ww.Detection.PlayedFiles);
        if (!Repo.Has(Image))
            return;
        var reference = Run();
        Assert.Equal(reference.Outputs.Keys.Order(), ww.Outputs.Keys.Order());
        foreach (var (key, bytes) in reference.Outputs)
            Assert.True(bytes.AsSpan().SequenceEqual(ww.Outputs[key]), $"{key} differs between 1988 and WizardWorks 1991");
    }

    [RequiresFilesFact("src/OpenPhantasie.Data/GameData/phantasie1/spells.json", Extracted + "spells.json")]
    public void SameNameAsIEqualsTheBuildsNameMatch()
    {
        // build_p3_gamedata.py matched III spells to I's by name (spaces, then also dots, ignored); the port keeps
        // that result as a fixed set so the import never reads I's names.
        var p1 = JsonNode.Parse(File.ReadAllBytes(Repo.Path("src/OpenPhantasie.Data/GameData/phantasie1/spells.json")))!["spells"]!
            .AsArray().Select((s, i) => (Key: ((string)s!["name"]!).Replace(" ", "").ToUpperInvariant(), Index: i))
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Index);
        var p3 = JsonNode.Parse(File.ReadAllBytes(Repo.Path(Extracted + "spells.json")))!["spells"]!.AsArray();
        var matched = new HashSet<int>();
        foreach (var s in p3)
        {
            var n = (int)s!["number"]!;
            var name = ((string)s["name"]!).Replace(" ", "").ToUpperInvariant();
            if (p1.TryGetValue(name.Replace(".", ""), out var i) || p1.TryGetValue(name, out i))
            {
                Assert.Equal(n - 1, i);
                matched.Add(n);
            }
        }
        Assert.True(matched.SetEquals(P3Build.SameNameAsI), string.Join(",", matched.Order()));
    }
}
