namespace OpenPhantasie.Core;

/// <summary>
/// Item combat points + UNUSABLE gates from FUN_0000_3053 (data-tables.md [C]).
/// Armory buy and Inn Distribute SELL prices from OV1 formulas [C]; buying charges the full list price. The Armory itself has no sell (s58).
/// </summary>
public static class ItemRules
{
    public static int SellPrice(int valueFactor) => valueFactor * valueFactor / 8;

    /// <summary>Full DOS armory buy price; no CHA modifier applies.</summary>
    public static int BuyPrice(int valueFactor, int townPriceIndex) =>
        valueFactor * valueFactor * townPriceIndex / 500 + 1;

    public static (int Points, int MinRequirement, bool UsesStrDex) CombatGate(int itemId)
    {
        if (itemId < 21) // shield
            return (itemId, itemId, false);
        if (itemId < 31) // plain armor
            return (itemId - 20, itemId * 2 - 40, false);
        if (itemId < 41) // magic/god armor
            return (itemId - 29, itemId * 2 - 62, false);
        if (itemId < 71) // plain weapon
            return ((itemId >> 1) - 20, itemId * 2 - 80, true);
        if (itemId < 101) // magic/god weapon
            return ((itemId * 3 >> 2) - 50, itemId * 2 - 140, true);
        return (0, 0, false); // potion/scroll/treasure — always "usable"
    }

    /// <summary>
    /// Town U)SE ITEM on anything but a potion or a scroll (D-061, user s53: manual p.12 "find the unusual properties of an
    /// item"; DOS prints "UNUSABLE", OV1 <c>0x40a9</c>). The name, what the item does in numbers (point value as protection
    /// or damage and the strength it needs, from the game's table or <see cref="CombatGate"/>; potion strength), its Armory sale value
    /// (<see cref="SellPrice"/>), then the description text. Lines are unwrapped; the caller wraps them.
    /// </summary>
    public static IReadOnlyList<string> Describe(ItemCatalogEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var id = item.Id;
        var lines = new List<string> { item.FullName is { } full ? $"{item.Name} - {full}" : item.Name };
        var (points, need, check) = Gate(item);
        if (id is >= 1 and <= 40)
        {
            lines.Add(GameText.F(id <= 20 ? "core.item.shield_protection_x" : "core.item.armor_protection_x", points));
            lines.Add(need > 0 ? NeedsLine(check, need) : GameText.S("core.item.anyone_can_wear"));
        }
        else if (id is >= 41 and <= 100)
        {
            // A bow by the catalog's kind (III 41–60, the manual's "Bows" table, III PDF 11) gets its own label; I and II have none.
            lines.Add(GameText.F(item.Kind == "bow" ? "core.item.bow_damage_x" : "core.item.weapon_damage_x", points));
            lines.Add(need > 0 ? NeedsLine(check, need) : GameText.S("core.item.anyone_can_use"));
        }
        else if (Magic.IsHealingPotion(id))
            lines.Add(GameText.F("core.item.healing_potion_x", Magic.PotionHealingAmount(id)));
        else if (Magic.IsMagicPotion(id))
            lines.Add(GameText.F("core.item.magic_potion_x", Magic.PotionMagicAmount(id)));
        else if (id is >= 121 and <= 140)
            lines.Add(GameText.S("core.item.scroll"));
        else if (id is >= 141 and <= 150)
            lines.Add(GameText.S("core.item.magic_rune"));
        else
            lines.Add(GameText.S("core.item.treasure"));
        if (item.ValueFactor > 0)
            lines.Add(GameText.F("core.item.armory_pays_x", SellPrice(item.ValueFactor)));
        if (!string.IsNullOrWhiteSpace(item.Description))
            lines.Add(item.Description);
        return lines;
    }

    /// <summary>
    /// The points, requirement and attribute sum <see cref="Describe"/> prints: the game's own table when the catalog has one
    /// (III's <c>min_str</c> / <c>str_check</c> / <c>points</c>, bows on STR + DEX), else I's id formulas
    /// (<see cref="CombatGate"/>). I's and II's <c>items.json</c> hold exactly those formulas, so their lines are unchanged.
    /// </summary>
    private static (int Points, int Need, string Check) Gate(ItemCatalogEntry item)
    {
        var (points, need, strDex) = CombatGate(item.Id);
        if (item.Requirement is { } req && item.StrCheck is { } check)
            return (item.Points ?? points, req, check);
        return (points, need, strDex ? "str2_plus_dex" : "str");
    }

    private static string NeedsLine(string check, int need) => check switch
    {
        "str2_plus_dex" => GameText.F("core.item.needs_str_dex_x", need),
        "str_plus_dex" => GameText.F("core.item.needs_str_plus_dex_x", need),
        _ => GameText.F("core.item.needs_strength_x", need),
    };

    public static bool IsUsable(int itemId, CharacterStats stats)
    {
        var (_, minReq, usesStrDex) = CombatGate(itemId);
        if (minReq <= 0 && itemId >= 101) return true;
        if (usesStrDex)
            return stats.Str * 2 + stats.Dex >= minReq;
        return stats.Str >= minReq;
    }

    public static bool IsEquipment(int itemId) => itemId is >= 1 and <= 100;
    public static bool IsShield(int itemId) => itemId is >= 1 and <= 20;
    public static bool IsArmor(int itemId) => itemId is >= 21 and <= 40;
    public static bool IsWeapon(int itemId) => itemId is >= 41 and <= 100;
    public static bool IsHealingPotion(int itemId) => Magic.IsHealingPotion(itemId);
    public static bool IsMagicPotion(int itemId) => Magic.IsMagicPotion(itemId);
    public static bool IsPotion(int itemId) => Magic.IsPotion(itemId);
}
