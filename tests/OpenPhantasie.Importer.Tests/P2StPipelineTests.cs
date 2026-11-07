using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Golden tests of the Phantasie II Atari ST pipeline (<c>p2-st-1.0</c>): on the reference Pasti images every output
/// equals the committed file (GameData JSON, the art root's PNGs and JSON, the raw program files), and the TOSEC
/// <c>.st</c> pair gives the same output byte for byte.
/// </summary>
public class P2StPipelineTests
{
    private const string Profile = "p2-st-1.0";
    private const string Stx1 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx";
    private const string Stx2 = "reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk2.stx";
    private const string Tosec1 = "reverse-engineering/atari-st/original-images/phantasie2/tosec-st/phantasie2-v1.0-tosec-disk1.st";
    private const string Tosec2 = "reverse-engineering/atari-st/original-images/phantasie2/tosec-st/phantasie2-v1.0-tosec-disk2.st";
    private const string GameData = "src/OpenPhantasie.Data/GameData/phantasie2";
    private const string ArtRoot = "reverse-engineering/atari-st/phantasie2/extracted/graphics";

    /// <summary>Committed GameData/phantasie2 files the pipeline does not write, and why.</summary>
    private static readonly Dictionary<string, string> NotProduced = new()
    {
        ["rules.json"] = "class (a), shipped: edited after build_p2_gamedata.py (124914e6 FFP training, 3dff71c5 D2-042), " +
                         "the script's constants are behind it",
        ["travel-time.json"] = "class (a), shipped: 3dff71c5 (D2-042 wilderness_inn_dos) edited it after the script",
        ["strings/en.json"] = "hand-written string table (not this pipeline)",
        ["strings/en.sources.json"] = "where the table's original lines sit in each copy (build_string_sources.py), ships",
        ["graphics/index.json"] = "class (a), hand-written pack list",
        ["graphics/apple2.json"] = "the Apple II pack: p2-apple2's output (P2Apple2PipelineTests)",
    };

    private static readonly string[] RawFiles =
    [
        "reverse-engineering/atari-st/phantasie2/unpacked/disk1/START.PRG",
        "reverse-engineering/atari-st/phantasie2/unpacked/disk1/PHANT.PRG",
    ];

    private static PipelineHarness.Result Pasti() => PipelineHarness.Run(Profile, Stx1, Stx2);

