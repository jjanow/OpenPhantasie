namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Golden tests of the I Amiga pipeline (<c>p1-amiga-1.0</c> + the Kickstart 1.3 ROM for topaz 8) on the reference ADF:
/// every file under the committed pack root is produced and pixel / JSON equal, the GameData files JSON (and byte)
/// equal, the raw audio files byte equal.
/// </summary>
public class P1AmigaPipelineTests
{
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf";
    private const string Kickstart = "reverse-engineering/amiga/original-images/kickstart";
    private const string Root = "reverse-engineering/amiga/extracted";

    private static readonly string[] GameDataFiles =
    [
        "phantasie1/asset-manifest.json",
        "phantasie1/graphics/fonts/amiga-z6font-6.json",
        "phantasie1/graphics/fonts/amiga-topaz-8.json",
        "phantasie3/graphics/fonts/amiga-topaz-8.json",
    ];

    /// <summary>The I Amiga pipeline plus the Kickstart one (topaz 8), as one import run of both gives them.</summary>
    private static PipelineHarness.Result Full()
    {
        var amiga = PipelineHarness.Run("p1-amiga-1.0", Adf, Kickstart);
        var rom = PipelineHarness.Run("kickstart-1.3", Adf, Kickstart);
        var all = new Dictionary<string, byte[]>(amiga.Outputs);
        foreach (var (k, v) in rom.Outputs)
            all.Add(k, v);
        return amiga with { AllOutputs = all, Warnings = [.. amiga.Warnings, .. rom.Warnings] };
    }

    private static byte[] Output(PipelineHarness.Result r, string key)
    {
        Assert.True(r.Outputs.TryGetValue(key, out var bytes), $"not produced: {key}");
        return bytes!;
    }

    [RequiresFilesFact(Adf, Kickstart, Root)]
    public void ProducesEveryCommittedArtFile_PixelAndJsonEqual()
    {
        var r = Full();
        Assert.Empty(r.Warnings);
        var committed = Directory.EnumerateFiles(Repo.Path(Root), "*", SearchOption.AllDirectories)
            .Select(p => Root + "/" + Path.GetRelativePath(Repo.Path(Root), p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToList();
        Assert.Equal(261, committed.Count);
        var produced = r.Outputs.Keys.Where(k => k.StartsWith("files/" + Root + "/", StringComparison.Ordinal))
            .Select(k => k["files/".Length..]).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(committed, produced);
        foreach (var rel in committed)
        {
            var bytes = Output(r, "files/" + rel);
            if (rel.EndsWith(".png", StringComparison.Ordinal))
                Golden.PngEqualsFile(bytes, rel);
            else
            {
                Golden.JsonEqualsFile(bytes, rel);
                Assert.True(Golden.BytesEqualFile(bytes, rel), $"{rel}: JSON equal but not byte equal");
            }
        }
    }

    [RequiresFilesFact(Adf, Kickstart)]
    public void GameDataFiles_JsonAndByteEqual()
    {
        var r = Full();
        foreach (var f in GameDataFiles)
        {
            var bytes = Output(r, "GameData/" + f);
            Golden.JsonEqualsFile(bytes, "src/OpenPhantasie.Data/GameData/" + f);
        }
        // The font files are the script's own output; the asset manifest's map_tiles part is hand-formatted.
        foreach (var f in GameDataFiles.Where(f => f.Contains("/fonts/")))
            Assert.True(Golden.BytesEqualFile(Output(r, "GameData/" + f), "src/OpenPhantasie.Data/GameData/" + f), f);
        // s201: alone (no DOS I), the Amiga also writes I's eleven tables (P1AmigaTablesTests).
        Assert.Equal(GameDataFiles.Length + P1AmigaTablesTests.Tables.Length,
            r.Outputs.Keys.Count(k => k.StartsWith("GameData/", StringComparison.Ordinal)));
    }

    [RequiresFilesFact(Adf, Kickstart)]
    public void RawAudioFiles_ByteEqual()
    {
        var r = Full();
        foreach (var f in new[] { "reverse-engineering/amiga/unpacked/PHANT/tamb.dat", "reverse-engineering/amiga/unpacked/PHANT/game" })
            Assert.True(Golden.BytesEqualFile(Output(r, "files/" + f), f), f);
    }

    [RequiresFilesFact(Adf, Kickstart)]
    public void WithoutKickstart_SkipsTopazOnly_TheReportOffersTheRom()
    {
        // s160: no pipeline warning; ImportRunner's report offers the ROM (ImportFlowTests.AmigaWithoutTheRom_*).
        var r = PipelineHarness.Run("p1-amiga-1.0", Adf);
        Assert.Empty(r.Warnings);
        Assert.DoesNotContain(r.Outputs.Keys, k => k.Contains("topaz", StringComparison.Ordinal));
        Assert.Contains("GameData/phantasie1/graphics/fonts/amiga-z6font-6.json", r.Outputs.Keys);
        Assert.Contains("files/" + Root + "/maps/maps.json", r.Outputs.Keys);
        Assert.Equal(Full().Outputs.Count - 3, r.Outputs.Count);
    }
}
