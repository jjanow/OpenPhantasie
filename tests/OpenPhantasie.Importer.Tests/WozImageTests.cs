using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// The Apple II <c>.woz</c> reader (<see cref="WozImage"/>): the original SSI disks of Phantasie I (WOZ 2) and II
/// (WOZ 1) decode to the same sectors as the 4am crack's <c>.dsk</c> made from them, except track 0 (the crack's own
/// boot and RWTS) and the crack's patches listed in the 4am notes; II imported from the <c>.woz</c> pair equals the
/// import from the <c>.dsk</c> pair byte for byte.
/// </summary>
[Collection(nameof(LocatorCollection))]
public class WozImageTests
{
    private const string Images = "reverse-engineering/apple2/original-images/";
    private const string P2WozA = Images + "phantasie2/woz-original/phantasie2-disk1-side-a.woz";
    private const string P2WozB = Images + "phantasie2/woz-original/phantasie2-disk1-side-b.woz";
    private const string P2DskA = Images + "phantasie2/4am-crack/phantasie2-4am-side-a.dsk";
    private const string P2DskB = Images + "phantasie2/4am-crack/phantasie2-4am-side-b.dsk";
    private const string P1WozA = Images + "phantasie1/woz-original/phantasie1-boot.woz";
    private const string P1WozB = Images + "phantasie1/woz-original/phantasie1-dungeon.woz";
    private const string P1DskA = Images + "phantasie1/4am-crack/phantasie1-4am-side-a-boot.dsk";
    private const string P1DskB = Images + "phantasie1/4am-crack/phantasie1-4am-side-b-dungeon.dsk";

    /// <summary>The 4am crack's patches off track 0 (its notes): (track, sector, offset, original bytes, cracked bytes).</summary>
    public static TheoryData<string, string, string> Disks => new()
    {
        { P2WozA, P2DskA, "14.7.$19:20A008B0>BD88C090" },
        { P2WozB, P2DskB, "" },
        { P1WozA, P1DskA, "33.12.$19:20A008B0>BD88C090 34.2.$0F:C6>A9" },
        { P1WozB, P1DskB, "34.5.$0F:C6>A9" },
    };

    [RequiresFilesTheory(P2WozA, P2WozB, P1WozA, P1WozB)]
    [MemberData(nameof(Disks))]
    public void Sectors_EqualThe4amDsk_ButTrack0AndTheCrackPatches(string woz, string dsk, string patches)
    {
        var raw = File.ReadAllBytes(Repo.Path(woz));
        Assert.True(WozImage.IsWoz(raw));
        var decoded = WozImage.TryDecode(raw)!;
        Assert.NotNull(decoded);
        // Track 0 sector 12: an address field with no data field (the 16-sector boot sector is in its place).
        Assert.Equal(new[] { (0, 12) }, decoded.Missing);
        var reference = File.ReadAllBytes(Repo.Path(dsk));
        var differing = new List<string>();
        for (var t = 1; t < 35; t++)
            for (var s = 0; s < 16; s++)
            {
                var a = decoded.Image[(t * 4096 + s * 256)..(t * 4096 + s * 256 + 256)];
                var b = reference[(t * 4096 + s * 256)..(t * 4096 + s * 256 + 256)];
                if (a.SequenceEqual(b))
                    continue;
                var first = Enumerable.Range(0, 256).First(i => a[i] != b[i]);
                var last = Enumerable.Range(0, 256).Last(i => a[i] != b[i]);
                differing.Add($"{t}.{s}.${first:X2}:{Convert.ToHexString(a[first..(last + 1)])}>{Convert.ToHexString(b[first..(last + 1)])}");
            }
        Assert.Equal(patches, string.Join(' ', differing));
    }