    [RequiresFilesFact(Stx1, Stx2, GameData)]
    public void GameData_EqualsCommitted()
    {
        var outputs = Pasti().Outputs;
        var dir = Repo.Path(GameData);
        var checkedFiles = 0;
        var notByteEqual = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var key = $"GameData/phantasie2/{rel}";
            if (!outputs.TryGetValue(key, out var bytes))
            {
                Assert.True(NotProduced.ContainsKey(rel), $"{rel}: committed but not produced");
                continue;
            }
            Assert.False(NotProduced.ContainsKey(rel), $"{rel}: listed as not produced");
            Golden.JsonEqualsFile(bytes, $"{GameData}/{rel}");
            if (!Golden.BytesEqualFile(bytes, $"{GameData}/{rel}"))
                notByteEqual.Add(rel);
            checkedFiles++;
        }
        Assert.Equal(15, checkedFiles);
        Assert.Empty(notByteEqual);
    }

    [RequiresFilesFact(Stx1, Stx2, ArtRoot)]
    public void Art_EqualsCommitted()
    {
        var outputs = Pasti().Outputs;
        var dir = Repo.Path(ArtRoot);
        int png = 0, json = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = $"{ArtRoot}/{Path.GetRelativePath(dir, file).Replace('\\', '/')}";
            Assert.True(outputs.TryGetValue("files/" + rel, out var bytes), $"{rel}: committed but not produced");
            if (rel.EndsWith(".png", StringComparison.Ordinal))
            {
                Golden.PngEqualsFile(bytes!, rel);
                png++;
            }
            else
            {
                Golden.JsonEqualsFile(bytes!, rel);
                Assert.True(Golden.BytesEqualFile(bytes!, rel), $"{rel}: JSON-equal but not byte-equal");
                json++;
            }
        }
        Assert.Equal(262, png);
        Assert.Equal(4, json);
    }

    [RequiresFilesFact(Stx1, Stx2, "reverse-engineering/atari-st/phantasie2/unpacked/disk1/PHANT.PRG")]
    public void RawFiles_EqualCommitted()
    {
        var outputs = Pasti().Outputs;
        foreach (var rel in RawFiles)
            Assert.True(Golden.BytesEqualFile(outputs["files/" + rel], rel), $"{rel} differs");
    }

    [RequiresFilesFact(Stx1, Stx2, GameData, ArtRoot)]
    public void EveryOutput_HasACommittedCounterpart()
    {
        foreach (var key in Pasti().Outputs.Keys)
        {
            var rel = key.StartsWith("files/", StringComparison.Ordinal)
                ? key["files/".Length..]
                : "src/OpenPhantasie.Data/" + key;
            Assert.True(File.Exists(Repo.Path(rel)), $"{key}: no committed file at {rel}");
        }
    }

    /// <summary>The pipeline itself warns about nothing (the played-copy warning is the runner's, from mutable_files).</summary>
    [RequiresFilesFact(Stx1, Stx2)]
    public void Pasti_PipelineAddsNoWarnings() => Assert.Empty(Pasti().Warnings);

    /// <summary>Every <c>file</c> the produced pack names (resolved against its root) is an output of the import.</summary>
    [RequiresFilesFact(Stx1, Stx2)]
    public void PackFiles_AreAllProduced()
    {
        var outputs = Pasti().Outputs;
        var pack = JsonNode.Parse(outputs["GameData/phantasie2/graphics/atari-st.json"])!.AsObject();
        var root = (string)pack["root"]!;
        var files = new List<string>();
        void Walk(JsonNode? n)
        {
            if (n is JsonObject o)
                foreach (var (k, v) in o)
                {
                    if (k == "file" && v is JsonValue jv && jv.TryGetValue(out string? f))
                        files.Add(f);
                    Walk(v);
                }
            else if (n is JsonArray a)
                foreach (var v in a)
                    Walk(v);
        }
        Walk(pack);
        Assert.Equal(3, files.Count);
        foreach (var f in files)
        {
            var parts = new List<string>(root.Split('/'));
            foreach (var seg in f.Split('/'))
                if (seg == "..")
                    parts.RemoveAt(parts.Count - 1);
                else
                    parts.Add(seg);
            var key = "files/" + string.Join('/', parts);
            Assert.True(outputs.ContainsKey(key), $"pack file {f}: {key} not produced");
        }
    }

    /// <summary>The pack takes only our constants from the shipped atari-st.json: the disk-derived values are rebuilt.</summary>
    [RequiresFilesFact(Stx1, Stx2, GameData)]
    public void Pack_RebuildsTheDiskValues()
    {
        var path = $"{GameData}/graphics/atari-st.json";
        var shipped = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(Repo.Path(path)))!.AsObject();
        shipped["root"] = "x";
        shipped["palettes"] = null;
        shipped["colour_cycle"]!["registers"] = null;
        shipped["town_screen"]!["name_colours"] = null;
        foreach (var k in new[] { "title.cover", "title.credits", "town.backdrop", "ui.decor", "ending.final" })
            shipped["pictures"]![k] = null;
        shipped["ending_scene"]!["palette"] = null;
        shipped["ending_scene"]!["intro_page"]!["palette"] = null;
        // s158: the alert boxes are the disks' form_alert strings: blanked here, rebuilt from PHANT.RSC / OUT.RSC / PHANT.PRG.
        var alerts = shipped["field_sounds"]!["alerts"]!;
        foreach (var line in alerts["lines"]!.AsArray())
            if (line!.AsObject().ContainsKey("box"))
                line["box"] = "";
        alerts["cue_boxes"] = new JsonObject();
        var index = System.Text.Json.Nodes.JsonNode.Parse(Pasti().Outputs[$"files/{ArtRoot}/extraction-index.json"])!.AsObject();
        var src = new Pipelines.P2St.P2StSource(Pasti().Detection.Files!);
        var pack = Pipelines.P2St.P2StManifest.Pack(index, shipped, src);
        Golden.JsonEqualsFile(Formats.PyJson.SerializeUtf8(pack, 2), path);
    }

    [RequiresFilesFact(Stx1, Stx2, Tosec1, Tosec2)]
    public void Tosec_GivesTheSameOutput()
    {
        var pasti = Pasti().Outputs;
        var tosec = PipelineHarness.Run(Profile, Tosec1, Tosec2).Outputs;
        Assert.Equal(pasti.Keys.Order(StringComparer.Ordinal), tosec.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, bytes) in pasti)
            Assert.True(bytes.AsSpan().SequenceEqual(tosec[key]), $"{key}: TOSEC output differs");
    }
}
