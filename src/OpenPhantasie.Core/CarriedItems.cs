namespace OpenPhantasie.Core;

/// <summary>
/// The nine carried item slots as DOS lays them out [C] (s58 static).
/// <see cref="Character.InventoryItemIds"/> holds the slots in order up to the last held item; an id of 0 is an empty
/// slot. A drunk potion empties its slot in place (OV2 <c>0x446b</c>, OV1 <c>0x31ac</c>), the Armory fills the first
/// empty slot (OV1 <c>0x1c18..0x1c2e</c>, write <c>0x1cef</c>), and Distribute zeroes every slot before refilling them in
/// order (<c>0x2f22</c>). Capacity counts held items only, so an empty slot is free space.
/// </summary>
public static class CarriedItems
{
    /// <summary>Held items (non-zero slots) — the DOS 9-item count (OV1 <c>0x1c16</c>, <c>0x2dc4</c>).</summary>
    public static int Count(Character ch)
    {
        ArgumentNullException.ThrowIfNull(ch);
        return ch.InventoryItemIds.Count(id => id != 0);
    }

    /// <summary>True when all <see cref="Character.InventorySlots"/> slots hold an item.</summary>
    public static bool IsFull(Character ch) => Count(ch) >= Character.InventorySlots;

    /// <summary>Put <paramref name="itemId"/> into the first empty slot (appending past the last one); returns the slot.</summary>
    public static int Put(Character ch, int itemId)
    {
        ArgumentNullException.ThrowIfNull(ch);
        var slot = ch.InventoryItemIds.IndexOf(0);
        if (slot >= 0)
        {
            ch.InventoryItemIds[slot] = itemId;
            return slot;
        }
        ch.InventoryItemIds.Add(itemId);
        return ch.InventoryItemIds.Count - 1;
    }

    /// <summary>Empty one slot in place — later items keep their slots. Trailing empty slots are dropped.</summary>
    public static void ClearSlot(Character ch, int slot)
    {
        ArgumentNullException.ThrowIfNull(ch);
        ch.InventoryItemIds[slot] = 0;
        TrimTrailingEmpty(ch.InventoryItemIds);
    }

    /// <summary>Drop empty slots after the last held item (they are the same nine words on disk).</summary>
    public static void TrimTrailingEmpty(List<int> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        while (slots.Count > 0 && slots[^1] == 0)
            slots.RemoveAt(slots.Count - 1);
    }
}
