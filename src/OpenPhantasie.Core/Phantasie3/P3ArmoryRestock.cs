namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's Armory restock data (<c>rule-tables.json</c> <c>armory_restock</c>, D3-033): Apple II's churn.
/// </summary>
/// <param name="Wipes">Random stock wipes per entry (Apple II line 850: 18).</param>
/// <param name="WipeIds">Wiped ids are 0 … <c>WipeIds − 1</c> (111: equipment and Healing Potions).</param>
/// <param name="Picks">Pool picks set to 1 per entry (line 860: 13).</param>
/// <param name="LevelCap">The party average level used for the pick is at most this (line 855: 15).</param>
/// <param name="Pool">Apple II's pool, <c>LA+181…226</c>.</param>
/// <param name="Removed">Pool ids a pick never stocks (user s89: the Light Key and the Burnt Gem).</param>
public sealed record P3ArmoryRestockRule(
    int Wipes, int WipeIds, int Picks, int LevelCap, IReadOnlyList<int> Pool, IReadOnlySet<int> Removed);

/// <summary>
/// D3-033 (user s89): the manual's "the stocks of weapons and potions can change each time the party enters", by Apple
/// II's rule (side 1A town lines 840–890) [C] minus the two quest treasures. On each town entry: <see
/// cref="P3ArmoryRestockRule.Wipes"/> times <c>stock[rnd(0 … WipeIds − 1)] = 0</c> (repeats allowed), then <see
/// cref="P3ArmoryRestockRule.Picks"/> times <c>stock[pool[⌊(1 − r₁·r₂) · L · 3⌋]] = 1</c> (set, not add; L = the party's
/// average level, capped), skipping the removed ids. Scrolls, magic potions and treasures are never wiped. DOS / Amiga
/// never restock (<c>p3.armory-restock</c> = <c>dos</c>).
/// </summary>
public static class P3ArmoryRestock
{
    /// <summary>Resolution of the Applesoft <c>RND(1)</c> stand-in.</summary>
    private const int RndSteps = 1 << 20;

    public static void Apply(TownState town, P3ArmoryRestockRule rule, int averageLevel, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(town);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(rng);
        for (var k = 0; k < rule.Wipes; k++)
            town.SetStock(rng.Next(0, rule.WipeIds), 0);
        var level = Math.Clamp(averageLevel, 0, rule.LevelCap);
        for (var k = 0; k < rule.Picks; k++)
        {
            var i = (int)Math.Floor((1 - Rnd(rng) * Rnd(rng)) * level * 3);
            if (i >= rule.Pool.Count)
                continue;
            var item = rule.Pool[i];
            if (item > 0 && item < TownState.StockSlotCount && !rule.Removed.Contains(item))
                town.SetStock(item, 1);
        }
    }

    /// <summary>The party's average level (Apple II <c>SA+1202</c> <c>[~]</c>): the adventurers' levels, rounded down.</summary>
    public static int AverageLevel(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        var members = party.Adventurers.ToList();
        return members.Count == 0 ? 0 : members.Sum(m => m.Level) / members.Count;
    }

    private static double Rnd(IRandomSource rng) => rng.Next(0, RndSteps) / (double)RndSteps;
}
