using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// I Amiga's played-copy check (10-importer.md 3.3). All eighteen OUT files have pristine sha1s: eleven from the
/// reference dump's never-rewritten files (s163), the other seven from the unplayed data files of the WHDLoad install
/// package (reverse-engineering/amiga/original-images/SOURCES.txt). The reference dump was played: its OUT9 / 10 / 11 /
/// 13 / 14 have fog lifted, while its OUT1 / OUT5 (no fog and no fixed fight to change) are byte-identical to the
/// pristine ones. A played copy is reported with a warning that the import is unchanged: s201, its own tables take the
/// grids from maps.int (never rewritten) and only the feature records and text pages, which play leaves, from the
/// region files (amiga/GAME-TABLES.md section 3).
/// </summary>
public class P1AmigaPlayedCopyTests
{
    private const string Unpacked = "reverse-engineering/amiga/unpacked/PHANT";
    private const string Adf = "reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf";

    /// <summary>The reference dump (the ADF and its unpacked folder) is found, with exactly its five played regions.</summary>
    [RequiresFilesTheory(Unpacked, Adf)]
    [InlineData(Adf)]
    [InlineData(Unpacked)]
    public void ReferenceDump_IsReady_WithItsPlayedRegions(string source)
    {
        var reference = ImportRunner.Scan([Repo.Path(source)]).Detections.Single(d => d.Profile.Id == "p1-amiga-1.0");
        Assert.Equal(DetectionStatus.Ready, reference.Status);
        Assert.Equal(18, reference.Profile.MutableFiles.Count);
        Assert.Equal(["out9.dat", "out10.dat", "out11.dat", "out13.dat", "out14.dat"], reference.PlayedFiles);
        var warning = ImportRunner.PlayedWarning(reference);
        Assert.Contains("same as from a fresh copy", warning);
        Assert.DoesNotContain("fog", warning);
    }

    [RequiresFilesFact(Unpacked)]
    public void AChangedRegionWithoutFog_IsFlagged()
    {
        var dir = Directory.CreateTempSubdirectory("phantasie-p1a-played-").FullName;
        try
        {
            foreach (var f in Directory.EnumerateFiles(Repo.Path(Unpacked)))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            // OUT1 (no fog, no fixed fight) had no pristine sha1 before; a changed byte must now be reported.
            var out1 = Path.Combine(dir, "OUT1.DAT");
            var bytes = File.ReadAllBytes(out1);
            bytes[0] ^= 1;
            File.WriteAllBytes(out1, bytes);
            var played = ImportRunner.Scan([dir]).Detections.Single(d => d.Profile.Id == "p1-amiga-1.0");
            Assert.Equal(["out1.dat", "out9.dat", "out10.dat", "out11.dat", "out13.dat", "out14.dat"], played.PlayedFiles);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
