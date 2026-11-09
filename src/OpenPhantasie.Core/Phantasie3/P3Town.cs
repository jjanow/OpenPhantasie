namespace OpenPhantasie.Core;

/// <summary>Why a Trade Items move did nothing.</summary>
public enum P3TradeResult
{
    Done,

    /// <summary>The save plays DOS III (<c>p3.trade</c> = <c>dos</c>): there is no Trade; items move through Distribute.</summary>
    NoTrade,

    /// <summary>No item at that slot.</summary>
    NoSuchItem,

    /// <summary>`core.p3.cant_carry_anymore` (string 455): the receiver has 9 items.</summary>
    ReceiverFull,

    /// <summary>The giver and the receiver are the same member.</summary>
    SameMember,
}

/// <summary>
/// Phantasie III's town rules (<c>town.md</c>, <c>elementals.md</c>): the bank (D3-025), the found-item limit (D3-020), Trade
/// Items (D3-027), the Guild roster size (D3-029) and the Armory restock (D3-033, <see cref="P3ArmoryRestock"/>). The gold cap (D3-026) is <see cref="GameOptions.GoldCap"/>; autosave
/// on leaving town (D3-028) is <see cref="P3RuleOptionsExtensions.P3AutoSaveOnLeavingTown"/>.
/// </summary>
public static class P3Town
{
    /// <summary>D3-020 manual: "The party can carry up to 90 items that they find while exploring."</summary>
    public const int ManualFoundItemLimit = 90;

    /// <summary>D3-020 DOS: the found-item list holds 60 ids (<c>FAR:0x960</c>); a DOS save holds 60 too.</summary>
    public const int DosFoundItemLimit = 60;

    /// <summary>D3-029 DOS: 20 roster records, the three elementals in slots 0–2, so 17 characters.</summary>
    public const int DosRosterCharacters = 17;

    /// <summary>D3-026 manual: the party's gold and each account hold at most 65,000 (III's rules profile carries it).</summary>
    public const long ManualGoldCap = 65000;

    /// <summary>The receiver's item slots (record <c>+0x96</c>, 9 ids).</summary>
    public const int CarriedSlots = 9;

    /// <summary>
    /// D3-025 (user s89): the manual's "small banking fee … at every deposit" is Apple II's share maths (town lines
    /// 600–695): each account gets <c>⌊S · gold / (ΣS + 0.00001)⌋</c>, so the 0.00001 makes every share fall just short
    /// (about 1 gold per member with shares) and any remainder is lost too. 600 gold, 6 × 1 share → 99 each.
    /// </summary>
    public static long AppleIiAccountShare(long gold, int totalShares, int shares) =>
        (long)Math.Floor(shares * (gold / (totalShares + 0.00001)));

    /// <summary>"Each share is … gold" (Apple II prints <c>gold / (ΣS + 0.00001)</c>, shown whole here).</summary>
    public static long AppleIiShareValue(long gold, int totalShares) =>
        (long)Math.Floor(gold / (totalShares + 0.00001));

