using System.Text;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// <see cref="RdosVolume"/> on SSI's two Apple II DOSes: RDOS 2.1 (13-sector; I, II v1.0) and RDOS 3.3 (16-sector;
/// II v2.1, III), and how the importer names the RDOS 3.3 disks (II v2.1 imported since s212; III not yet).
/// </summary>
[Collection(nameof(LocatorCollection))]
public class RdosVolumeTests
{
    private const string Images = "reverse-engineering/apple2/original-images/";
    private const string V21A = Images + "phantasie2/v2.1-a2can/phantasie2-v2.1-side-a-boot.dsk";
    private const string V21B = Images + "phantasie2/v2.1-a2can/phantasie2-v2.1-side-b-dungeon.dsk";
    private const string MemorialA = Images + "phantasie2/memorial-set-gog/PHANT2A.DSK";
    private const string P3Cr = Images + "phantasie3/Phantasie_III_1987_SSI_cr_Disk_1_of_3_Side_A.dsk";
    private const string P3Woz = Images + "phantasie3/woz-original-v3.0/phantasie3-v3.0-disk1-side-a-boot.woz";

    private static RdosVolume Open(string path) => RdosVolume.TryOpen(File.ReadAllBytes(Repo.Path(path)))!;

    [RequiresFilesTheory(V21A, V21B, P3Cr, MemorialA)]
    [InlineData(V21A, true, 76)]
    [InlineData(V21B, true, 29)]
    [InlineData(P3Cr, true, 105)]
    [InlineData(MemorialA, false, 72)]
    public void PicksTheLayout_AndReadsTheWholeCatalog(string path, bool rdos33, int entries)
    {
        var v = Open(path);
        Assert.Equal(rdos33, v.IsRdos33);
        Assert.Equal(entries, v.Entries.Count);
        Assert.StartsWith(rdos33 ? "RDOS 3.3" : "RDOS 2.1", v.Entries[0].Name);
    }

    [RequiresFilesFact(V21A, MemorialA)]
    public void V21_FilesTheVersionsShare_ReadTheSameAsV10()
    {
        var v21 = Open(V21A);
        var v10 = Open(MemorialA);
        foreach (var name in new[] { "TOWN2", "PHANTASIE II", "ITEM", "TINFO", "TF2", "SHAPES", "ICE DRAGON", "INFO" })
            Assert.Equal(v10.Read(v10.Files.Single(e => e.Name == name)), v21.Read(v21.Files.Single(e => e.Name == name)));
    }

    [RequiresFilesFact(P3Woz)]
    public void P3Woz_DecodesToRdos33_Version30()
    {
        var woz = WozImage.TryDecode(File.ReadAllBytes(Repo.Path(P3Woz)))!;
        Assert.Equal(16, woz.SectorsPerTrack);
        Assert.Empty(woz.Missing);
        var v = RdosVolume.TryOpen(woz.Image)!;
        Assert.True(v.IsRdos33);
        var intro = Encoding.ASCII.GetString(v.Read(v.Files.Single(e => e.Name == "INTRO3")).Select(b => (byte)(b & 0x7F)).ToArray());
        Assert.Contains("VERSION 3.0", intro);
    }

    [RequiresFilesTheory(V21A, P3Woz)]
    [InlineData("p2-apple2-2.1", "phantasie2/v2.1-a2can")]
    [InlineData("p3-apple2", "phantasie3/woz-original-v3.0")]
    public void Rdos33Disks_AreTheirOwnVersion_NotAnotherBuild(string profile, string folder)
    {
        var scan = ImportRunner.Scan([Repo.Path(Images + folder)]);
        var d = Assert.Single(scan.Detections);
        Assert.Equal(profile, d.Profile.Id);
        // s212: both are imported now (II v2.1, III's Apple II tables).
        Assert.Equal(DetectionStatus.Ready, d.Status);
        Assert.Equal([profile], scan.Importable.Select(x => x.Profile.Id));
        Assert.Empty(scan.Notes);
    }
}
