namespace OpenPhantasie.Core;

/// <summary>
/// Shared post-combat loot pool (SACK.DAT semantics): XP/GP plus the found-item slots <see cref="ItemIds"/>, divided at
/// town entry. [D✓] s65: the slots keep DOS's holes (0 = empty) — a kept item fills the first empty slot and BEG FOR
/// MERCY empties slots 0, 2, …, 48 in place.
/// </summary>
public sealed class LootSack
{
    public const int MaxItems = 90;

    /// <summary>DOS's found-item list: 60 slots (<c>0x63fa</c>, SACK.DAT; D-018).</summary>
    public const int DosSlotCount = 60;

    /// <summary>
    /// Most items the sack holds: <see cref="MaxItems"/> (the manual's 90, D-018) or, when the save plays
    /// <c>p1.sack-size</c> = <c>dos</c>, <see cref="DosSlotCount"/>. A session sets it from its rules.
    /// </summary>
    public int Capacity { get; set; } = MaxItems;

    /// <summary>
    /// Most found gold the pool holds — the party purse cap (<see cref="RulesProfile.PartyGoldCap"/>; Phantasie I:
    /// 65,000, D-048), since found gold is carried. A session sets it from its rules and its gold cap option.
    /// </summary>
    public long GoldCap { get; set; } = RulesProfile.PhantasieI.PartyGoldCap;

    public long Experience { get; set; }
    public long Gold { get; set; }
    /// <summary>Found-item slots in order; 0 = an empty slot (a hole). Use <see cref="Items"/> for the items alone.</summary>
    public List<int> ItemIds { get; } = new();

    /// <summary>The found items (nonzero slots) in slot order.</summary>
    public IEnumerable<int> Items => ItemIds.Where(id => id != 0);

    public int ItemCount => ItemIds.Count(id => id != 0);

    /// <summary>
    /// The slots as they were when the party last left town (<see cref="MarkTownExit"/>); null = never marked (every item
    /// counts as new). III's Beg for Mercy takes only items found since then (D3-024).
    /// </summary>
    public int[]? TownExitItemIds { get; set; }

    /// <summary>Remember the slots on leaving town.</summary>
    public void MarkTownExit() => TownExitItemIds = ItemIds.ToArray();

    /// <summary>
    /// Slot <paramref name="slot"/> holds an item found since the party last left town: not empty and not the item that
    /// was there then (a find fills the first hole, and the slots keep their places outside town).
    /// </summary>
    public bool IsNewSinceTownExit(int slot) =>
        ItemIds[slot] != 0
        && (TownExitItemIds is not { } old || slot >= old.Length || old[slot] != ItemIds[slot]);

    public bool IsFull => ItemCount >= Capacity;

    /// <summary>Store <paramref name="id"/> in the first empty slot ([D✓] s65 DOSBox); false when the sack is full.</summary>
    public bool TryAdd(int id)
    {
        if (id <= 0 || IsFull)
            return false;
        var hole = ItemIds.IndexOf(0);
        if (hole >= 0)
            ItemIds[hole] = id;
        else
            ItemIds.Add(id);
        return true;
    }

    public bool IsEmpty => Experience == 0 && Gold == 0 && ItemCount == 0;

    public void Clear()
    {
        Experience = 0;
        Gold = 0;
        ItemIds.Clear();
        TownExitItemIds = null;
    }

    /// <summary>
    /// BEG FOR MERCY (OV2 <c>0x3bf8</c>): the found gold <c>[0x63f6]</c> goes to the monsters and found-item slots
    /// 0, 2, …, 48 are emptied in place ([D✓] s65: the holes stay, so a second BEG hits the same slots). Carried gold
    /// goes too — the caller clears the purse (one DOS purse).
    /// </summary>
    public void ForfeitToBeggars()
    {
        Gold = 0;
        for (var i = 0; i <= 48 && i < ItemIds.Count; i += 2)
            ItemIds[i] = 0;
        while (ItemIds.Count > 0 && ItemIds[^1] == 0)
            ItemIds.RemoveAt(ItemIds.Count - 1);
    }

    /// <summary>Add combat victory rewards into the pool (not yet distributed).</summary>
    public void DepositRewards(long xp, long gold, IEnumerable<int>? items = null)
    {
        // DOS pool [0x63f4] is in tens, capped at 0xffff (OV2 0x40b2); Core keeps whole XP (D-053), same cap.
        Experience = Math.Min(checked(Experience + xp), Math.Max(CombatEncounter.XpPoolCap, Experience));
        Gold = Math.Min(checked(Gold + gold), Math.Max(GoldCap, Gold)); // found gold is carried too (D-048)
        if (items is null) return;
        foreach (var id in items)
        {
            if (IsFull) break;
            TryAdd(id);
        }
    }
}
