namespace OpenPhantasie.Core;

/// <summary>Where a distributed item goes: SELL, SACK, or a party member (0-based slot).</summary>
public readonly record struct DistributeTarget(int Value)
{
    public static readonly DistributeTarget Sell = new(-2);
    public static readonly DistributeTarget Sack = new(-1);

    public static DistributeTarget Member(int slot) => new(slot);

    public bool IsMember => Value >= 0;
}

/// <summary>
/// The DOS item hand-out screen (OV1 <c>0x2ab3</c> → <c>0x2bed</c>, <c>town-menus.md</c> §Distribute) [C]. Every item the
/// party carries — worn ones included, DEAD members too — and every found item go into one pool; nothing stays worn. The
/// pool is sorted by item id, highest first, and each item must go to SELL (V²/8 gold, the Armory's stock +1), the SACK
/// or a member with room (then the worn-item selector runs). There is no cancel: the screen ends when the pool is empty.
/// </summary>
public sealed class ItemDistribution
{
    /// <summary>OV1 <c>0x2ef1</c>: a sold item raises the town's stock, at most to 99.</summary>
    public const int MaxStock = 99;

    private readonly Party _party;
    private readonly LootSack _sack;
    private readonly TownState _town;
    private readonly IItemCatalog _items;
    private readonly List<int> _pool;
    private readonly Func<int, long>? _salePrice;
    private readonly GearRules _gear;

    private ItemDistribution(
        Party party, LootSack sack, TownState town, IItemCatalog items, List<int> pool, Func<int, long>? salePrice,
        GearRules gear)
    {
        _party = party;
        _sack = sack;
        _town = town;
        _items = items;
        _pool = pool;
        _salePrice = salePrice;
        _gear = gear;
    }

    /// <summary>Items still to hand out, the current one first.</summary>
    public IReadOnlyList<int> Remaining => _pool;

    public int? Current => _pool.Count > 0 ? _pool[0] : null;

    public bool IsDone => _pool.Count == 0;

    /// <summary>
    /// Pools every carried and found item (OV1 <c>0x2ac9..0x2bdc</c>); null with no party. <paramref name="salePrice"/> is
    /// the game's SELL price by item id (null = Phantasie I's <c>V²/8</c>; III: <see cref="ICampaign.DistributeSalePrice"/>).
    /// <paramref name="gear"/>: the game's best-gear pick and point values (<see cref="GameSession.Gear"/>; null = I's).
    /// </summary>
    public static ItemDistribution? Begin(
        Party party, LootSack sack, TownState town, IItemCatalog items, Func<int, long>? salePrice = null,
        bool foundOnly = false, GearRules? gear = null)
    {
        gear ??= GearRules.PhantasieI;
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(sack);
        ArgumentNullException.ThrowIfNull(town);
        ArgumentNullException.ThrowIfNull(items);
        if (party.Members.Count == 0)
            return null;

        var pool = new List<int>();
        foreach (var ch in foundOnly ? [] : party.Members) // III's town entry (DOS ovl2 0x0000(1)): the found items only
        {
            pool.AddRange(ch.InventoryItemIds.Where(id => id != 0));
            ch.InventoryItemIds.Clear();
            ch.WornShield = ch.WornArmor = ch.WornWeapon = ch.WornBow = ch.WornThrown = 0; // nothing stays worn, any slot
            CombatGear.ApplyWornItemPoints(ch, gear.Points);
        }
        pool.AddRange(sack.ItemIds.Where(id => id != 0));
        sack.ItemIds.Clear();
        pool.Sort((a, b) => b.CompareTo(a)); // 0x2b79 bubble sort: larger id first
        return new ItemDistribution(party, sack, town, items, pool, salePrice, gear);
    }

    /// <summary>
    /// Give the current item (OV1 <c>0x2e03</c>). False when refused — the sack is full or the member already carries 9 —
    /// and the same item is asked again.
    /// </summary>
    public bool Give(DistributeTarget target, out string message)
    {
        if (Current is not int id)
        {
            message = "";
            return false;
        }
        var name = _items.Get(id)?.Name ?? $"#{id}";
        if (target == DistributeTarget.Sack)
        {
            if (!_sack.TryAdd(id)) // D-018: 90 (DOS 60); the first empty slot
            {
                message = GameText.S("core.distribute.the_sack_is_full");
                return false;
            }
            message = GameText.F("core.distribute.x_into_the_sack", name);
        }
        else if (target == DistributeTarget.Sell)
        {
            var gold = _salePrice?.Invoke(id) ?? ItemRules.SellPrice(_items.Get(id)?.ValueFactor ?? 0);
            _party.AddGold(gold); // D-048: over 65,000 is lost
            if (id < TownState.StockSlotCount)
                _town.SetStock(id, Math.Min(MaxStock, _town.GetStock(id) + 1));
            message = GameText.F("core.distribute.x_sold_for_x_gp", name, gold);
        }
        else
        {
            if ((uint)target.Value >= (uint)_party.Members.Count)
            {
                message = "";
                return false;
            }
            var ch = _party.Members[target.Value];
            if (CarriedItems.IsFull(ch))
            {
                message = GameText.F("core.distribute.x_cant_carry_more", ch.Name);
                return false;
            }
            CarriedItems.Put(ch, id); // 0x2f22 slot[count]: Begin emptied every slot, so this is contiguous
            _gear.ReadyBest(ch); // I: OV1 0x2fdb; III: ovl2 0x37f1 after each give (0x0b25, s173 [C] static)
            message = GameText.F("core.distribute.x_to_x", name, ch.Name);
        }
        _pool.RemoveAt(0);
        return true;
    }

    /// <summary>
    /// The item line (OV1 <c>0x1875</c>, <c>"%-10s:%3d %c%s"</c>): name, points, kind letter (S/A/W) and the party numbers of
    /// the members who can use it (<c>" 1234567"</c>, others blanked); no digits for non-equipment. Points and the gate are
    /// the game's (<paramref name="gear"/>, null = I's; III's column is ovl2 <c>0x39b5</c> at <c>0x0788</c>, s173 [C] static).
    /// </summary>
    public static string ItemLine(int itemId, string name, Party party, GearRules? gear = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        gear ??= GearRules.PhantasieI;
        var points = itemId is >= 1 and <= 100 ? gear.Points(itemId) : 0;
        var kind = itemId switch
        {
            < 21 => GameText.S("core.item.kind_shield"),
            < 41 => GameText.S("core.item.kind_armor"),
            < 101 => GameText.S("core.item.kind_weapon"),
            _ => " ",
        };
        if (itemId >= 101)
            points = 0;
        var users = itemId is >= 1 and <= 100
            ? string.Concat(party.Members.Select((m, i) => gear.CanUse(m, itemId) ? (char)('1' + i) : ' '))
            : "";
        return $"{(name.Length > 10 ? name[..10] : name),-10}:{points,3} {kind} {users}".TrimEnd();
    }
}
