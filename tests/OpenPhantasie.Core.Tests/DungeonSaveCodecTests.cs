using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>DNG.SAV / DNGX.SAV one-slot dungeon save (save-file-format.md §Dungeon save slot, OV2 0x5929) [C].</summary>
public class DungeonSaveCodecTests
{
    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [OriginalDataTheory]
    [InlineData("DNG.s35.sav", "DNGX.s35.sav")]
    [InlineData("DNG.pass2.sav", "DNGX.pass2.sav")]
    public void Fixtures_RoundTripByteForByte(string dng, string dngx)
    {
        var dngBytes = Fixture(dng);
        var dngxBytes = Fixture(dngx);
        var slot = DungeonSaveCodec.Read(dngBytes, dngxBytes);

        Assert.NotNull(slot);
        Assert.Equal(1, slot!.DungeonId);
        Assert.Equal(dngBytes, DungeonSaveCodec.WriteDngSav(slot));
        Assert.Equal(dngxBytes, DungeonSaveCodec.WriteDngxSav(slot));
    }

    [OriginalDataFact]
    public void S35_LevelBuffer_IsDng1_WithTheClearedFightCell()
    {
        var slot = DungeonSaveCodec.Read(Fixture("DNG.s35.sav"), Fixture("DNGX.s35.sav"))!;
        var original = GameDataRepository.LoadPhantasie1().Dungeons.Single(d => d.Id == "DNG1").ToRuntime().ToBuffer();

        var diffs = Enumerable.Range(0, DungeonLevel.BufferSize).Where(i => original[i] != slot.LevelBuffer[i]).ToList();
        Assert.Equal(new[] { 1240 }, diffs);
        Assert.Equal(0xCB, original[1240]);
        Assert.Equal(0xC8, slot.LevelBuffer[1240]); // 0x2a26: lock/ambush 0xc8..0xd6 -> 0xc8

        var level = DungeonLevel.FromBuffer(1, "DNG1", slot.LevelBuffer);
        Assert.Equal(0xC8, level.Get(1240 % DungeonLevel.Width, 1240 / DungeonLevel.Width));
        Assert.Equal(slot.LevelBuffer, level.ToBuffer());
    }

    [OriginalDataFact]
    public void Pass2_Buffer_IsTheOriginalDng1()
    {
        var slot = DungeonSaveCodec.Read(Fixture("DNG.pass2.sav"), Fixture("DNGX.pass2.sav"))!;
        var original = GameDataRepository.LoadPhantasie1().Dungeons.Single(d => d.Id == "DNG1").ToRuntime().ToBuffer();
        Assert.Equal(original, slot.LevelBuffer);
    }

    [Fact]
    public void ZeroIdWord_IsAnEmptySlot()
    {
        Assert.Null(DungeonSaveCodec.Read(new byte[DungeonSaveCodec.DngSavSize], new byte[DungeonSaveCodec.DngxSavSize]));
    }

    [Fact]
    public void CoreMadeSlot_WritesZeroScreenImage()
    {
        var buffer = new byte[DungeonLevel.BufferSize];
        buffer[5] = 0x42;
        var slot = new DungeonSaveSlot(7, buffer);
        var dng = DungeonSaveCodec.WriteDngSav(slot);

        Assert.Equal(DungeonSaveCodec.DngSavSize, dng.Length);
        Assert.Equal(7, dng[0]);
        Assert.Equal(0, dng[1]);
        Assert.All(dng.Skip(2), b => Assert.Equal(0, b));
        var back = DungeonSaveCodec.Read(dng, DungeonSaveCodec.WriteDngxSav(slot))!;
        Assert.Equal(7, back.DungeonId);
        Assert.Equal(0x42, back.LevelBuffer[5]);
    }

    [Fact]
    public void WrongSizes_Throw()
    {
        Assert.Throws<InvalidDataException>(() => DungeonSaveCodec.Read(new byte[10], new byte[DungeonSaveCodec.DngxSavSize]));
        Assert.Throws<InvalidDataException>(() => DungeonSaveCodec.Read(new byte[DungeonSaveCodec.DngSavSize], new byte[10]));
    }
}
