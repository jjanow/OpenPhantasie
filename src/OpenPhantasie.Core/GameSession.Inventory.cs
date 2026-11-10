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
        Rewear(Gate(() => InventoryActions.TryEquip(Party, member, carriedSlot, Equipment, ItemGate)), member);

    public InventoryRefusal CanEquipFromStash(int stashSlot, Character member) =>
        Gate(() => InventoryActions.CanEquipFromStash(Party, Sack, stashSlot, member, Equipment, ItemGate));

    public InventoryRefusal TryEquipFromStash(int stashSlot, Character member) =>
        Rewear(Gate(() => InventoryActions.TryEquipFromStash(Party, Sack, stashSlot, member, Equipment, ItemGate)), member);

    public InventoryRefusal CanUnequip(Character member, string slotId) =>
        Gate(() => InventoryActions.CanUnequip(Party, member, slotId, Equipment));

    public InventoryRefusal TryUnequip(Character member, string slotId) =>
        Rewear(Gate(() => InventoryActions.TryUnequip(Party, member, slotId, Equipment)), member);

    public InventoryRefusal CanTransferItem(Character from, int carriedSlot, Character to) =>
        Gate(() => InventoryActions.CanTransfer(Party, from, carriedSlot, to));

    public InventoryRefusal TryTransferItem(Character from, int carriedSlot, Character to)
    {
        var id = InventoryActions.CarriedAt(from, carriedSlot);
        var r = Rewear(Gate(() => InventoryActions.TryTransfer(Party, from, carriedSlot, to, Equipment)), from);
        if (r == InventoryRefusal.None)
            WearArrival(to, id);
        return r;
    }

    public InventoryRefusal CanPutInStash(Character member, int carriedSlot) =>
        Gate(() => InventoryActions.CanToStash(Party, Sack, member, carriedSlot));

    public InventoryRefusal TryPutInStash(Character member, int carriedSlot) =>
        Rewear(Gate(() => InventoryActions.TryToStash(Party, Sack, member, carriedSlot, Equipment)), member);

    public InventoryRefusal CanTakeFromStash(int stashSlot, Character member) =>
        Gate(() => InventoryActions.CanFromStash(Party, Sack, stashSlot, member));

    public InventoryRefusal TryTakeFromStash(int stashSlot, Character member)
    {
        var id = (uint)stashSlot < (uint)Sack.ItemIds.Count ? Sack.ItemIds[stashSlot] : 0;
        var r = Gate(() => InventoryActions.TryFromStash(Party, Sack, stashSlot, member));
        if (r == InventoryRefusal.None)
            WearArrival(member, id);
        return r;
    }

    /// <summary>
    /// QoL <c>modern-inventory</c> (<c>inventory-actions.md</c> section 7): a new item <paramref name="itemId"/> that has just
    /// reached <paramref name="member"/> (a give, the stash, the Armory) is worn only when its slot holds nothing and the
    /// member can use it. Nothing else changes: worn items are never swapped, a slot the player emptied stays empty until
    /// a new item arrives for it. Off: nothing (the caller runs the game's own rule).
    /// </summary>
    internal void WearArrival(Character member, int itemId)
    {
        if (!Options.ModernInventory)
            return;
        if (InventoryActions.WearIfSlotEmpty(member, itemId, Equipment, ItemGate))
            CombatGear.ApplyWornItemPoints(member, ItemPoints);
    }

    /// <summary>
    /// QoL <c>modern-inventory</c>: can the item in <paramref name="member"/>'s carried slot be sold here? Only in a town
    /// (<see cref="GameMode.Town"/>), only with the option on, and not while the moves are blocked (<see cref="InventoryAccess"/>).
    /// </summary>
    public InventoryRefusal CanSellItem(Character member, int carriedSlot) =>
        SellGate(() => InventoryActions.CarriedAt(member, carriedSlot) == 0 ? InventoryRefusal.NoItem : InventoryRefusal.None, member);

    /// <summary>The same for the stash item in <paramref name="stashSlot"/>.</summary>
    public InventoryRefusal CanSellStashItem(int stashSlot) =>
        SellGate(() => (uint)stashSlot < (uint)Sack.ItemIds.Count && Sack.ItemIds[stashSlot] != 0
            ? InventoryRefusal.None : InventoryRefusal.NoItem, null);

    /// <summary>
    /// What selling <paramref name="itemId"/> pays here: the game's Distribute SELL price (<see cref="ICampaign.DistributeSalePrice"/>;
    /// I / II <see cref="ItemRules.SellPrice"/>, III 75 % of the town's price). 0 outside a town.
    /// </summary>
    public long SellPriceOf(int itemId)
    {
        if (Mode != GameMode.Town || CurrentTown is not { } town || itemId <= 0)
            return 0;
        return Campaign.DistributeSalePrice(town, _items)?.Invoke(itemId)
            ?? ItemRules.SellPrice(_items.Get(itemId)?.ValueFactor ?? 0);
    }

    /// <summary>
    /// Sell the item in <paramref name="member"/>'s carried slot (the Distribute SELL row as an inventory move): it leaves
    /// the member (stops being worn), the town's stock of it rises by 1 (at most <see cref="ItemDistribution.MaxStock"/>)
    /// and <see cref="SellPriceOf"/> goes to the party's gold.
    /// </summary>
    public InventoryRefusal TrySellItem(Character member, int carriedSlot)
    {
        var r = CanSellItem(member, carriedSlot);
        if (r != InventoryRefusal.None)
            return r;
        var id = InventoryActions.CarriedAt(member, carriedSlot);
        InventoryActions.TakeCarriedItem(member, carriedSlot, Equipment);
        CombatGear.ApplyWornItemPoints(member, ItemPoints);
        PaySale(id);
        return r;
    }

    /// <summary>Sell the stash item in <paramref name="stashSlot"/> (as <see cref="TrySellItem"/>).</summary>
    public InventoryRefusal TrySellStashItem(int stashSlot)
    {
        var r = CanSellStashItem(stashSlot);
        if (r != InventoryRefusal.None)
            return r;
        var id = Sack.ItemIds[stashSlot];
        InventoryActions.TakeStashItem(Sack, stashSlot); // the stash keeps its holes (LootSack)
        PaySale(id);
        return r;
    }

    private void PaySale(int id)
    {
        var gold = SellPriceOf(id);
        var town = RequireTownState();
        if (id < TownState.StockSlotCount)
            town.SetStock(id, Math.Min(ItemDistribution.MaxStock, town.GetStock(id) + 1));
        Party.AddGold(gold); // D-048: over the cap is lost (the Distribute SELL row says the price too)
        Note(GameText.F("core.distribute.x_sold_for_x_gp", _items.Get(id)?.Name ?? $"#{id}", gold));
        SweepTreasury(); // s175: a sale's gold
    }

    private InventoryRefusal SellGate(Func<InventoryRefusal> item, Character? member)
    {
        if (!Options.ModernInventory || Mode != GameMode.Town || CurrentTown is null)
            return InventoryRefusal.NoShop;
        var access = InventoryAccess;
        if (access != InventoryRefusal.None)
            return access;
        if (member is not null && !Party.Adventurers.Contains(member))
            return InventoryRefusal.NotInParty;
        return item();
    }

    /// <summary>
    /// s174 (D3-073, the manual, III PDF 6): the Armory list's point column for <paramref name="itemId"/> as
    /// <paramref name="shopper"/> sees it: the item's points when they can use it, UNUSABLE when not; empty for an item
    /// that isn't a weapon, bow, shield or armour (potions, scrolls, treasure). Null when the game's list has no such
    /// column (<see cref="ICampaign.ArmoryListsPoints"/>). DOS prints "N/A" for both an unusable item and every item with
    /// no requirement (Cloth +1, Robes +1, Self Bow included); the remaster follows the manual.
    /// </summary>
    public string? ArmoryPointsText(Character shopper, int itemId)
    {
        if (!Campaign.ArmoryListsPoints)
            return null;
        if (Items.Get(itemId) is not { Kind: "weapon" or "bow" or "shield" or "armor" })
            return "";
        return CanUseItem(shopper, itemId)
            ? Campaign.ItemPoints(this, itemId).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : GameText.S("core.armory.unusable");
    }

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

    /// <summary>
    /// <paramref name="itemId"/>'s point value by this game's rules (<see cref="ICampaign.ItemPoints"/>: I / II the id
    /// formulas, III its table); 0 for no item.
    /// </summary>
    public int ItemPoints(int itemId) => itemId > 0 ? Campaign.ItemPoints(this, itemId) : 0;

    /// <summary><paramref name="member"/>'s gear numbers as this game's combat reads them (<see cref="ICampaign.GearStatsOf"/>).</summary>
    public GearStats GearStatsOf(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Campaign.GearStatsOf(this, member);
    }

    /// <summary>This game's best-gear pick for <paramref name="member"/> (<see cref="ICampaign.ReadyBestEquipment"/>).</summary>
    public void ReadyBestEquipment(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        Campaign.ReadyBestEquipment(this, member);
    }

    /// <summary>
    /// The hand-out screen's worn-item point columns for <paramref name="member"/> in this game's order
    /// (<see cref="ICampaign.HandOutWornColumns"/>: III's SH AR BW WP), or null for I's WPN ARM SHL row.
    /// </summary>
    public IReadOnlyList<int>? HandOutWornPoints(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Campaign.HandOutWornColumns(this)?.Select(slot => ItemPoints(WornItems.Get(member, slot))).ToArray();
    }

    /// <summary>This game's gate, point values and best-gear pick, for the static helpers (hand-out, Trade).</summary>
    public GearRules Gear => new(ItemGate, ItemPoints, ReadyBestEquipment);

    /// <summary>
    /// What wearing <paramref name="itemId"/> would change for <paramref name="member"/> (read-only, any time), in this
    /// game's numbers (<see cref="GearStatsOf"/>).
    /// </summary>
    public GearComparison? CompareGear(Character member, int itemId) =>
        InventoryActions.Compare(member, itemId, Equipment, m =>
        {
            CombatGear.ApplyWornItemPoints(m, ItemPoints);
            return GearStatsOf(m);
        });

    /// <summary>
    /// After a move: the worn fields of <paramref name="members"/> hold this game's points (the moves write I's; for I / II
    /// this rewrites the same values).
    /// </summary>
    private InventoryRefusal Rewear(InventoryRefusal result, params Character[] members)
    {
        if (result == InventoryRefusal.None)
            foreach (var m in members)
                CombatGear.ApplyWornItemPoints(m, ItemPoints);
        return result;
    }

    private InventoryRefusal Gate(Func<InventoryRefusal> move)
    {
        var access = InventoryAccess;
        return access != InventoryRefusal.None ? access : move();
    }
}
