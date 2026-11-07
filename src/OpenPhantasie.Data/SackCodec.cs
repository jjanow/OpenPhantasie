using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// <c>SACK.DAT</c> (120 B) = 60 × u16 found-item ids <c>0x63fa</c>, insertion order, 0 = empty [C] (s35,
/// save-file-format.md §Loot sack). No XP or gold field: the combat pool lives in RAM and is split before every save.
/// </summary>
public static class SackCodec
{
    public const int SlotCount = 60;
    public const int FileSize = SlotCount * 2;

    /// <summary>The 60 slots as stored (holes kept).</summary>
    public static int[] ReadSlots(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != FileSize)
            throw new InvalidDataException($"SACK.DAT expected {FileSize} bytes, got {data.Length}.");
        var slots = new int[SlotCount];
        for (var i = 0; i < SlotCount; i++)
            slots[i] = DosFieldWriter.ReadU16(data, 2 * i);
        return slots;
    }

    /// <summary>The slots up to the last nonzero one (holes between items kept, trailing empties dropped).</summary>
    public static int[] TrimSlots(int[] slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var n = slots.Length;
        while (n > 0 && slots[n - 1] == 0)
            n--;
        return slots[..n];
    }

    /// <summary>Fill <paramref name="sack"/> from SACK.DAT: the slots with their holes ([D✓] s65), XP/GP = 0.</summary>
    public static void Read(byte[] data, LootSack sack)
    {
        ArgumentNullException.ThrowIfNull(sack);
        var slots = ReadSlots(data);
        sack.Clear();
        sack.ItemIds.AddRange(TrimSlots(slots));
    }

    /// <summary>
    /// Write SACK.DAT from the sack's slots (0 = hole), so the DOS layout survives a round trip. Slots past 60 that still
    /// hold items (the remaster's 90, D-018) are packed into the first free slots instead. More than 60 items is an
    /// export blocker (P1.4 decision 5) — checked by <see cref="DosSaveSet"/>; here it throws.
    /// </summary>
    public static byte[] Write(IReadOnlyList<int> itemIds, ICollection<DosSaveWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var count = itemIds.Count(id => id != 0);
        if (count > SlotCount)
            throw new InvalidOperationException(
                $"The sack holds {count} items; SACK.DAT holds {SlotCount}.");

        var slots = itemIds.Count <= SlotCount
            ? itemIds.Concat(Enumerable.Repeat(0, SlotCount - itemIds.Count)).ToArray()
            : itemIds.Where(id => id != 0).Concat(Enumerable.Repeat(0, SlotCount - count)).ToArray();

        var data = new byte[FileSize];
        for (var i = 0; i < SlotCount; i++)
            DosFieldWriter.U16(data, 2 * i, slots[i], "SACK.DAT", GameText.F("core.dossave.slot_x", i), warnings);
        return data;
    }
}
