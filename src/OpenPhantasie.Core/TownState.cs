namespace OpenPhantasie.Core;

/// <summary>
/// Mutable Armory stock + pricing for one town (from TWNS.DAT).
/// </summary>
public sealed class TownState
{
    public const int StockSlotCount = 181;

    public int Id { get; }
    public int Index { get; }
    public string Name { get; }
    public int PriceIndex { get; set; }
    public int[] Stock { get; }

    /// <summary>
    /// P1.4 raw-record carry: the 291-byte TWNS.DAT record this town was imported from (null when built from
    /// <c>towns.json</c>). Bytes past the stock, price index and name are written back unchanged: 181..270 are the restock
    /// list (<see cref="RestockItems"/>, read at OV1 <c>0x95c</c>) [C]; 289..290 have no reader or writer — probably the
    /// name's NUL terminator plus a pad [~].
    /// </summary>
    public byte[]? DosRecord { get; set; }

    /// <summary>
    /// TWNS.DAT <c>+0xb5</c>: the town's 45 restock item ids, ordered roughly by party level (<see cref="ArmoryRestock"/>).
    /// Static data — DOS never writes it.
    /// </summary>
    public IReadOnlyList<int> RestockItems { get; init; } = [];

    /// <summary>
    /// TWNS.DAT <c>+0x111</c>/<c>+0x113</c>/<c>+0x115</c>: overworld region and the town tile's x/y [C] (s41, s56). Static;
    /// their only reader is town Exit, which copies them into the party position (OV1 <c>0x3309..0x331a</c>). Null without
    /// a DOS record.
    /// </summary>
    public (int Region, int X, int Y)? ExitPosition =>
        DosRecord is { Length: >= 0x117 } r
            ? (r[0x111] | r[0x112] << 8, r[0x113] | r[0x114] << 8, r[0x115] | r[0x116] << 8)
            : Exit;

    /// <summary>
    /// The Exit position a town without a DOS record carries (Phantasie II, <c>towns.json</c> <c>exit_region</c> / <c>exit_x</c> /
    /// <c>exit_y</c>; ST <c>0xdd6e</c> copies them with no other test, so BLACK MOOR and DEMONIAC exit into other regions).
    /// </summary>
    public (int Region, int X, int Y)? Exit { get; init; }

    /// <summary>
    /// Phantasie II: the towns the ST's TRANSPORTATION picker offers from here (<c>transport_destinations</c>, same plane;
    /// D2-030 option <c>p2.transport-list</c> = <c>st</c>). Null for I.
    /// </summary>
    public IReadOnlyList<int>? TransportDestinations { get; init; }

    /// <summary>This town's saved state with the static data a save does not hold (II's exit and transport list) from <paramref name="template"/>.</summary>
    public TownState WithStaticDataFrom(TownState template)
    {
        var copy = Clone();
        return new TownState(Id, Index, Name, PriceIndex, Stock)
        {
            DosRecord = copy.DosRecord,
            RestockItems = RestockItems.Count > 0 ? RestockItems : template.RestockItems,
            Exit = Exit ?? template.Exit,
            TransportDestinations = TransportDestinations ?? template.TransportDestinations,
        };
    }

    /// <summary>The first item id the Armory lists or sells to the party [C] (OV1 <c>0x3e2d</c>/<c>0x3ee8</c> start at 1): id 0 is never shown.</summary>
    public const int FirstListedItemId = 1;

    /// <summary>Deep copy (session snapshots).</summary>
    public TownState Clone() =>
        new(Id, Index, Name, PriceIndex, Stock)
        {
            DosRecord = DosRecord is null ? null : (byte[])DosRecord.Clone(),
            RestockItems = RestockItems,
            Exit = Exit,
            TransportDestinations = TransportDestinations,
        };

    public TownState(int id, int index, string name, int priceIndex, IReadOnlyList<int> stock)
    {
        if (stock.Count < StockSlotCount)
            throw new ArgumentException($"Stock must have {StockSlotCount} slots.", nameof(stock));

        Id = id;
        Index = index;
        Name = name;
        PriceIndex = priceIndex;
        Stock = new int[StockSlotCount];
        for (var i = 0; i < StockSlotCount; i++)
            Stock[i] = stock[i];
    }

    public int GetStock(int itemId) =>
        (uint)itemId < (uint)StockSlotCount ? Stock[itemId] : 0;

    public void SetStock(int itemId, int qty)
    {
        if ((uint)itemId >= (uint)StockSlotCount)
            throw new ArgumentOutOfRangeException(nameof(itemId));
        Stock[itemId] = Math.Max(0, qty);
    }
}

/// <summary>Catalog entry needed by town transactions (value factor + name).</summary>
public sealed class ItemCatalogEntry
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int ValueFactor { get; init; }

    /// <summary>
    /// Phantasie III's value (<c>DS:0xf92</c>): the Armory asks <c>value · price index / 100</c> (root <c>0x0fb8</c>)
    /// [~]. Null in I / II, whose price comes from <see cref="ValueFactor"/>.
    /// </summary>
    public int? Value { get; init; }
    public string Kind { get; init; } = "unknown";

    /// <summary>
    /// Phantasie III's point value (<c>DS:0x113c + id</c>, <c>FORMAT.md</c> §4.1): shield / armour protection, weapon and bow
    /// damage. I / II combat reads <see cref="ItemRules.CombatGate"/> instead.
    /// </summary>
    public int? Points { get; init; }

    /// <summary>
    /// The item's attribute requirement (<c>items.json</c> <c>min_str</c>; III's table <c>DS:0x11a2 + id</c>). I's and II's
    /// files hold I's id formulas (<see cref="ItemRules.CombatGate"/>), which their gates still compute; III's gate reads it
    /// (<c>P3TownEntryPayout.CanUse</c>). Null when the item has none.
    /// </summary>
    public int? Requirement { get; init; }

    /// <summary>
    /// Which attribute sum <see cref="Requirement"/> is compared with (<c>items.json</c> <c>str_check</c>, III ovl2
    /// <c>0x39b5</c>): <c>str</c>, <c>str_plus_dex</c> or <c>str2_plus_dex</c>; null when the item has no requirement.
    /// </summary>
    public string? StrCheck { get; init; }

    /// <summary>The manual's name for the item (D-061 U)SE ITEM), e.g. "Small shield +1"; null when none was written.</summary>
    public string? FullName { get; init; }

    /// <summary>Remaster-written text for U)SE ITEM (D-061, <c>item-descriptions.json</c>).</summary>
    public string? Description { get; init; }
}

public interface IItemCatalog
{
    ItemCatalogEntry? Get(int itemId);
    int Count { get; }
}
