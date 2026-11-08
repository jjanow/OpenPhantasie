using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s163: I Amiga's played-copy check (10-importer.md 3.3). The reference dump was played (OUT1 / 5 / 9 written in 1992,
/// OUT10 / 11 / 13 / 14 written by the game with the fog partly lifted), so only the eleven OUT files it never rewrote
/// have pristine sha1s; a copy where one of those changed is reported played, with a warning that the import is
/// unchanged (fog and fights come from DOS I's tables; the Amiga files give only the town / dungeon places).
/// </summary>
public class P1AmigaPlayedCopyTests
{
    private const string Unpacked = "reverse-engineering/amiga/unpacked/PHANT";

    [RequiresFilesFact(Unpacked)]
    public void ReferenceDump_IsNotFlagged_AChangedPristineRegionIs()
    {
        var clean = ImportRunner.Scan([Repo.Path(Unpacked)]).Detections.Single(d => d.Profile.Id == "p1-amiga-1.0");
        Assert.Equal(DetectionStatus.Ready, clean.Status);
        Assert.Empty(clean.PlayedFiles);
        Assert.Equal(11, clean.Profile.MutableFiles.Count);

        var dir = Directory.CreateTempSubdirectory("phantasie-p1a-played-").FullName;
        try
        {
            foreach (var f in Directory.EnumerateFiles(Repo.Path(Unpacked)))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            var out2 = Path.Combine(dir, "out2.dat");
            var bytes = File.ReadAllBytes(out2);
            bytes[0] -= 120; // a cell's fog lifted, as play does
            File.WriteAllBytes(out2, bytes);
            var played = ImportRunner.Scan([dir]).Detections.Single(d => d.Profile.Id == "p1-amiga-1.0");
            Assert.Equal(["out2.dat"], played.PlayedFiles);
            var warning = ImportRunner.PlayedWarning(played);
            Assert.Contains("same as from a fresh copy", warning);
            Assert.DoesNotContain("fog", warning);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
