namespace OpenPhantasie.Core;

/// <summary>Where the inventory moves can be used (<see cref="RuleOptionRegistry.StashAccess"/>).</summary>
public enum StashAccessRule
{
    /// <summary>The original: in town only (manual p.15, D)ISTRIBUTE is a Town-menu command).</summary>
    Town,

    /// <summary>QoL: anywhere with a party outside combat.</summary>
    Anywhere,
}

/// <summary>Why an inventory move is refused; <see cref="InventoryActions.ReasonKey"/> gives its string id.</summary>
public enum InventoryRefusal
{
    None,
    NotHere,
    NotInParty,
    NoItem,
    NotEquipment,
    Unusable,
    CarryFull,
    StashFull,
    SameMember,
    NothingWorn,
}

/// <summary>A member's gear numbers as combat uses them (<see cref="InventoryActions.Compare"/>).</summary>
public readonly record struct GearStats(int ShieldSoak, int ArmorSoak, int WeaponDamage, int DefenseRating);

/// <summary>What wearing an item would change: the slot it goes in, the item it replaces (0 = none), and the numbers.</summary>
public sealed record GearComparison(EquipmentSlot Slot, int ReplacedItemId, GearStats Before, GearStats After);

/// <summary>
/// The inventory rework's moves (09 T2, <c>reverse-engineering/dos/logic-specs/inventory-actions.md</c>): wear an item from a
/// member's carried slots or the party stash (the found-item <see cref="LootSack"/>), take it off, hand it to another member,
/// put it in or take it from the stash. Every move has a <c>CanX</c> check giving an <see cref="InventoryRefusal"/>; the
/// matching <c>TryX</c> runs the same check and then does it.
/// <para>
/// Worn items are carried items the worn fields point at (DOS OV1 <c>0x2fdb</c>), so they count toward the 9. The slot an
/// item is worn in comes from the game's <see cref="EquipmentLayout"/>. Where the moves are allowed is not checked here: see
/// <see cref="StashAccessible"/> and the <see cref="GameSession"/> wrappers.
/// </para>
/// </summary>
public static class InventoryActions
{
    /// <summary>The string-table id of <paramref name="refusal"/> (<c>inventory.refuse.carry_full</c> …); empty for None.</summary>
    public static string ReasonKey(InventoryRefusal refusal) => refusal switch
    {
        InventoryRefusal.None => "",
        InventoryRefusal.NotHere => "inventory.refuse.not_here",
        InventoryRefusal.NotInParty => "inventory.refuse.not_in_party",
        InventoryRefusal.NoItem => "inventory.refuse.no_item",
        InventoryRefusal.NotEquipment => "inventory.refuse.not_equipment",
        InventoryRefusal.Unusable => "inventory.refuse.unusable",
        InventoryRefusal.CarryFull => "inventory.refuse.carry_full",
        InventoryRefusal.StashFull => "inventory.refuse.stash_full",
        InventoryRefusal.SameMember => "inventory.refuse.same_member",
        InventoryRefusal.NothingWorn => "inventory.refuse.nothing_worn",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal)),
    };

    /// <summary>Every reason id <see cref="ReasonKey"/> can return (for the Client's string table).</summary>
    public static IReadOnlyList<string> ReasonKeys { get; } =
        Enum.GetValues<InventoryRefusal>().Where(r => r != InventoryRefusal.None).Select(ReasonKey).ToArray();

    // --- where ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// True when the moves may be used in <paramref name="mode"/> (<c>inventory-actions.md</c> §4): <see cref="GameMode.Town"/>
    /// and the town's <see cref="GameMode.Guild"/> (user, s75) under <see cref="StashAccessRule.Town"/>; any mode but Title and
    /// Combat under <see cref="StashAccessRule.Anywhere"/>.
    /// The session adds its own blocks (an open D)ISTRIBUTE screen, a waiting treasure).
    /// </summary>
    public static bool StashAccessible(GameMode mode, StashAccessRule rule) => rule switch
    {
        StashAccessRule.Town => mode is GameMode.Town or GameMode.Guild,
        _ => mode is not (GameMode.Title or GameMode.Combat),
    };

    // --- what -------------------------------------------------------------------------------------------------------

    /// <summary>The stash's items with their slot index (holes skipped), for listing; slots are what the moves take.</summary>
    public static IReadOnlyList<(int Slot, int ItemId)> StashItems(LootSack sack)
    {
        ArgumentNullException.ThrowIfNull(sack);
        return sack.ItemIds.Select((id, i) => (i, id)).Where(x => x.id != 0).ToArray();
    }

    /// <summary>The worn item of <paramref name="slot"/> (0 = none).</summary>
    public static int WornIn(Character member, EquipmentSlot slot) => WornItems.Get(member, slot.Id);

    /// <summary>True when the carried item in <paramref name="carriedSlot"/> is one the member wears.</summary>
    public static bool IsWorn(Character member, int carriedSlot, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(layout);
        if ((uint)carriedSlot >= (uint)member.InventoryItemIds.Count || member.InventoryItemIds[carriedSlot] is not (var id and > 0))
            return false;
        if (layout.SlotFor(id) is not { } slot || WornItems.Get(member, slot.Id) != id)
            return false;
        // With copies of one id, the first carried copy is the worn one.
        return member.InventoryItemIds.IndexOf(id) == carriedSlot;
    }

    /// <summary>Can <paramref name="member"/> wear <paramref name="itemId"/> at all (a slot takes it and the gate passes)?</summary>
    public static InventoryRefusal CanWear(Character member, int itemId, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(layout);
        if (itemId <= 0)
            return InventoryRefusal.NoItem;
        if (layout.SlotFor(itemId) is null)
            return InventoryRefusal.NotEquipment;
        return ItemRules.IsUsable(itemId, member.Stats) ? InventoryRefusal.None : InventoryRefusal.Unusable;
    }

    // --- Equip ------------------------------------------------------------------------------------------------------

    /// <summary>Wear the item in the member's carried slot <paramref name="carriedSlot"/>.</summary>
    public static InventoryRefusal CanEquip(Party party, Character member, int carriedSlot, EquipmentLayout layout)
    {
        var r = Member(party, member);
        if (r != InventoryRefusal.None)
            return r;
        return CanWear(member, Carried(member, carriedSlot), layout);
    }

    public static InventoryRefusal TryEquip(Party party, Character member, int carriedSlot, EquipmentLayout layout)
    {
        var r = CanEquip(party, member, carriedSlot, layout);
        if (r != InventoryRefusal.None)
            return r;
        var id = member.InventoryItemIds[carriedSlot];
        WornItems.Set(member, layout.SlotFor(id)!.Id, id); // the old worn item stays carried
        CombatGear.ApplyWornItemPoints(member);
        return r;
    }

    /// <summary>
    /// Wear the stash item in <paramref name="stashSlot"/>: it moves to a free carried slot; with 9 carried the item worn in
    /// that slot goes to the stash in its place.
    /// </summary>
    public static InventoryRefusal CanEquipFromStash(
        Party party, LootSack sack, int stashSlot, Character member, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(sack);
        var r = Member(party, member);
        if (r != InventoryRefusal.None)
            return r;
        var id = Stashed(sack, stashSlot);
        r = CanWear(member, id, layout);
        if (r != InventoryRefusal.None)
            return r;
        // 9 carried: only a swap with the item worn in that slot makes room.
        if (CarriedItems.IsFull(member)
            && WornItems.Get(member, layout.SlotFor(id)!.Id) is var worn && (worn == 0 || !member.InventoryItemIds.Contains(worn)))
            return InventoryRefusal.CarryFull;
        return InventoryRefusal.None;
    }

    public static InventoryRefusal TryEquipFromStash(
        Party party, LootSack sack, int stashSlot, Character member, EquipmentLayout layout)
    {
        var r = CanEquipFromStash(party, sack, stashSlot, member, layout);
        if (r != InventoryRefusal.None)
            return r;
        var id = sack.ItemIds[stashSlot];
        var slot = layout.SlotFor(id)!;
        if (CarriedItems.IsFull(member))
        {
            // Swap: the worn item takes the stash slot the new one leaves.
            var old = WornItems.Get(member, slot.Id);
            var oldSlot = member.InventoryItemIds.IndexOf(old);
            member.InventoryItemIds[oldSlot] = id;
            sack.ItemIds[stashSlot] = old;
        }
        else
        {
            TakeFromStash(sack, stashSlot);
            CarriedItems.Put(member, id);
        }
        WornItems.Set(member, slot.Id, id);
        CombatGear.ApplyWornItemPoints(member);
        return r;
    }

    // --- Unequip ----------------------------------------------------------------------------------------------------

    /// <summary>Stop wearing what is in slot <paramref name="slotId"/>; the item stays carried.</summary>
    public static InventoryRefusal CanUnequip(Party party, Character member, string slotId, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var r = Member(party, member);
        if (r != InventoryRefusal.None)
            return r;
        if (layout.Find(slotId) is null)
            return InventoryRefusal.NotEquipment;
        return WornItems.Get(member, slotId) == 0 ? InventoryRefusal.NothingWorn : InventoryRefusal.None;
    }

    public static InventoryRefusal TryUnequip(Party party, Character member, string slotId, EquipmentLayout layout)
    {
        var r = CanUnequip(party, member, slotId, layout);
        if (r != InventoryRefusal.None)
            return r;
        WornItems.Set(member, slotId, 0);
        CombatGear.ApplyWornItemPoints(member);
        return r;
    }

    // --- Transfer ---------------------------------------------------------------------------------------------------

    /// <summary>Hand the item in <paramref name="from"/>'s carried slot to <paramref name="to"/> (not worn there).</summary>
    public static InventoryRefusal CanTransfer(Party party, Character from, int carriedSlot, Character to)
    {
        var r = Member(party, from);
        if (r == InventoryRefusal.None)
            r = Member(party, to);
        if (r != InventoryRefusal.None)
            return r;
        if (Carried(from, carriedSlot) == 0)
            return InventoryRefusal.NoItem;
        if (ReferenceEquals(from, to))
            return InventoryRefusal.SameMember;
        return CarriedItems.IsFull(to) ? InventoryRefusal.CarryFull : InventoryRefusal.None;
    }

    public static InventoryRefusal TryTransfer(
        Party party, Character from, int carriedSlot, Character to, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var r = CanTransfer(party, from, carriedSlot, to);
        if (r != InventoryRefusal.None)
            return r;
        var id = TakeCarried(from, carriedSlot, layout);
        CarriedItems.Put(to, id);
        return r;
    }

    // --- Stash ------------------------------------------------------------------------------------------------------

    /// <summary>Put the item in the member's carried slot into the stash's first empty slot.</summary>
    public static InventoryRefusal CanToStash(Party party, LootSack sack, Character member, int carriedSlot)
    {
        ArgumentNullException.ThrowIfNull(sack);
        var r = Member(party, member);
        if (r != InventoryRefusal.None)
            return r;
        if (Carried(member, carriedSlot) == 0)
            return InventoryRefusal.NoItem;
        return sack.IsFull ? InventoryRefusal.StashFull : InventoryRefusal.None;
    }

    public static InventoryRefusal TryToStash(
        Party party, LootSack sack, Character member, int carriedSlot, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var r = CanToStash(party, sack, member, carriedSlot);
        if (r != InventoryRefusal.None)
            return r;
        sack.TryAdd(TakeCarried(member, carriedSlot, layout));
        return r;
    }

    /// <summary>Give the stash item in <paramref name="stashSlot"/> to the member's first free carried slot (not worn).</summary>
    public static InventoryRefusal CanFromStash(Party party, LootSack sack, int stashSlot, Character member)
    {
        ArgumentNullException.ThrowIfNull(sack);
        var r = Member(party, member);
        if (r != InventoryRefusal.None)
            return r;
        if (Stashed(sack, stashSlot) == 0)
            return InventoryRefusal.NoItem;
        return CarriedItems.IsFull(member) ? InventoryRefusal.CarryFull : InventoryRefusal.None;
    }

    public static InventoryRefusal TryFromStash(Party party, LootSack sack, int stashSlot, Character member)
    {
        var r = CanFromStash(party, sack, stashSlot, member);
        if (r != InventoryRefusal.None)
            return r;
        CarriedItems.Put(member, TakeFromStash(sack, stashSlot));
        return r;
    }

    // --- Compare ----------------------------------------------------------------------------------------------------

    /// <summary>The member's gear numbers as the next fight would use them (<c>Combat.PrepFor</c>), without writing anything.</summary>
    public static GearStats Stats(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return new GearStats(
            member.ShieldSoak,
            member.ArmorSoak,
            member.WeaponDamageA + member.WeaponDamageB,
            CombatMath.ComputeDefenseRating(member.Stats.Dex, member.ArmorSoak, member.Skills.Parry, member.ShieldSoak));
    }

    /// <summary>
    /// What wearing <paramref name="itemId"/> would change for <paramref name="member"/>, worked out on a clone; null when no
    /// slot takes the item. Usability is not checked here (<see cref="CanWear"/>).
    /// </summary>
    public static GearComparison? Compare(Character member, int itemId, EquipmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.SlotFor(itemId) is not { } slot)
            return null;
        var probe = member.Clone();
        CombatGear.ApplyWornItemPoints(probe);
        var before = Stats(probe);
        var replaced = WornItems.Get(probe, slot.Id);
        WornItems.Set(probe, slot.Id, itemId);
        CombatGear.ApplyWornItemPoints(probe);
        return new GearComparison(slot, replaced, before, Stats(probe));
    }

    // --- helpers ----------------------------------------------------------------------------------------------------

    private static InventoryRefusal Member(Party party, Character member)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        return party.Members.Contains(member) && !ReferenceEquals(member, party.Elemental)
            ? InventoryRefusal.None
            : InventoryRefusal.NotInParty;
    }

    private static int Carried(Character member, int slot) =>
        (uint)slot < (uint)member.InventoryItemIds.Count ? member.InventoryItemIds[slot] : 0;

    private static int Stashed(LootSack sack, int slot) =>
        (uint)slot < (uint)sack.ItemIds.Count ? sack.ItemIds[slot] : 0;

    /// <summary>Empty a carried slot; a worn id the member no longer carries any copy of stops being worn.</summary>
    private static int TakeCarried(Character member, int slot, EquipmentLayout layout)
    {
        var id = member.InventoryItemIds[slot];
        CarriedItems.ClearSlot(member, slot);
        if (!member.InventoryItemIds.Contains(id) && layout.SlotFor(id) is { } worn && WornItems.Get(member, worn.Id) == id)
        {
            WornItems.Set(member, worn.Id, 0);
            CombatGear.ApplyWornItemPoints(member);
        }
        return id;
    }

    /// <summary>Empty a stash slot in place (the sack keeps its holes, [D✓] s65); trailing holes are dropped.</summary>
    private static int TakeFromStash(LootSack sack, int slot)
    {
        var id = sack.ItemIds[slot];
        sack.ItemIds[slot] = 0;
        while (sack.ItemIds.Count > 0 && sack.ItemIds[^1] == 0)
            sack.ItemIds.RemoveAt(sack.ItemIds.Count - 1);
        return id;
    }
}
