using System.Text;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P1C64;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s228: the numbers the C64 packs' play sounds ship (<see cref="C64PlaySounds"/>, I's GRAPHICS.md 9.8) equal the disks':
/// <c>MN</c>'s field 13 (the marks), the AWAKENS and ring notes of the programs' DATA lines, and III's <c>SND</c> bytes the
/// sounds were read from. Numbers only (no text of the originals).
/// </summary>
public class C64PlaySoundsTests
{
    private const string I1 = "reverse-engineering/c64/original-images/phantasie1/c64pp/phantasie_s1[ssi_1985](v1_4).g64";
    private const string II1 = "reverse-engineering/c64/original-images/phantasie2/c64pp/phantasie_ii_s1[ssi_1986](v1_2)(never_used).g64";
    private const string III1 = "reverse-engineering/c64/original-images/phantasie3/c64pp/phantasie_iii_s1[ssi_1987](v1_0)(!).g64";

    private static byte[] Read(string image, string name)
    {
        var raw = File.ReadAllBytes(Repo.Path(image));
        var vol = D64Volume.TryOpen(G64Image.IsG64(raw) ? G64Image.Decode(raw).Image : raw) ?? throw new InvalidDataException(image);
        return vol.Read(vol.Files.First(e => e.Name == name));
    }

    /// <summary>Field 13 of <c>MN</c>'s first 80 records (100 bytes each, CR-ended decimal fields).</summary>
    private static int[] Field13(byte[] mn) =>
        [.. Enumerable.Range(0, 80).Select(m => int.Parse(Encoding.ASCII.GetString(mn, m * 100, 100).Split('\r')[13]))];

    /// <summary>The integers of a program's plain BASIC DATA lines (its first 2 KB, before the compiled code).</summary>
    private static List<int> DataNumbers(byte[] prg) =>
        [.. Encoding.ASCII.GetString(prg, 0, Math.Min(prg.Length, 2048)).Split(',', '\0', ':')
            .Select(t => t.Trim()).Where(t => int.TryParse(t, out _)).Select(int.Parse)];

    private static bool Contains(IReadOnlyList<int> list, IReadOnlyList<int> run) =>
        Enumerable.Range(0, list.Count - run.Count + 1).Any(i => list.Skip(i).Take(run.Count).SequenceEqual(run));

    [RequiresFilesFact(I1)]
    public void I_MarksAndNotes_AreTheDisks()
    {
        Assert.Equal(C64PlaySounds.MarksP1, Field13(Read(I1, "MN")));
        Assert.True(Contains(DataNumbers(Read(I1, "OUTDOORS")), C64PlaySounds.AwakeNotes));
        Assert.True(Contains(DataNumbers(Read(I1, "TOWNR")), [.. C64PlaySounds.RingNotes, -1]));
    }

    [RequiresFilesFact(II1)]
    public void II_MarksAndNotes_AreTheDisks()
    {
        Assert.Equal(C64PlaySounds.MarksP2, Field13(Read(II1, "MN")));
        Assert.True(Contains(DataNumbers(Read(II1, "OUTDOORS2")), C64PlaySounds.AwakeNotes));
    }

    [RequiresFilesFact(III1)]
    public void III_SndIsTheRoutinesRead()
    {
        var snd = Read(III1, "SND");
        Assert.Equal((0x6E, 0x12), (snd[0], snd[1])); // loads at $126E
        byte At(int address) => snd[2 + address - 0x126E];
        // entry2 ($12C2): $81 / $80 into entry0's operands; entry4 ($12E2): SR $FA, low byte $B4, A = 4; then $30
        Assert.Equal([0xA9, 0x81, 0x8D, 0x9E, 0x12, 0xA9, 0x80, 0x8D, 0xA3, 0x12], Enumerable.Range(0x12C2, 10).Select(At));
        Assert.Equal([0xA9, 0xFA, 0x8D, 0x99, 0x12, 0xA9, 0xB4, 0x8D, 0x87, 0x12, 0xA9, 0x04], Enumerable.Range(0x12E2, 12).Select(At));
        Assert.Equal(0x30, At(0x12FC));
        // entry1 ($12A8): noise off, freq hi $80, AD $71, SR 0, noise gate on
        Assert.Equal([0x80, 0x80, 0x71, 0x00, 0x81], new[] { 0x12A9, 0x12AE, 0x12B3, 0x12B8, 0x12BD }.Select(At));
        var phant = Read(III1, "PHANT.OBJ");
        // the hit's JSR $1274 at file $3B43; "AND HITS" ends at file $2B42, so the print routine leaves A = $42
        Assert.Equal([0x20, 0x74, 0x12], phant.AsSpan(2 + 0x3B43 - 0x1400, 3).ToArray());
    }
}
