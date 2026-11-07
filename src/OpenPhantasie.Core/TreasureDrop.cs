namespace OpenPhantasie.Core;

/// <summary>
/// End-of-combat treasure, OV2 <c>0x40e4..0x41f6</c> and item drop <c>0x421d</c> (<c>combat-resolution.md</c>
/// §Outputs / rewards). Runs after the elemental leaves, the XP/gold go into the sack and the party wakes. Returns the
/// item ids offered with "YOU FIND %s. KEEP IT?", in order; the caller asks and stores them.
/// </summary>
public static class TreasureDrop
{
    /// <summary>
    /// 0-based id of the BLACK LORD: a dungeon fight against him drops nothing
    /// (<see cref="Phantasie1Campaign.WithholdsDungeonTreasure"/>).
    /// </summary>
    public const int BlackLordId = Phantasie1Campaign.BlackLordId;

    /// <summary>
    /// SCROLL 17, the win scroll (<c>endgame.md</c>): only dropped inside the Fortress of the Black Knights
    /// (<see cref="Phantasie1Campaign.MayDropItem"/>).
    /// </summary>
    public const int WinScrollId = Phantasie1Campaign.WinScrollId;

    /// <param name="dungeonLevelId">Current dungeon level, or null in the wilderness (<c>[0x64e8]</c> = 2).</param>
    /// <param name="itemCount">Size of the item name table; shifted ids outside it are dropped <c>[~]</c>.</param>
    /// <param name="mayDrop">
    /// The campaign's item-by-level rule (<see cref="ICampaign.MayDropItem"/>); null = Phantasie I's (SCROLL 17 only in
    /// DNG9).
    /// </param>
    /// <param name="withholds">
    /// The campaign's no-treasure rule for a dungeon fight by first monster (<see cref="ICampaign.WithholdsDungeonTreasure"/>);
    /// null = Phantasie I's (the BLACK LORD).
    /// </param>
    public static IReadOnlyList<int> Roll(
        CombatEncounter encounter, int? dungeonLevelId, int itemCount, IRandomSource rng, List<string> log,
        Func<int, int?, bool>? mayDrop = null, Func<int, bool>? withholds = null)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);

        var ids = encounter.TreasureItemIds;
        var idA = ids.Count > 0 ? ids[0] : 0;
        var idB = ids.Count > 1 ? ids[1] : 0;
        var give = !encounter.TreasureForfeited; // [0x74ba]: 1 at build, 0 after BEG / GREET / FLEE / TELEPORTATION; CHARM = remaster D-022

        if (dungeonLevelId is not null)
        {
            // 0x40e4: presence roll, then the first non-DEAD member (party order) who makes a find-item roll spots it.
            if ((withholds ?? Phantasie1Campaign.Instance.WithholdsDungeonTreasure)(encounter.FirstMonsterId) || !give)
                give = false;
            else
                give = Roll0To99(rng) <= 45 * (encounter.SurrenderTreasure ? 1 : 0) + 2 * encounter.MonsterLevel + 50;
            if (give)
            {
                var finder = encounter.Party.Members.FirstOrDefault(
                    m => m.Status != CharacterStatus.Dead && Roll0To99(rng) <= m.Skills.FindItem);
                give = finder is not null;
                if (finder is not null && (idA != 0 || idB != 0))
                    log.Add(GameText.F("core.treasure.x_spots_an_item", finder.Name));
            }
        }
        // Wilderness: DOS 0x41a3 rolls rand0_99 ≤ 2·L + 20. Remaster (D-031, user s34, manual p.10: wilderness monsters
        // "automatically give away their treasure"): no roll — unless the save plays DOS's (p1.wild-treasure).
        var dos = encounter.Rules.IsDos(RuleOptionRegistry.WildernessTreasure);
        if (dungeonLevelId is null && dos && give)
            give = Roll0To99(rng) <= 2 * encounter.MonsterLevel + 20;

        var offers = new List<int>();
        if ((encounter.SurrenderTreasure || give) && (idA != 0 || idB != 0))
        {
            // 0x41c4: both ids, even if one is 0 (each still draws its shift roll).
            foreach (var id in new[] { idA, idB })
            {
                if (Drop(id, shift: true, dungeonLevelId, itemCount, rng, mayDrop, guardUnshifted: dos) is { } v)
                    offers.Add(v);
            }
        }
        return offers;
    }

    /// <summary>
    /// Item drop <c>0x421d(id, roll)</c>: with <paramref name="shift"/> the item is <c>id + 3 − rand/20</c> (id − 1 … id + 3,
    /// 20 % each; the roll is drawn before any test). Nothing for <c>id ≤ 0</c> or <c>id ≥ 256</c>.
    /// SCROLL 17 only inside the Fortress of the Black Knights. DOS tests the <i>unshifted</i> id there, so FIRE ELEMENTAL
    /// (treasure 135) gave SCROLL 17 anywhere 20 % of the time; the remaster tests the shifted item (user, s34, D-031).
    /// Script op 6 calls this with <paramref name="shift"/> false. Which item may drop where is the campaign's
    /// (<paramref name="mayDrop"/>, <see cref="ICampaign.MayDropItem"/>); null = Phantasie I's rule.
    /// </summary>
    /// <param name="guardUnshifted">
    /// D-031 <c>dos</c> (<c>p1.wild-treasure</c>): test the item rule on the unshifted <paramref name="id"/>, as DOS's
    /// SCROLL 17 guard does, so FIRE ELEMENTAL's shifted 137 drops anywhere.
    /// </param>
    public static int? Drop(
        int id, bool shift, int? dungeonLevelId, int itemCount, IRandomSource rng, Func<int, int?, bool>? mayDrop = null,
        bool guardUnshifted = false)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var v = shift ? id + 3 - Roll0To99(rng) / 20 : id;
        if (id <= 0 || id >= 256)
            return null;
        if (!(mayDrop ?? Phantasie1Campaign.Instance.MayDropItem)(guardUnshifted ? id : v, dungeonLevelId))
            return null;
        if (v <= 0 || v >= itemCount) // id 1 can shift to 0 (DOS stores an empty slot) [~]
            return null;
        return v;
    }

    private static int Roll0To99(IRandomSource rng) => rng.Next(0, 100);
}