    [RequiresFilesTheory(P2WozA, P2WozB, P1WozA, P1WozB)]
    [MemberData(nameof(Disks))]
    public void Catalog_AndFiles_EqualThe4amDsk(string woz, string dsk, string patches)
    {
        _ = patches;
        var w = RdosVolume.TryOpen(WozImage.TryDecode(File.ReadAllBytes(Repo.Path(woz)))!.Image)!;
        var d = RdosVolume.TryOpen(File.ReadAllBytes(Repo.Path(dsk)))!;
        Assert.Equal(d.Entries, w.Entries);
        // RDOS itself (track 0 holds the crack's RWTS) and the files the crack patched differ; every other file is equal.
        var differ = w.Files.Where(e => !w.Read(e).AsSpan().SequenceEqual(d.Read(e))).Select(e => e.Name).ToList();
        Assert.All(differ, n => Assert.Contains(n, new[] { "RDOS 2.1 COPYRIGHT 1981", "SSI.INIT", "SSI.INIT2", "QWERTY" }));
        Assert.Contains("RDOS 2.1 COPYRIGHT 1981", differ);
    }

    [RequiresFilesFact(P2WozA, P2WozB, P2DskA, P2DskB)]
    public void P2_ImportFromTheWozPair_EqualsTheDskPair()
    {
        var woz = PipelineHarness.Run("p2-apple2", P2WozA, P2WozB);
        var dsk = PipelineHarness.Run("p2-apple2", P2DskA, P2DskB);
        Assert.True(dsk.AllOutputs.Keys.Count(k => k.StartsWith("GameData/phantasie2/", StringComparison.Ordinal)) > 10);
        Assert.Equal(dsk.AllOutputs.Keys.Order(StringComparer.Ordinal), woz.AllOutputs.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, bytes) in dsk.AllOutputs)
            Assert.True(bytes.AsSpan().SequenceEqual(woz.AllOutputs[key]), $"{key} differs");
        // The .woz disks are the played copy the crack was made from: the same state files, the same warning.
        Assert.Equal(dsk.Detection.PlayedFiles, woz.Detection.PlayedFiles);
        Assert.NotEmpty(woz.Detection.PlayedFiles);
        Assert.All(woz.Detection.Containers, c => Assert.Equal("woz", c.Kind));
    }

    [RequiresFilesFact(P1WozA, P1WozB, P1DskA, P1DskB)]
    public void P1_WozPairIsDetectedAsTheDskPair()
    {
        // No importer for Apple I: both pairs are recognised by their image hashes, and the files the .woz now shows
        // do not make another version's detection.
        var woz = ImportRunner.Scan([Repo.Path(P1WozA), Repo.Path(P1WozB)]);
        var dsk = ImportRunner.Scan([Repo.Path(P1DskA), Repo.Path(P1DskB)]);
        Assert.Equal(dsk.Detections.Select(d => (d.Profile.Id, d.Status)), woz.Detections.Select(d => (d.Profile.Id, d.Status)));
        Assert.Equal(("p1-apple2", DetectionStatus.Recognised), woz.Detections.Select(d => (d.Profile.Id, d.Status)).Single());
        Assert.Empty(woz.Importable);
    }

    [Fact]
    public void Malformed_IsNullNotAThrow()
    {
        var header = "WOZ2"u8.ToArray().Concat(new byte[] { 0xFF, 0x0A, 0x0D, 0x0A, 0, 0, 0, 0 }).ToArray();
        Assert.True(WozImage.IsWoz(header));
        Assert.Null(WozImage.TryDecode(header));
        // TMAP and TRKS chunks pointing past the end, a truncated chunk and random bytes.
        var chunks = header.Concat("TMAP"u8.ToArray()).Concat(BitConverter.GetBytes(160)).Concat(new byte[160])
            .Concat("TRKS"u8.ToArray()).Concat(BitConverter.GetBytes(1280)).Concat(Enumerable.Repeat((byte)0xFF, 1280)).ToArray();
        Assert.Null(WozImage.TryDecode(chunks));
        Assert.Null(WozImage.TryDecode(chunks[..300]));
        var noise = new byte[240_000];
        new Random(7).NextBytes(noise);
        header.CopyTo(noise, 0);
        Assert.Null(WozImage.TryDecode(noise));
        Assert.False(WozImage.IsWoz(new byte[143_360]));
    }

    [Fact]
    public void Synthetic_Woz1Track_Decodes()
    {
        // One track 0 of 13 sectors written with the 5-and-3 encoding the reader inverts; the rest of the disk absent.
        var sectors = Enumerable.Range(0, 13).Select(s => Enumerable.Range(0, 256).Select(i => (byte)(i * 7 + s * 31)).ToArray()).ToArray();
        var woz = SyntheticWoz1(sectors);
        var r = WozImage.TryDecode(woz)!;
        Assert.NotNull(r);
        for (var s = 0; s < 13; s++)
            Assert.Equal(sectors[s], r.Image.AsSpan(s * 256, 256).ToArray());
        Assert.Equal(34 * 13, r.Missing.Count);
        Assert.DoesNotContain(r.Missing, m => m.Track == 0);
    }

    private static readonly byte[] Gcr53 =
    [
        0xAB, 0xAD, 0xAE, 0xAF, 0xB5, 0xB6, 0xB7, 0xBA, 0xBB, 0xBD, 0xBE, 0xBF, 0xD6, 0xD7, 0xDA, 0xDB,
        0xDD, 0xDE, 0xDF, 0xEA, 0xEB, 0xED, 0xEE, 0xEF, 0xF5, 0xF6, 0xF7, 0xFA, 0xFB, 0xFD, 0xFE, 0xFF,
    ];

    /// <summary>DOS 3.2's prenibble: 256 bytes to 410 five-bit values (the inverse of the reader's layout).</summary>
    private static int[] Prenibble(byte[] data)
    {
        var v = new int[410];
        for (var g = 0; g < 51; g++)
        {
            var b = Enumerable.Range(0, 5).Select(k => data[5 * g + k]).ToArray();
            for (var k = 0; k < 5; k++)
                v[154 + 51 * k + 50 - g] = b[k] >> 3;
            v[103 + g] = (b[0] & 7) << 2 | ((b[3] >> 2) & 1) << 1 | ((b[4] >> 2) & 1);
            v[52 + g] = (b[1] & 7) << 2 | ((b[3] >> 1) & 1) << 1 | ((b[4] >> 1) & 1);
            v[1 + g] = (b[2] & 7) << 2 | (b[3] & 1) << 1 | (b[4] & 1);
        }
        v[409] = data[255] >> 3;
        v[0] = data[255] & 7;
        return v;
    }

    private static byte[] SyntheticWoz1(byte[][] sectors)
    {
        var nibbles = new List<byte>();
        void FourAndFour(int x) => nibbles.AddRange([(byte)((x >> 1) | 0xAA), (byte)(x | 0xAA)]);
        for (var s = 0; s < sectors.Length; s++)
        {
            nibbles.AddRange(Enumerable.Repeat((byte)0xFF, 20));
            nibbles.AddRange([0xD5, 0xAA, 0xB5]);
            FourAndFour(254);
            FourAndFour(0);
            FourAndFour(s);
            FourAndFour(254 ^ s);
            nibbles.AddRange([0xDE, 0xAA, 0xEB, 0xFF, 0xFF, 0xFF, 0xFF, 0xD5, 0xAA, 0xAD]);
            var previous = 0;
            foreach (var x in Prenibble(sectors[s]))
            {
                nibbles.Add(Gcr53[x ^ previous]);
                previous = x;
            }
            nibbles.Add(Gcr53[previous]);
            nibbles.AddRange([0xDE, 0xAA, 0xEB]);
        }
        var bits = new byte[6646];
        var count = nibbles.Count * 8;
        nibbles.ToArray().CopyTo(bits, 0);
        var file = new List<byte>();
        file.AddRange("WOZ1"u8.ToArray());
        file.AddRange([0xFF, 0x0A, 0x0D, 0x0A, 0, 0, 0, 0]);
        file.AddRange("INFO"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(60));
        file.AddRange(new byte[60]);
        file.AddRange("TMAP"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(160));
        var tmap = Enumerable.Repeat((byte)0xFF, 160).ToArray();
        tmap[0] = 0;
        file.AddRange(tmap);
        file.AddRange("TRKS"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(6656));
        file.AddRange(bits);
        file.AddRange(BitConverter.GetBytes((ushort)nibbles.Count));
        file.AddRange(BitConverter.GetBytes((ushort)count));
        file.AddRange(new byte[6]);
        return file.ToArray();
    }
}
