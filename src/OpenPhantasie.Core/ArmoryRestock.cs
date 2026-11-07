namespace OpenPhantasie.Core;

/// <summary>
/// Armory stock churn on town entry [C] (s41, OV1 <c>0x8e1</c>, called at the end of the town-entry pass <c>0x1954</c>;
/// town-menus.md §Armory restock). The manual (p.10): "Combat weapons and potions, however, may appear and disappear
/// between visits"; rings, scrolls and other treasures stay. DOS, for the town being entered:
/// <list type="number">
/// <item>18 times: <c>stock[rand / 274] = 0</c> — ids 0..119 only (equipment 0..100 and the HEALING/MAGIC potions
/// 101..120 minus MAGIC 10); a whole stack goes, repeats allowed.</item>
/// <item>4 times: <c>stock[restock[i]] = 1</c> with <c>i = rand / (32767 / L)</c>, <c>L</c> = the average party level
/// <c>[0x63ec]</c> from the last town exit; with <c>L = 0</c>, <c>i = 0</c> and no random number is drawn. It is a
/// set, not an add: a 5-stack hit here drops to 1.</item>
/// </list>
/// Skipped when <c>[0x63e8] != 0</c> (the new-game / load path <c>0x1e15(arg ≠ 0)</c>); every town entry through
/// <c>0x1954</c> clears that flag first, so every ordinary arrival restocks.
/// <para>D-054 (user s42, manual): only weapons and potions change. The same random numbers are drawn as in DOS, but a
/// wipe that lands on any other id (armor, shields) leaves its stock alone. A restock draw on armor or a shield refills it
/// to 1 only when it is sold out (user s57: bought-out armor must come back; never lowers a stack, so nothing
/// "disappears"). <paramref name="dosRule"/> = true runs the DOS rule (armor and shields churn too): the save's
/// <c>p1.armory-restock</c> option (<see cref="GameOptions.DosArmoryRestock"/>, Original (DOS) preset).</para>
/// </summary>
public static class ArmoryRestock
{
    /// <summary>Number of random stock wipes (OV1 <c>0x916</c>: <c>cmp si, 0x12</c>).</summary>
    public const int WipeCount = 18;

    /// <summary>Wipe index divisor (OV1 <c>0x91e</c>): <c>rand / 274</c> = 0..119.</summary>
    public const int WipeDivisor = 274;

    /// <summary>Number of restock draws (OV1 <c>0x932</c>: <c>cmp si, 4</c>).</summary>
    public const int AddCount = 4;

    /// <summary>D-054: the ids the manual lets "appear and disappear" — weapons and the HEALING/MAGIC potions.</summary>
    public static bool ManualChurns(int itemId) => ItemRules.IsWeapon(itemId) || ItemRules.IsPotion(itemId);

    public static void Apply(TownState town, int averagePartyLevel, IDosRandomSource rng, bool dosRule = false)
    {
        ArgumentNullException.ThrowIfNull(town);
        ArgumentNullException.ThrowIfNull(rng);

        for (var k = 0; k < WipeCount; k++)
        {
            var id = rng.NextDosWord() / WipeDivisor;
            if (dosRule || ManualChurns(id))
                town.SetStock(id, 0);
        }

        for (var k = 0; k < AddCount; k++)
        {
            // [C] OV1 0x937..0x968: signed 16-bit divides; rand is 0..32767 so i can reach L itself (rand ≥ L·⌊32767/L⌋).
            var i = averagePartyLevel != 0 ? rng.NextDosWord() / (0x7fff / averagePartyLevel) : 0;
            // DOS reads the word at record +0xb5 + 2i with no bound; i ≤ L ≤ 20 (training stops at 15, the party-defeat
            // UNDEAD fate sets level 20, endgame.md §4) always lands in the 45-entry list, so the guard is never reached.
            if ((uint)i >= (uint)town.RestockItems.Count)
                continue;
            var item = town.RestockItems[i];
            if ((uint)item >= (uint)TownState.StockSlotCount)
                continue;
            if (dosRule || ManualChurns(item) || town.GetStock(item) == 0)
                town.SetStock(item, 1);
        }
    }
}
