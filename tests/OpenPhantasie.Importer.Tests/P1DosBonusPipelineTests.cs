using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Pipelines.P1Dos;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// I's WizardWorks Bonus Edition (the Memorial Set's I, and Bonus v1.1's disk): its LZEXE-packed PHANTASI.EXE
/// unpacks to 1987's program image (one byte apart), and its pipeline writes exactly what the 1987 pipeline writes
/// (<c>reverse-engineering/dos/bonus-edition.md</c>).
/// </summary>
public class P1DosBonusPipelineTests
{
    private const string Dos1987 = "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima";
    private const string Memorial = "reverse-engineering/dos/original-images/phantasie1/memorial-set-gog";
    private const string BonusIma = "reverse-engineering/dos/original-images/phantasie1/bonus-v1.1-1991-wizardworks";
    private const string Profile = "p1-dos-bonus-1991";

    [RequiresFilesFact(Memorial + "/GAME/PH1/PHANTASI.EXE", "reverse-engineering/dos/original-files/PHANTASI.COM")]
    public void Exe_UnpacksTo1987sImage_ExceptTheHiddenSaveFlag()
    {
        var exe = File.ReadAllBytes(Repo.Path(Memorial + "/GAME/PH1/PHANTASI.EXE"));
        var com = File.ReadAllBytes(Repo.Path("reverse-engineering/dos/original-files/PHANTASI.COM"));
        Assert.True(LzExe.Is(exe));
        Assert.False(LzExe.Is(com));
        var bonus = ComImage.UnpackAny(exe);
        var original = ComImage.UnpackAny(com);
        Assert.Equal(original.Length, bonus.Length);
        var diffs = Enumerable.Range(0, bonus.Length).Where(i => bonus[i] != original[i]).ToList();
        const int flag = 0x0C9F * 16 + 0x3094; // DS:0x3094: 1987 creates saves hidden (attribute 0x22), Bonus not
        Assert.Equal([flag], diffs);
        Assert.Equal(1, original[flag]);
        Assert.Equal(0, bonus[flag]);
    }

    [RequiresFilesTheory(Dos1987, Memorial)]
    [InlineData(Memorial)]
    [InlineData(BonusIma)]
    public void WritesWhatThe1987PipelineWrites(string source)
    {
        var bonus = PipelineHarness.Run(Profile, source);
        var original = PipelineHarness.Run("p1-dos-1987", Dos1987);
        Assert.Empty(bonus.Warnings);
        Assert.Empty(bonus.Detection.PlayedFiles);
        Assert.Equal(original.Outputs.Keys.OrderBy(k => k, StringComparer.Ordinal),
            bonus.Outputs.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var (key, bytes) in original.Outputs)
            Assert.True(bytes.AsSpan().SequenceEqual(bonus.Outputs[key]), $"{key} differs");
        // The games' own lines: the same text, under the Bonus profile's file name.
        var strings = original.AllOutputs.Keys.Where(k => !original.Outputs.ContainsKey(k)).ToList();
        Assert.NotEmpty(strings);
        foreach (var key in strings)
        {
            var renamed = key.Replace(ImportedStrings.OutputFile("p1-dos-1987"), ImportedStrings.OutputFile(Profile));
            Assert.True(bonus.AllOutputs.TryGetValue(renamed, out var got), $"{renamed} missing");
            var a = System.Text.Json.Nodes.JsonNode.Parse(original.AllOutputs[key])!["strings"]!.ToJsonString();
            var b = System.Text.Json.Nodes.JsonNode.Parse(got)!["strings"]!.ToJsonString();
            Assert.Equal(a, b);
        }
    }
}
