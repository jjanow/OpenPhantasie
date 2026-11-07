namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie IV outdoors, ships and the non-combat spells with a decided difference
/// (<c>reverse-engineering/x68000/phantasie4/logic-specs/outdoor.md</c>, <c>events.md</c> §6.4,
/// <c>spells-noncombat.md</c>): D4-038 (ships), D4-039 (Levitation), D4-040 (Navigation), D4-041 (Examine), D4-053
/// (Identify) and D4-054 (Monster Evaluation) as <c>p4.*</c> options. The manual's amounts are the user's (s84,
/// <see cref="P4ManualAmounts"/>); one still <c>[?]</c> throws <see cref="P4AmountUnknownException"/>.
/// </summary>
public static class P4Outdoor
{
    /// <summary>
    /// D4-039: how many steps Levitation cast outside combat keeps the party aloft — 5 × the slowest member's stride under the
    /// manual (p. 55); null in the code (until the end of the next fight, any distance).
    /// </summary>
    public static int? LevitationSteps(GameOptions o, P4OutdoorTables t, IEnumerable<int> strides) =>
        o.P4Code(P4RuleOptions.Levitation) ? null : t.LevitationStrideMultiple * strides.Min();

    /// <summary>
    /// D4-039: whether Levitation keeps the party out of the water outdoors too (manual: "the whole party can fly", wilds and
    /// dungeons); the code reads the flag only in dungeons.
    /// </summary>
    public static bool LevitationWorksOutdoors(GameOptions o) => !o.P4Code(P4RuleOptions.Levitation);

    /// <summary>
    /// Who takes the helm on boarding — the member with the highest Navigation (first on a tie; the code does not check
    /// condition); null for an empty party. D4-040 (user s84): the manual's "do not use it below 50 %" is advice with a risk,
    /// so the best navigator steers under both rules (<see cref="Drifts"/>).
    /// </summary>
    public static int? Navigator(IReadOnlyList<int> navigation)
    {
        if (navigation.Count == 0)
            return null;
        var best = 0;
        for (var i = 1; i < navigation.Count; i++)
        {
            if (navigation[i] > navigation[best])
                best = i;
        }
        return best;
    }

    /// <summary>
    /// Drift: each step aboard with no forced move pending, rand(100) &gt; Navigation starts a drift (outdoor.md §4.2). D4-040
    /// manual (user s84): below Navigation 50 a second roll, rand(100) ≤ 50 − Navigation, drifts too.
    /// </summary>
    public static bool Drifts(GameOptions o, P4OutdoorTables t, IRandomSource rng, int navigation)
    {
        if (P4Dice.Rand(rng, 100) > navigation)
            return true;
        return !o.P4Code(P4RuleOptions.Navigation) && navigation < t.NavigationDriftBelowManual
            && P4Dice.Rand(rng, 100) <= t.NavigationDriftBelowManual - navigation;
    }

    /// <summary>
    /// D4-038: whether a fight at sea wrecks the party's ship — never in the code; under the manual (p. 28) 5 % after a sea
    /// fight the party lost or fled, never after one it won (user s84).
    /// </summary>
    public static bool ShipWreckedInFight(GameOptions o, P4ManualAmounts amounts, IRandomSource rng, bool partyWon) =>
        !o.P4Code(P4RuleOptions.ShipWear) && !partyWon
        && P4Dice.Rand(rng, 100) <= P4Amount.Require(amounts.ShipWreckChancePerSeaFight, "D4-038 wreck chance");

    /// <summary>
    /// D4-038: whether a ship that has sailed <paramref name="steps"/> sea steps is worn out — never in the code; under the
    /// manual after <c>p4.ship-life</c> steps (4000 by default, user s84).
    /// </summary>
    public static bool ShipWornOut(GameOptions o, int steps) =>
        !o.P4Code(P4RuleOptions.ShipWear) && steps >= o.P4ShipLifeSteps();

    /// <summary>
    /// D4-041: Examine (E) in a dungeon. Code: an automatic walk of up to 63 steps to the least-visited squares with
    /// encounters at a quarter of the rate, no search roll (returns 0). Manual (p. 38): the party searches its surroundings
    /// and finds traps and treasure more efficiently: +20 to the trap-finding and treasure-finding rolls (user s84).
    /// </summary>
    public static int ExamineSearchBonus(GameOptions o, P4ManualAmounts amounts) =>
        o.P4Code(P4RuleOptions.Examine) ? 0 : P4Amount.Require(amounts.ExamineSearchBonus, "D4-041 search bonus");

    /// <summary>True when Examine is the code's automatic walk (<c>_seainit</c> / <c>_seasel</c>) rather than the manual's search.</summary>
    public static bool ExamineWalks(GameOptions o) => o.P4Code(P4RuleOptions.Examine);

    /// <summary>
    /// D4-053: Identify always succeeds in the code; under the manual (p. 54) it does not always: rand(100) ≤ 50 + 5 × the
    /// caster's level (user s84), so from level 10 it always does.
    /// </summary>
    public static bool IdentifySucceeds(GameOptions o, P4ManualAmounts amounts, IRandomSource rng, int casterLevel) =>
        o.P4Code(P4RuleOptions.Identify)
        || P4Dice.Rand(rng, 100) <= P4Amount.Require(amounts.IdentifyChanceBase, "D4-053 Identify chance")
            + P4Amount.Require(amounts.IdentifyChancePerLevel, "D4-053 Identify chance per level") * casterLevel;

    /// <summary>Monster Evaluation's reported level stays within 1–12 (the code's outdoor clamp, <c>_pmagic3</c>).</summary>
    public const int MinReportedLevel = 1, MaxReportedLevel = 12;

    /// <summary>
    /// D4-054: the level Monster Evaluation reports (the code: the true one; outdoors the encounter level clamped 1–12, in a
    /// dungeon the map's level). Under the manual (p. 55) it sometimes reports wrongly (user s84): on rand(100) ≤ 50 − 5 ×
    /// the caster's level (at least 10) it is off by 1, down on rand(2) = 1, else up, kept within 1–12.
    /// </summary>
    public static int MonsterEvaluation(GameOptions o, P4ManualAmounts amounts, IRandomSource rng, int trueLevel, int casterLevel)
    {
        if (o.P4Code(P4RuleOptions.MonsterEval))
            return trueLevel;
        var chance = Math.Max(
            P4Amount.Require(amounts.MonsterEvalWrongMin, "D4-054 wrong-report minimum"),
            P4Amount.Require(amounts.MonsterEvalWrongBase, "D4-054 wrong-report chance")
            - P4Amount.Require(amounts.MonsterEvalWrongPerLevel, "D4-054 wrong-report chance per level") * casterLevel);
        if (P4Dice.Rand(rng, 100) > chance)
            return trueLevel;
        var by = P4Amount.Require(amounts.MonsterEvalWrongBy, "D4-054 wrong-report size");
        var reported = P4Dice.Rand(rng, 2) == 1 ? trueLevel - by : trueLevel + by;
        return Math.Clamp(reported, MinReportedLevel, MaxReportedLevel);
    }

    /// <summary>Swimming without a ship: a member takes rand(5) + 1 when not fit or rand(100) &gt; Swim (outdoor.md §3.1).</summary>
    public static int? SwimDamage(IRandomSource rng, int swim, bool fit) =>
        !fit || P4Dice.Rand(rng, 100) > swim ? P4Dice.Rand(rng, 5) + 1 : null;
}
