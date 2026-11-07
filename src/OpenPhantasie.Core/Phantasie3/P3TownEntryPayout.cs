namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's town-entry payout (D3-071, manual "the experience will be divided among the adventurers when they reach
/// a town"; DOS root <c>0xa911</c> → ovl2 <c>0x0000</c>(1), read s143 / s144 <c>[C]</c> static, <c>logic-specs/town.md</c>
/// section 7). Runs only when the XP pool or the found list is not empty. In DOS order: every character's best equipment
/// readied (<see cref="ReadyBestEquipment"/>), the wand taken out of the found list (<see cref="DropFoundItem"/>), the found
/// items' hand-out screen, then "Shares for NAME :" for every character (<see cref="ShareOptions"/>) and the split
/// (<see cref="ShareOut"/>). The loops run over the characters only (<c>FAR:0xb614</c>): a loaded elemental is never
/// readied, asked or paid.
/// </summary>
public static class P3TownEntryPayout
{
    /// <summary>The share counts a dead member may be given (DOS <c>0x1f8</c>: 0–3; the field starts at "0").</summary>
    public static IReadOnlyList<string> DeadShareOptions { get; } = ["0", "1", "2", "3"];

    /// <summary>The share counts any other member may be given (DOS <c>0x1a0</c>: 1–3; any other digit asks again).</summary>
    public static IReadOnlyList<string> LivingShareOptions { get; } = ["1", "2", "3"];

    /// <summary>
    /// DOS ovl2 <c>0x37f1</c> ("ready best equipment") <c>[C]</c>: for each carried item in list order that the character can
    /// use (<see cref="CanUse"/>) and that a worn slot takes, the item is worn when its points are ≥ the worn item's
    /// (so a tie goes to the later item). A slot is never emptied: a worn item no longer carried or no longer usable stays
    /// until something replaces it. Slots and points come from data (<c>rules.json</c> <c>equipment_slots</c>,
    /// <c>items.json</c> <c>points</c> / <c>min_str</c> / <c>str_check</c>).
    /// </summary>
    public static void ReadyBestEquipment(Character ch, IItemCatalog items, EquipmentLayout slots)
    {
        ArgumentNullException.ThrowIfNull(ch);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(slots);
        int Points(int id) => id > 0 ? items.Get(id)?.Points ?? 0 : 0;
        foreach (var id in ch.InventoryItemIds)
        {
            if (id <= 0 || slots.SlotFor(id) is not { } slot || items.Get(id) is not { } item || !CanUse(item, ch.Stats))
                continue;
            if (Points(id) >= Points(WornItems.Get(ch, slot.Id)))
                WornItems.Set(ch, slot.Id, id);
        }
        CombatGear.ApplyWornItemPoints(ch); // as III's creation kit does (Phantasie3Campaign.RollNewMember)
    }

    /// <summary>
    /// III's usability test (ovl2 <c>0x39b5</c>, <c>FORMAT.md</c> section 4.1) <c>[C]</c>: the attribute sum named by
    /// <see cref="ItemCatalogEntry.StrCheck"/> (STR; STR + DEX; 2·STR + DEX) ≥ <see cref="ItemCatalogEntry.Requirement"/>.
    /// </summary>
    public static bool CanUse(ItemCatalogEntry item, CharacterStats stats)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(stats);
        var sum = item.StrCheck switch
        {
            "str" => stats.Str,
            "str_plus_dex" => stats.Str + stats.Dex,
            "str2_plus_dex" => 2 * stats.Str + stats.Dex,
            _ => 0,
        };
        return sum >= (item.Requirement ?? 0);
    }

    /// <summary>
    /// DOS ovl2 <c>0x5a9</c> <c>[C]</c>: the last found item with id <paramref name="itemId"/> leaves the found list for good
    /// (the list closes up; no quest bit is set, nothing else reads the freed slot). One per call. False when there is none.
    /// </summary>
    public static bool DropFoundItem(LootSack sack, int itemId)
    {
        ArgumentNullException.ThrowIfNull(sack);
        var at = sack.ItemIds.LastIndexOf(itemId);
        if (at < 0)
            return false;
        sack.ItemIds.RemoveAt(at);
        return true;
    }

    /// <summary>The share counts <paramref name="member"/>'s prompt offers (dead 0–3, any other status 1–3).</summary>
    public static IReadOnlyList<string> ShareOptions(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Status == CharacterStatus.Dead ? DeadShareOptions : LivingShareOptions;
    }

    /// <summary>
    /// The share count the prompt starts on (DOS pre-fills the field): a dead member 0; any other member its stored
    /// <see cref="Phantasie3CharacterData.ExperienceShares"/>, 1 when that is 0 or less (ovl2 <c>0x2a8</c>), at most 3.
    /// </summary>
    public static int DefaultShares(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.Status == CharacterStatus.Dead)
            return 0;
        var stored = member.Phantasie3?.ExperienceShares ?? 1;
        return stored <= 0 ? 1 : Math.Min(stored, 3);
    }

    /// <summary>
    /// DOS ovl2 <c>0x0466…0x059b</c> <c>[C]</c>: <c>r = pool mod total shares</c> (MS C long remainder <c>0x3b2:0x23da</c>);
    /// <c>r</c> times one point goes to the character <c>rnd(characters)</c> (any status, dead too); then each character gets
    /// <c>shares × ((pool − r) ÷ total)</c> (long divide <c>0x3b2:0x2300</c>), so the whole pool is paid; the pool is emptied.
    /// Each member's answer is stored as its <see cref="Phantasie3CharacterData.ExperienceShares"/>.
    /// </summary>
    public static void ShareOut(IReadOnlyList<Character> characters, IReadOnlyList<int> shares, LootSack sack, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(shares);
        ArgumentNullException.ThrowIfNull(sack);
        ArgumentNullException.ThrowIfNull(rng);
        if (shares.Count != characters.Count)
            throw new ArgumentException("One share count per character.", nameof(shares));
        for (var i = 0; i < characters.Count; i++)
            if (!ShareOptions(characters[i]).Contains(shares[i].ToString(System.Globalization.CultureInfo.InvariantCulture)))
                throw new ArgumentException($"Shares for {characters[i].Name}: {shares[i]} is not offered.", nameof(shares));
        var total = shares.Sum();
        if (total <= 0)
            throw new ArgumentException("No shares at all.", nameof(shares)); // DOS would divide by zero
        for (var i = 0; i < characters.Count; i++)
            characters[i].P3.ExperienceShares = shares[i];
        var pool = sack.Experience;
        var remainder = pool % total;
        for (var n = 0; n < remainder; n++)
            characters[P3Dice.Rnd(rng, characters.Count) - 1].Experience += 1;
        var each = (pool - remainder) / total;
        for (var i = 0; i < characters.Count; i++)
            characters[i].Experience = checked(characters[i].Experience + shares[i] * each);
        sack.Experience = 0;
    }
}
