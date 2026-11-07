using System.Security.Cryptography;
using OpenPhantasie.Importer.Pipelines.P3Amiga;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// <c>p3-amiga-1.0</c> (<see cref="P3AmigaPipeline"/>) against the committed Amiga III art pack, which
/// <c>extract_amiga_p3_graphics.py</c> reproduces byte for byte (checked s158): every PNG pixel-equal, the JSON files
/// JSON-equal, the raw audio files byte-equal.
/// </summary>
public class P3AmigaPipelineTests
{
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf";
    private const string DosIma = "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima";
    private const string Root = "reverse-engineering/amiga/phantasie3/extracted/graphics";
    private const string Unpacked = "reverse-engineering/amiga/phantasie3/unpacked/PhantasieIII";

    /// <summary>Committed files under the root the pipeline does not write, with the reason (none today).</summary>
    private static readonly Dictionary<string, string> NotProduced = new();

    /// <summary>Written only with DOS III in the same run.</summary>
    private static readonly string[] NeedsDos = ["previews/scandor-dos.png"];

    private static string Key(string repoRelative) => "files/" + repoRelative;

    private static IEnumerable<string> CommittedUnderRoot() =>
        Directory.EnumerateFiles(Repo.Path(Root), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Repo.Path(Root), f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal);

    [RequiresFilesFact(Adf, DosIma, Root)]
    public void WithDos_MatchesCommittedPack()
    {
        var r = PipelineHarness.Run("p3-amiga-1.0", Adf, DosIma);
        Assert.Empty(r.Warnings);
        var committed = CommittedUnderRoot().ToList();
        var pngs = 0;
        var missing = new List<string>();
        foreach (var rel in committed)
        {
            if (!r.Outputs.TryGetValue(Key($"{Root}/{rel}"), out var bytes))
            {
                if (!NotProduced.ContainsKey(rel))
                    missing.Add(rel);
                continue;
            }
            if (rel.EndsWith(".png", StringComparison.Ordinal))
            {
                Golden.PngEqualsFile(bytes, $"{Root}/{rel}");
                pngs++;
            }
            else
            {
                Golden.JsonEqualsFile(bytes, $"{Root}/{rel}");
                Assert.True(Golden.BytesEqualFile(bytes, $"{Root}/{rel}"), $"{rel}: JSON-equal but not byte-equal");
            }
        }
        Assert.True(missing.Count == 0, "not produced: " + string.Join(", ", missing));
        Assert.Equal(579, pngs);

        var extra = r.Outputs.Keys.Where(k => k.StartsWith(Key(Root + "/"), StringComparison.Ordinal))
            .Select(k => k[Key(Root + "/").Length..]).Except(committed).ToList();
        Assert.True(extra.Count == 0, "not committed: " + string.Join(", ", extra));

        foreach (var raw in new[] { "D/m0", "D/m1", "PhantasieIII" })
            Assert.True(Golden.BytesEqualFile(r.Outputs[Key($"{Unpacked}/{raw}")], $"{Unpacked}/{raw}"), raw);
        // Nothing outside the pack root and the three raw files (the script writes no GameData).
        Assert.Equal(committed.Count + 3, r.Outputs.Count);
    }

    [RequiresFilesFact(Adf, Root)]
    public void AmigaAlone_SkipsOnlyTheDosScandorPreview()
    {
        var r = PipelineHarness.Run("p3-amiga-1.0", Adf);
        var warning = Assert.Single(r.Warnings);
        Assert.Contains("WORLD.DAT", warning);
        foreach (var rel in NeedsDos)
            Assert.False(r.Outputs.ContainsKey(Key($"{Root}/{rel}")), rel);
        // Everything else is the same; the plane previews come from the Amiga's D/pln1, D/pln2, D/pln4.
        foreach (var rel in CommittedUnderRoot().Where(f => f.EndsWith(".png", StringComparison.Ordinal) && !NeedsDos.Contains(f)))
            Golden.PngEqualsFile(r.Outputs[Key($"{Root}/{rel}")], $"{Root}/{rel}");
        var index = System.Text.Json.Nodes.JsonNode.Parse(r.Outputs[Key($"{Root}/extraction-index.json")])!;
        Assert.Empty(index["scandor_dos_vs_amiga"]!["cells"]!.AsArray());
    }

    [RequiresFilesFact(Adf)]
    public void HunkLoader_MatchesThePythonFlatImage()
    {
        var r = PipelineHarness.Run("p3-amiga-1.0", Adf);
        var program = r.Detection.Files!.Read("PhantasieIII");
        var hunks = AmigaHunks.Parse(program);
        Assert.Equal(["CODE", "DATA", "BSS"], hunks.Select(h => h.Type));
        var (image, bases) = AmigaHunks.LoadFlat(program);
        Assert.Equal([0, 0x21890, 0x283a0], bases);
        // extract_amiga_p3_common.py prints this sha1 for its flat image.
        Assert.Equal("43b87d7ae91cb8d10a3d7749232c59b1f2d53664", Convert.ToHexStringLower(SHA1.HashData(image)));
    }
}
