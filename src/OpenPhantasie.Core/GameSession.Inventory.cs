namespace OpenPhantasie.Core;

/// <summary>
/// The inventory rework's moves on this session's party and stash (09 T2, <see cref="InventoryActions"/>,
/// <c>inventory-actions.md</c>). Each <c>CanX</c>/<c>TryX</c> first checks <see cref="InventoryAccess"/> (where, from this
/// save's <see cref="GameOptions.StashAccess"/>), then the move itself. The stash is <see cref="Sack"/>; the worn-item slots
/// are <see cref="RulesProfile.Equipment"/>.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The worn-item slots of this game (<see cref="RulesProfile.Equipment"/>).</summary>
    public EquipmentLayout Equipment => Rules.Equipment;

    /// <summary>
    /// <see cref="InventoryRefusal.None"/> when the moves can be used now: the save's stash rule allows this
    /// <see cref="Mode"/>, there is a party, no D)ISTRIBUTE screen is open (its pool holds the items) and no found treasure
    /// waits for "KEEP IT?". Else <see cref="InventoryRefusal.NotHere"/>.
    /// </summary>
    public InventoryRefusal InventoryAccess =>
        InventoryActions.StashAccessible(Mode, Options.StashAccess)
        && Party.Members.Count > 0
        && ItemDistribution is null
        && _pendingTreasure.Count == 0
            ? InventoryRefusal.None
            : InventoryRefusal.NotHere;

    /// <summary>True when <see cref="InventoryAccess"/> allows the moves now.</summary>
    public bool StashAccessible => InventoryAccess == InventoryRefusal.None;

    public InventoryRefusal CanEquip(Character member, int carriedSlot) =>
        Gate(() => InventoryActions.CanEquip(Party, member, carriedSlot, Equipment, ItemGate));

    public InventoryRefusal TryEquip(Character member, int carriedSlot) =>
        Gate(() => InventoryActions.TryEquip(Party, member, carriedSlot, Equipment, ItemGate));

    public InventoryRefusal CanEquipFromStash(int stashSlot, Character member) =>
        Gate(() => InventoryActions.CanEquipFromStash(Party, Sack, stashSlot, member, Equipment, ItemGate));

    public InventoryRefusal TryEquipFromStash(int stashSlot, Character member) =>
        Gate(() => InventoryActions.TryEquipFromStash(Party, Sack, stashSlot, member, Equipment, ItemGate));

    public InventoryRefusal CanUnequip(Character member, string slotId) =>
        Gate(() => InventoryActions.CanUnequip(Party, member, slotId, Equipment));

    public InventoryRefusal TryUnequip(Character member, string slotId) =>
        Gate(() => InventoryActions.TryUnequip(Party, member, slotId, Equipment));

    public InventoryRefusal CanTransferItem(Character from, int carriedSlot, Character to) =>
        Gate(() => InventoryActions.CanTransfer(Party, from, carriedSlot, to));

    public InventoryRefusal TryTransferItem(Character from, int carriedSlot, Character to) =>
        Gate(() => InventoryActions.TryTransfer(Party, from, carriedSlot, to, Equipment));

    public InventoryRefusal CanPutInStash(Character member, int carriedSlot) =>
        Gate(() => InventoryActions.CanToStash(Party, Sack, member, carriedSlot));

    public InventoryRefusal TryPutInStash(Character member, int carriedSlot) =>
        Gate(() => InventoryActions.TryToStash(Party, Sack, member, carriedSlot, Equipment));

    public InventoryRefusal CanTakeFromStash(int stashSlot, Character member) =>
        Gate(() => InventoryActions.CanFromStash(Party, Sack, stashSlot, member));

    public InventoryRefusal TryTakeFromStash(int stashSlot, Character member) =>
        Gate(() => InventoryActions.TryFromStash(Party, Sack, stashSlot, member));

    /// <summary>
    /// Can <paramref name="member"/> use <paramref name="itemId"/> by this game's own gate (<see cref="ICampaign.CanUseItem"/>:
    /// I / II the id formulas, III its tables with bows on STR + DEX)? Read-only, any time; true for items no gate covers.
    /// </summary>
    public bool CanUseItem(Character member, int itemId)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Campaign.CanUseItem(this, member, itemId);
    }

    /// <summary>
    /// <see cref="InventoryActions.CanWear"/> by this game's gate: a slot takes <paramref name="itemId"/> and
    /// <paramref name="member"/> passes <see cref="CanUseItem"/>. Read-only, any time (no <see cref="InventoryAccess"/> check).
    /// </summary>
    public InventoryRefusal CanWear(Character member, int itemId) =>
        InventoryActions.CanWear(member, itemId, Equipment, ItemGate);

    private ItemGate ItemGate => CanUseItem;

    /// <summary>What wearing <paramref name="itemId"/> would change for <paramref name="member"/> (read-only, any time).</summary>
    public GearComparison? CompareGear(Character member, int itemId) => InventoryActions.Compare(member, itemId, Equipment);

    private InventoryRefusal Gate(Func<InventoryRefusal> move)
    {
        var access = InventoryAccess;
        return access != InventoryRefusal.None ? access : move();
    }
}