    /// <summary>
    /// Adds a found item to the party's list unless it is full (D3-020: 90, unlimited, or DOS's 60); false = "The party
    /// can't carry any more items." and the item is lost (root <c>0xf421</c>).
    /// </summary>
    public static bool TryAddFoundItem(List<int> found, int itemId, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.P3FoundItemLimit() is { } limit && found.Count >= limit)
            return false;
        found.Add(itemId);
        return true;
    }

    /// <summary>
    /// Guild New Member has room (D3-029): no limit (manual "dozens"), or DOS's 17 characters; the elementals never count.
    /// </summary>
    public static bool RosterHasRoom(int characters, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.P3RosterCharacterLimit() is not { } limit || characters < limit;
    }

    // ---- Bank (D3-025) -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The manual's Deposit: the party's gold is split by 1 / 2 / 3 shares per adventurer and the share value shown
    /// (Phantasie I's checks and messages, <see cref="TownMenus.DepositShares(Party, IReadOnlyList{int})"/>). <c>p3.bank</c> =
    /// <c>manual</c> pays by Apple II's maths (<see cref="AppleIiAccountShare"/>, the fee); <c>no-fee</c> by Phantasie I's
    /// (<c>S · ⌊gold / ΣS⌋</c>). Refused under DOS's bank.
    /// </summary>
    public static TownTransactionResult DepositShares(Party party, IReadOnlyList<int> shares, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.P3DosBank())
            return new TownTransactionResult { Kind = TownTransactionKind.Failed };
        return rules.P3NoDepositFee()
            ? TownMenus.DepositShares(party, shares)
            : TownMenus.DepositShares(party, shares, AppleIiAccountShare, AppleIiShareValue);
    }

    /// <summary>The manual's Withdraw: one adventurer takes gold from his own account (<see cref="TownMenus.Withdraw"/>).</summary>
    public static TownTransactionResult Withdraw(Party party, Character member, long amount, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.P3DosBank()
            ? new TownTransactionResult { Kind = TownTransactionKind.Failed }
            : TownMenus.Withdraw(party, member, amount);
    }

    /// <summary>
    /// DOS's `p3town.bank_deposit_some` (ovl2 <c>0x297a</c>, <c>p3.bank</c> = <c>dos</c>): <c>0 &lt; amount ≤ party gold</c>; the
    /// remainder <c>amount mod party size</c> goes 1 gold at a time to random living members (root <c>0x1124</c>, here a
    /// uniform <c>rnd</c> over the members not Dead <c>[~]</c>), then every member gets <c>amount / party size</c>. No fee,
    /// no shares. Returns false (nothing changes) for a bad amount or the manual's bank.
    /// </summary>
    public static bool DosDeposit(Party party, long amount, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rules);
        var n = party.Members.Count;
        if (!rules.P3DosBank() || n == 0 || amount <= 0 || amount > party.GoldCarried)
            return false;
        party.GoldCarried -= amount;
        var r = amount % n;
        var each = (amount - r) / n;
        var living = party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList();
        if (living.Count == 0) living = party.Members.ToList();
        for (var i = 0; i < r; i++)
            living[P3Dice.Rnd(rng, living.Count) - 1].AddBankGold(1, party.BankGoldCap);
        foreach (var m in party.Members)
            m.AddBankGold(each, party.BankGoldCap);
        return true;
    }

    /// <summary>DOS's `p3town.bank_withdraw_all`: every account is emptied into the party's gold (the excess over the purse cap is lost).</summary>
    public static bool DosWithdrawAll(Party party, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rules);
        if (!rules.P3DosBank()) return false;
        long sum = 0;
        foreach (var m in party.Members)
        {
            sum += m.GoldInBank;
            m.GoldInBank = 0;
        }
        party.AddGold(sum);
        return true;
    }

    // ---- Trade Items (D3-027) ------------------------------------------------------------------------------------------

    /// <summary>
    /// Trade Items, give (manual, as the Amiga program): the item at <paramref name="slot"/> of <paramref name="giver"/>'s
    /// carried list moves to <paramref name="receiver"/>'s (the end of the list, or an empty slot the remaster's inventory
    /// left), unless the receiver has 9 items; the giver's list closes up. Both re-ready their worn equipment with
    /// <paramref name="ready"/> (the session passes III's ovl2 <c>0x37f1</c>; null = I's selector); the giver first stops
    /// wearing the item when no copy of it is left (the Amiga Trade's re-ready is not read `[~]`). Refused with
    /// <see cref="P3TradeResult.NoTrade"/> under <c>p3.trade</c> = <c>dos</c>.
    /// </summary>
    public static P3TradeResult Give(
        Character giver, int slot, Character receiver, GameOptions rules, Action<Character>? ready = null)
    {
        ArgumentNullException.ThrowIfNull(giver);
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.P3NoTrade()) return P3TradeResult.NoTrade;
        if (ReferenceEquals(giver, receiver)) return P3TradeResult.SameMember;
        var carried = giver.InventoryItemIds;
        if (slot < 0 || slot >= carried.Count || carried[slot] == 0) return P3TradeResult.NoSuchItem;
        if (CarriedItems.IsFull(receiver)) return P3TradeResult.ReceiverFull;
        var id = carried[slot];
        CarriedItems.Put(receiver, id);
        carried.RemoveAt(slot);
        ready ??= CombatGear.SelectWornItems;
        StopWearingGone(giver, id);
        ready(giver);
        ready(receiver);
        return P3TradeResult.Done;
    }

    /// <summary>A worn field still naming <paramref name="itemId"/> after its last carried copy left is emptied.</summary>
    private static void StopWearingGone(Character ch, int itemId)
    {
        if (itemId <= 0 || ch.InventoryItemIds.Contains(itemId))
            return;
        if (ch.WornShield == itemId) ch.WornShield = 0;
        if (ch.WornArmor == itemId) ch.WornArmor = 0;
        if (ch.WornWeapon == itemId) ch.WornWeapon = 0;
        if (ch.WornBow == itemId) ch.WornBow = 0;
        if (ch.WornThrown == itemId) ch.WornThrown = 0;
    }

    /// <summary>
    /// The sale price (D3-027, user s89: Distribute's, ovl2 <c>0xa8f</c>): <c>⌊⌊value · index / 100⌋ · 75 / 100⌋</c>,
    /// at least 1, i.e. 75 % of the town's buying price.
    /// </summary>
    public static long SalePrice(long value, int priceIndex) => Math.Max(1, value * priceIndex / 100 * 75 / 100);

    /// <summary>
    /// Trade Items, sell ("trading it to adventurer 7"): the item leaves the list, the town's stock of it goes up by 1 (as a
    /// Distribute sale, <c>town.md</c> §7, capped at <see cref="ItemDistribution.MaxStock"/> as the shared Distribute) and
    /// <see cref="SalePrice"/> goes to the party's gold. Refused under <c>p3.trade</c> = <c>dos</c> (no Trade) and
    /// <c>give-only</c> (the Amiga's Trade). The seller re-readies worn equipment (<paramref name="ready"/>, as
    /// <see cref="Give"/>).
    /// </summary>
    public static P3TradeResult Sell(
        Party party, Character giver, int slot, TownState town, IItemCatalog items, GameOptions rules,
        Action<Character>? ready = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(giver);
        ArgumentNullException.ThrowIfNull(town);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.P3NoTrade() || rules.P3TradeGiveOnly()) return P3TradeResult.NoTrade;
        var carried = giver.InventoryItemIds;
        if (slot < 0 || slot >= carried.Count || carried[slot] == 0) return P3TradeResult.NoSuchItem;
        var id = carried[slot];
        carried.RemoveAt(slot);
        StopWearingGone(giver, id);
        (ready ?? CombatGear.SelectWornItems)(giver);
        if (id > 0 && id < TownState.StockSlotCount)
            town.SetStock(id, Math.Min(ItemDistribution.MaxStock, town.GetStock(id) + 1));
        party.AddGold(SalePrice(items.Get(id)?.Value ?? 0, town.PriceIndex));
        return P3TradeResult.Done;
    }
}
