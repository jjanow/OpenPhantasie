using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The container readers (FAT12, AmigaDOS, Pasti, DIM, zip) give the same files, byte for byte, as the unpacked copies
/// in the repo (made with mtools / xdftool / stx_to_st.py / x68k_dim.py).
/// </summary>
public class ContainerTests
{
    private static Container Single(string image)
    {
        var notes = new List<string>();
        var cs = SourceScanner.Scan([Repo.Path(image)], notes);
        Assert.Empty(notes);
        return Assert.Single(cs);
    }

    private static void SameAsFolder(Container c, string folder, string prefix = "", params string[] skip)
    {
        var dir = Repo.Path(folder);
        var compared = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (skip.Contains(rel) || rel.EndsWith(".md", StringComparison.Ordinal) || rel.EndsWith(".txt", StringComparison.Ordinal))
                continue;
            var inImage = c.Files.FirstOrDefault(f => f.Path.Equals(prefix + rel, StringComparison.OrdinalIgnoreCase));
            Assert.True(inImage is not null, $"{rel} not in {c.Description}");
            Assert.True(File.ReadAllBytes(file).AsSpan().SequenceEqual(inImage!.Data), $"{rel} differs");
            compared++;
        }
        Assert.True(compared > 5, $"only {compared} files compared");
    }

    [RequiresFilesFact("reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima")]
    public void Dos1Ima_MatchesOriginalFiles()
    {
        var c = Single("reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima");
        Assert.Equal("fat12", c.Kind);
        SameAsFolder(c, "reverse-engineering/dos/original-files");
    }

    [RequiresFilesFact("reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.zip")]
    public void Dos1Zip_OpensTheImageInside()
    {
        var c = Single("reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.zip");
        Assert.Equal("fat12", c.Kind);
        Assert.Contains(c.Files, f => f.Name == "PHANTASI.COM");
    }

    [RequiresFilesFact("reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima")]
    public void Dos3Ima_MatchesUnpacked()
    {
        var c = Single("reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima");
        SameAsFolder(c, "reverse-engineering/dos/phantasie3/unpacked/ssi-1988");
    }

    [RequiresFilesFact("reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf")]
    public void Amiga1Adf_MatchesUnpacked()
    {
        var c = Single("reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf");
        Assert.Equal("adf", c.Kind);
        SameAsFolder(c, "reverse-engineering/amiga/unpacked/PHANT");
    }

    [RequiresFilesFact("reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf")]
    public void Amiga3Adf_MatchesUnpacked()
    {
        var c = Single("reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf");
        SameAsFolder(c, "reverse-engineering/amiga/phantasie3/unpacked/PhantasieIII");
    }

    [RequiresFilesTheory("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx")]
    [InlineData("phantasie2-v1.0-disk1.stx", "disk1")]
    [InlineData("phantasie2-v1.0-disk2.stx", "disk2")]
    public void StStx_MatchesUnpacked(string image, string folder)
    {
        var c = Single("reverse-engineering/atari-st/original-images/phantasie2/" + image);
        Assert.Equal("stx", c.Kind);
        SameAsFolder(c, "reverse-engineering/atari-st/phantasie2/unpacked/" + folder);
    }

    [RequiresFilesTheory("reverse-engineering/x68000/original-images/phantasie4", "reverse-engineering/x68000/phantasie4/unpacked/disk1")]
    [InlineData("Phantasie IV (1991)(Starcraft)(Disk 1 of 2).dim", "disk1")]
    [InlineData("Phantasie IV (1991)(Starcraft)(Disk 2 of 2).dim", "disk2")]
    public void X68kDim_MatchesUnpacked(string image, string folder)
    {
        var c = Single("reverse-engineering/x68000/original-images/phantasie4/" + image);
        Assert.Equal("dim", c.Kind);
        SameAsFolder(c, "reverse-engineering/x68000/phantasie4/unpacked/" + folder);
    }

    [RequiresFilesTheory("reverse-engineering/dos/original-images")]
    [InlineData("p1-dos-1987", "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.ima")]
    [InlineData("p1-dos-1987", "reverse-engineering/dos/original-images/phantasie1/phantasie-1987-ssi-360k.zip")]
    [InlineData("p1-dos-1987", "reverse-engineering/dos/original-files")]
    [InlineData("p1-amiga-1.0", "reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf")]
    [InlineData("p3-dos-1988", "reverse-engineering/dos/original-images/phantasie3/phantasie3-1988-ssi-360k.ima")]
    [InlineData("p3-amiga-1.0", "reverse-engineering/amiga/original-images/phantasie3/phantasie3-v1.0-1987-ssi.adf")]
    [InlineData("kickstart-1.3", "reverse-engineering/amiga/original-images/kickstart")]
    public void Detects_ReferenceCopies(string profile, string source)
    {
        var scan = ImportRunner.Scan([Repo.Path(source)]);
        var d = Assert.Single(scan.Detections, x => x.Profile.Id == profile);
        Assert.Equal(DetectionStatus.Ready, d.Status);
        Assert.Empty(d.PlayedFiles);
    }

    [RequiresFilesFact("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx")]
    public void Detects_StIi_BothDisks_AndNamesAMissingOne()
    {
        var both = ImportRunner.Scan([Repo.Path("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx"),
            Repo.Path("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk2.stx")]);
        Assert.Equal(DetectionStatus.Ready, Assert.Single(both.Detections, d => d.Profile.Id == "p2-st-1.0").Status);

        var one = ImportRunner.Scan([Repo.Path("reverse-engineering/atari-st/original-images/phantasie2/phantasie2-v1.0-disk1.stx")]);
        var d = Assert.Single(one.Detections, x => x.Profile.Id == "p2-st-1.0");
        Assert.Equal(DetectionStatus.MissingDisk, d.Status);
        Assert.Contains("disk 2 not found", d.Message);
    }

    [RequiresFilesFact("reverse-engineering/x68000/original-images/phantasie4")]
    public void Detects_X68000_IV()
    {
        var scan = ImportRunner.Scan([Repo.Path("reverse-engineering/x68000/original-images/phantasie4")]);
        Assert.Equal(DetectionStatus.Ready, Assert.Single(scan.Detections, d => d.Profile.Id == "p4-x68000-1.02").Status);
    }

    [RequiresFilesTheory("reverse-engineering/apple2/original-images")]
    [InlineData("p2-apple2", "reverse-engineering/apple2/original-images/phantasie2/woz-original")]
    [InlineData("p1-c64", "reverse-engineering/c64/original-images/phantasie1/en")]
    [InlineData("p1-dos-bonus-1991", "reverse-engineering/dos/original-images/phantasie1/bonus-v1.1-1991-wizardworks")]
    [InlineData("p4-pc98", "reverse-engineering/pc98/original-images/phantasie4")]
    public void Recognises_UnsupportedVersions(string profile, string source)
    {
        var scan = ImportRunner.Scan([Repo.Path(source)]);
        var d = Assert.Single(scan.Detections, x => x.Profile.Id == profile);
        Assert.NotEqual(DetectionStatus.Ready, d.Status); // the Apple II .woz is a supported version's unreadable image
        Assert.Empty(scan.Importable);
    }

    [Fact]
    public void PlayedDos1Copy_IsDetectedAndWarned()
    {
        if (!Repo.Has("reverse-engineering/dos/original-files/OUT1.DAT"))
            return;
        var dir = Directory.CreateTempSubdirectory("phantasie-played-").FullName;
        try
        {
            foreach (var f in Directory.EnumerateFiles(Repo.Path("reverse-engineering/dos/original-files")))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            var out3 = Path.Combine(dir, "OUT3.DAT");
            var bytes = File.ReadAllBytes(out3);
            bytes[100] ^= 0x40; // a lifted fog bit
            File.WriteAllBytes(out3, bytes);
            var scan = ImportRunner.Scan([dir]);
            var d = Assert.Single(scan.Detections, x => x.Profile.Id == "p1-dos-1987");
            Assert.Equal(DetectionStatus.Ready, d.Status);
            Assert.Equal(["OUT3.DAT"], d.PlayedFiles);
            Assert.Contains("fog is already lifted", ImportRunner.PlayedWarning(d));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
    [RequiresFilesFact("reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf")]
    public void SharedFileNames_DoNotMakeAnUnknownBuild()
    {
        // The Amiga I disk holds MESS1, DNG1, SCROLLS.DTX, MSTR2.PAT like II's disk 2, but none of II's own files.
        var scan = ImportRunner.Scan([Repo.Path("reverse-engineering/amiga/original-images/phantasie1/phantasie-v1.0-1987-ssi.adf")]);
        Assert.DoesNotContain(scan.Detections, d => d.Profile.Id == "p2-st-1.0");
    }

    [Fact]
    public void ChangedProgram_IsAnUnknownBuild_NotImported()
    {
        if (!Repo.Has("reverse-engineering/dos/original-files/PHANTASI.COM"))
            return;
        var dir = Directory.CreateTempSubdirectory("phantasie-build-").FullName;
        try
        {
            foreach (var f in Directory.EnumerateFiles(Repo.Path("reverse-engineering/dos/original-files")))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            var com = Path.Combine(dir, "PHANTASI.COM");
            var bytes = File.ReadAllBytes(com);
            bytes[2000] ^= 1;
            File.WriteAllBytes(com, bytes);
            var scan = ImportRunner.Scan([dir]);
            var d = Assert.Single(scan.Detections, x => x.Profile.Id == "p1-dos-1987");
            Assert.Equal(DetectionStatus.UnknownBuild, d.Status);
            Assert.Contains("PHANTASI.COM", d.Message);
            Assert.Empty(scan.Importable);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
