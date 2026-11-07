using System.Buffers.Binary;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// <c>DNG.SAV</c> (6386 B: u16 id + 6384 B screen image) and <c>DNGX.SAV</c> (2500 B level buffer) [C]
/// (save-file-format.md §Dungeon save slot, writer OV2 <c>0x5929</c>).
/// </summary>
public static class DungeonSaveCodec
{
    public const int DngSavSize = 2 + DungeonSaveSlot.ScreenImageSize; // 6386
    public const int DngxSavSize = DungeonLevel.BufferSize;           // 2500

    /// <summary>
    /// Read a slot. Returns null when the id word is 0 (the master disk and Utility → Reset the game zero both files, so
    /// no dungeon id matches).
    /// </summary>
    public static DungeonSaveSlot? Read(byte[] dngSav, byte[] dngxSav)
    {
        ArgumentNullException.ThrowIfNull(dngSav);
        ArgumentNullException.ThrowIfNull(dngxSav);
        if (dngSav.Length != DngSavSize)
            throw new InvalidDataException($"DNG.SAV expected {DngSavSize} bytes, got {dngSav.Length}.");
        if (dngxSav.Length != DngxSavSize)
            throw new InvalidDataException($"DNGX.SAV expected {DngxSavSize} bytes, got {dngxSav.Length}.");

        int id = BinaryPrimitives.ReadUInt16LittleEndian(dngSav);
        if (id == 0)
            return null;
        return new DungeonSaveSlot(id, dngxSav, dngSav.AsSpan(2).ToArray());
    }

    public static DungeonSaveSlot? ReadFiles(string dngSavPath, string dngxSavPath) =>
        Read(File.ReadAllBytes(dngSavPath), File.ReadAllBytes(dngxSavPath));

    /// <summary><c>DNG.SAV</c>: the id word then the screen image (zeros for a slot made by Core).</summary>
    public static byte[] WriteDngSav(DungeonSaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        var data = new byte[DngSavSize];
        BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)slot.DungeonId);
        Buffer.BlockCopy(slot.ScreenImage, 0, data, 2, DungeonSaveSlot.ScreenImageSize);
        return data;
    }

    /// <summary><c>DNGX.SAV</c>: the 2500-byte level buffer, verbatim.</summary>
    public static byte[] WriteDngxSav(DungeonSaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return (byte[])slot.LevelBuffer.Clone();
    }
}
