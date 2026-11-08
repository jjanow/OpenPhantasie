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
        // 13-sector: the one 16-sector sector (the boot sector, its address checksum bad) does not make it 16-sector.
        Assert.Equal(13, decoded.SectorsPerTrack);
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

    /// <summary>DOS 3.3's interleave, written out: <c>.dsk</c> (DOS-order) sector l of a track is physical sector [l].</summary>
    private static readonly int[] DosOrderPhysical = [0, 13, 11, 9, 7, 5, 3, 1, 14, 12, 10, 8, 6, 4, 2, 15];

    /// <summary>A sector's payload tagged with its track and physical sector.</summary>
    private static byte[] Payload(int track, int physical) =>
        Enumerable.Range(0, 256).Select(i => i switch { 0 => (byte)physical, 1 => (byte)track, _ => (byte)(i * 13 + physical * 7 + track * 3) }).ToArray();

    [Fact]
    public void Synthetic16_Woz2_DecodesToADosOrderDsk()
    {
        // Tracks 0 and 1 of 16 sectors in 6-and-2, the rest absent; track 1's bit stream starts mid-sector, so one
        // sector runs across the splice.
        var woz = SyntheticWoz2(t => t < 2 ? Track62(t, p => Payload(t, p)) : null, rotateTrack: 1);
        var r = WozImage.TryDecode(woz)!;
        Assert.NotNull(r);
        Assert.Equal(16, r.SectorsPerTrack);
        Assert.Equal(33 * 16, r.Missing.Count);
        Assert.DoesNotContain(r.Missing, m => m.Track < 2);
        for (var t = 0; t < 2; t++)
            for (var l = 0; l < 16; l++)
                Assert.Equal(Payload(t, DosOrderPhysical[l]), r.Image.AsSpan(t * 4096 + l * 256, 256).ToArray());
        // Spot checks of the interleave by tag: .dsk sector 1 = physical 13, 7 = physical 1, 14 = physical 2.
        Assert.Equal((13, 1), (r.Image[4096 + 1 * 256], r.Image[4096 + 1 * 256 + 1]));
        Assert.Equal(1, r.Image[7 * 256]);
        Assert.Equal(2, r.Image[14 * 256]);
        Assert.True(r.Image.AsSpan(2 * 4096).IndexOfAnyExcept((byte)0) < 0);
    }

    [Fact]
    public void Synthetic16_BadChecksumOrEpilogue_LeavesTheSectorMissing()
    {
        // Track 0: sector 3's data has one nibble changed to another valid 6-and-2 byte (data checksum fails),
        // sector 5's data epilogue is DF AA, sector 9's address checksum is wrong, sector 11's address epilogue is
        // DE AB, sector 13's data field has a nibble outside the table.
        var faults = new Dictionary<int, Fault>
        {
            [3] = Fault.DataNibble, [5] = Fault.DataEpilogue, [9] = Fault.AddressChecksum, [11] = Fault.AddressEpilogue,
            [13] = Fault.InvalidNibble,
        };
        var woz = SyntheticWoz2(t => t == 0 ? Track62(0, p => Payload(0, p), faults) : null);
        var r = WozImage.TryDecode(woz)!;
        Assert.Equal(16, r.SectorsPerTrack);
        Assert.Equal(new[] { 3, 5, 9, 11, 13 }, r.Missing.Where(m => m.Track == 0).Select(m => m.Sector));
        for (var l = 0; l < 16; l++)
        {
            var physical = DosOrderPhysical[l];
            var expected = faults.ContainsKey(physical) ? new byte[256] : Payload(0, physical);
            Assert.Equal(expected, r.Image.AsSpan(l * 256, 256).ToArray());
        }
    }

    [RequiresFilesFact(P2DskA, P2DskB)]
    public void P2_As16SectorWoz_ImportsAsTheDskPair()
    {
        // The 4am pair written out as 16-sector .woz disks (physical sector s = the 4am image's sector s, as on a
        // 16-sector crack such as the Memorial Set's DOS-order .dsk): the .woz decodes to a DOS-order .dsk,
        // RdosVolume finds the catalog in that order, and the import is byte-equal to the .dsk pair's.
        var dir = Directory.CreateTempSubdirectory("phantasie-woz16-").FullName;
        try
        {
            var paths = new List<string>();
            foreach (var (dsk, name) in new[] { (P2DskA, "a.woz"), (P2DskB, "b.woz") })
            {
                var image = File.ReadAllBytes(Repo.Path(dsk));
                var woz = SyntheticWoz2(t => Track62(t, p => image[(t * 4096 + p * 256)..(t * 4096 + p * 256 + 256)]));
                var decoded = WozImage.TryDecode(woz)!;
                Assert.Equal(16, decoded.SectorsPerTrack);
                Assert.Empty(decoded.Missing);
                // DOS order: .dsk sector l = physical (4am) sector DosOrderPhysical[l].
                for (var l = 0; l < 16; l++)
                    Assert.Equal(image[(5 * 4096 + DosOrderPhysical[l] * 256)..][..256], decoded.Image[(5 * 4096 + l * 256)..][..256]);
                var path = Path.Combine(dir, name);
                File.WriteAllBytes(path, woz);
                paths.Add(path);
            }
            var fromWoz = PipelineHarness.Run("p2-apple2", paths[0], paths[1]);
            var fromDsk = PipelineHarness.Run("p2-apple2", P2DskA, P2DskB);
            Assert.Equal(fromDsk.AllOutputs.Keys.Order(StringComparer.Ordinal), fromWoz.AllOutputs.Keys.Order(StringComparer.Ordinal));
            foreach (var (key, bytes) in fromDsk.AllOutputs)
                Assert.True(bytes.AsSpan().SequenceEqual(fromWoz.AllOutputs[key]), $"{key} differs");
            Assert.All(fromWoz.Detection.Containers, c => Assert.Equal("woz", c.Kind));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    public enum Fault { DataNibble, DataEpilogue, AddressChecksum, AddressEpilogue, InvalidNibble }

    private static readonly byte[] Gcr62 =
    [
        0x96, 0x97, 0x9A, 0x9B, 0x9D, 0x9E, 0x9F, 0xA6, 0xA7, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xB2, 0xB3,
        0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xCB, 0xCD, 0xCE, 0xCF, 0xD3,
        0xD6, 0xD7, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE5, 0xE6, 0xE7, 0xE9, 0xEA, 0xEB, 0xEC,
        0xED, 0xEE, 0xEF, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF,
    ];

    /// <summary>DOS 3.3's prenibble: 256 bytes to 342 six-bit values (86 of low-bit pairs, swapped, then 256 tops).</summary>
    private static int[] Prenibble62(byte[] data)
    {
        var v = new int[342];
        for (var i = 0; i < 256; i++)
        {
            var low = data[i] & 3;
            v[i % 86] |= ((low & 1) << 1 | low >> 1) << (2 * (i / 86));
            v[86 + i] = data[i] >> 2;
        }
        return v;
    }

    /// <summary>
    /// One 16-sector track as bits (physical sectors 0-15 in order): 10-bit sync bytes (<c>FF</c> and two 0 bits),
    /// address field, sync, data field, each with its <c>DE AA EB</c> epilogue.
    /// </summary>
    private static List<bool> Track62(int track, Func<int, byte[]> sector, Dictionary<int, Fault>? faults = null)
    {
        var bits = new List<bool>();
        void Byte(int b) => bits.AddRange(Enumerable.Range(0, 8).Select(k => ((b >> (7 - k)) & 1) != 0));
        void Sync(int n)
        {
            for (var k = 0; k < n; k++)
            {
                Byte(0xFF);
                bits.AddRange([false, false]);
            }
        }
        void FourAndFour(int x)
        {
            Byte((x >> 1) | 0xAA);
            Byte(x | 0xAA);
        }
        for (var s = 0; s < 16; s++)
        {
            var fault = faults is not null && faults.TryGetValue(s, out var f) ? f : (Fault?)null;
            Sync(16);
            foreach (var b in new[] { 0xD5, 0xAA, 0x96 })
                Byte(b);
            FourAndFour(254);
            FourAndFour(track);
            FourAndFour(s);
            FourAndFour(254 ^ track ^ s ^ (fault == Fault.AddressChecksum ? 1 : 0));
            foreach (var b in new[] { 0xDE, fault == Fault.AddressEpilogue ? 0xAB : 0xAA, 0xEB })
                Byte(b);
            Sync(6);
            var disk = new List<int>();
            var previous = 0;
            foreach (var x in Prenibble62(sector(s)))
            {
                disk.Add(Gcr62[x ^ previous]);
                previous = x;
            }
            disk.Add(Gcr62[previous]);
            if (fault == Fault.DataNibble)
                disk[100] = Gcr62[(Array.IndexOf(Gcr62, (byte)disk[100]) + 1) % 64];
            if (fault == Fault.InvalidNibble)
                disk[200] = 0xD5;
            foreach (var b in new[] { 0xD5, 0xAA, 0xAD }.Concat(disk))
                Byte(b);
            foreach (var b in new[] { fault == Fault.DataEpilogue ? 0xDF : 0xDE, 0xAA, 0xEB })
                Byte(b);
        }
        Sync(30);
        return bits;
    }

    /// <summary>
    /// A WOZ 2 file: INFO, TMAP (track t at quarter-track 4t), TRKS with each present track's bits in whole 512-byte
    /// blocks from block 3. <paramref name="rotateTrack"/>'s bit stream starts a third of the way into its 6th sector.
    /// </summary>
    private static byte[] SyntheticWoz2(Func<int, List<bool>?> track, int rotateTrack = -1)
    {
        var tmap = Enumerable.Repeat((byte)0xFF, 160).ToArray();
        var entries = new byte[160 * 8];
        var blocks = new List<byte>();
        var index = 0;
        for (var t = 0; t < 35; t++)
        {
            var bits = track(t);
            if (bits is null)
                continue;
            if (t == rotateTrack)
            {
                var perSector = (bits.Count - 30 * 10) / 16;
                var shift = 5 * perSector + perSector / 3;
                bits = bits.Skip(shift).Concat(bits.Take(shift)).ToList();
            }
            var bytes = new byte[(bits.Count + 4095) / 4096 * 512];
            for (var k = 0; k < bits.Count; k++)
                if (bits[k])
                    bytes[k >> 3] |= (byte)(0x80 >> (k & 7));
            tmap[t * 4] = (byte)index;
            BitConverter.GetBytes((ushort)(3 + blocks.Count / 512)).CopyTo(entries, index * 8);
            BitConverter.GetBytes((ushort)(bytes.Length / 512)).CopyTo(entries, index * 8 + 2);
            BitConverter.GetBytes((uint)bits.Count).CopyTo(entries, index * 8 + 4);
            blocks.AddRange(bytes);
            index++;
        }
        var file = new List<byte>();
        file.AddRange("WOZ2"u8.ToArray());
        file.AddRange([0xFF, 0x0A, 0x0D, 0x0A, 0, 0, 0, 0]);
        file.AddRange("INFO"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(60));
        var info = new byte[60];
        (info[0], info[1]) = (2, 1); // version 2, 5.25"
        file.AddRange(info);
        file.AddRange("TMAP"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(160));
        file.AddRange(tmap);
        file.AddRange("TRKS"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(entries.Length + blocks.Count));
        file.AddRange(entries);
        Assert.Equal(3 * 512, file.Count);
        file.AddRange(blocks);
        return file.ToArray();
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
