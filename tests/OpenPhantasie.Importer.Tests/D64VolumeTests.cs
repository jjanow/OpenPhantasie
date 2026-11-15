using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Containers;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// Commodore 1541 images (s216): <see cref="G64Image"/> decodes C64PP's G64s to the D64 <c>c64_g64.py</c> gives, and
/// <see cref="D64Volume"/> reads every file as <c>extract_c64_d64.py</c> wrote it (the committed v1.4 manifest).
/// </summary>
public class D64VolumeTests
{
    private const string Images = "reverse-engineering/c64/original-images/phantasie1/";
    private const string Manifest = "reverse-engineering/c64/phantasie1/extracted-v1.4/manifest.json";

    [RequiresFilesTheory(Images + "c64pp", Manifest)]
    [InlineData("phantasie_s1[ssi_1985](v1_4).g64")]
    [InlineData("phantasie_s2[ssi_1985](v1_4).g64")]
    public void G64_EveryFileEqualsThePythonExtract(string image)
    {
        var data = File.ReadAllBytes(Repo.Path(Images + "c64pp/" + image));
        var decoded = G64Image.Decode(data);
        Assert.Empty(decoded.Missing);
        var vol = D64Volume.TryOpen(decoded.Image);
        Assert.NotNull(vol);
        var expected = JsonNode.Parse(File.ReadAllBytes(Repo.Path(Manifest)))![image]!["files"]!.AsArray();
        Assert.Equal(expected.Count, vol.Files.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal((string)expected[i]!["name"]!, vol.Files[i].Name);
            Assert.Equal((string)expected[i]!["type"]!, vol.Files[i].Type);
            Assert.Equal((string)expected[i]!["sha1"]!, Hashing.Sha1(vol.Read(vol.Files[i])));
            if (expected[i]!["record_length"] is { } rl)
                Assert.Equal((int)rl, vol.Files[i].RecordLength);
        }
    }

    /// <summary>Every C64 Phantasie I image held opens: the en / de D64s and C64PP's G64s, all sectors decoded.</summary>
    [RequiresFilesFact(Images + "c64pp")]
    public void EveryHeldImage_Opens()
    {
        foreach (var path in Directory.EnumerateFiles(Repo.Path(Images), "*.*", SearchOption.AllDirectories)
                     .Where(p => p.EndsWith(".d64", StringComparison.Ordinal) || p.EndsWith(".g64", StringComparison.Ordinal)))
        {
            var data = File.ReadAllBytes(path);
            var image = data;
            if (G64Image.IsG64(data))
            {
                var g = G64Image.Decode(data);
                Assert.True(g.Missing.Count == 0, $"{path}: {g.Missing.Count} sectors missing");
                image = g.Image;
            }
            var vol = D64Volume.TryOpen(image);
            Assert.True(vol is not null, path);
            Assert.True(vol.Files.Count > 100, $"{path}: {vol.Files.Count} files");
            foreach (var f in vol.Files)
                Assert.NotNull(vol.Read(f));
        }
    }

    [RequiresFilesFact(Images + "c64pp/phantasie_s1[ssi_1985](v1_4).g64")]
    public void Scanner_OpensG64AsAContainer()
    {
        var notes = new List<string>();
        var cs = SourceScanner.Scan([Repo.Path(Images + "c64pp/phantasie_s1[ssi_1985](v1_4).g64")], notes);
        var c = Assert.Single(cs);
        Assert.Equal("g64", c.Kind);
        Assert.Empty(notes);
        Assert.Contains(c.Files, f => f.Path == "TWN 99");
        Assert.Equal(8100, c.Files.Single(f => f.Path == "MN").Length);
    }

    [Fact]
    public void NotAD64_ReturnsNull()
    {
        Assert.Null(D64Volume.TryOpen(new byte[D64Volume.ImageSize]));
        Assert.Null(D64Volume.TryOpen(new byte[1000]));
        Assert.False(G64Image.IsG64(new byte[100]));
    }
}
