namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's spell list on the shared caster (T4): #1–12, 17–52 and 54 are I's spells (<c>FORMAT.md</c> §4.2) and
/// cast as I's; III's own — #13–16 Arrow Flame I / P. Arrow Flame I / Arrow Flame II / P. Arrow Flame II (I's slots were
/// Strength I–IV), #53 (empty), #55 / #56 Accuracy / Party Accuracy and #57 Divine Aid (never castable,
/// <c>dungeon-events.md</c> §6) — do nothing on it: in a III fight <see cref="P3CombatModule"/> casts Arrow Flame #13–16
/// and Accuracy #55 / #56 itself (III's spell layout, <see cref="SpellSlots.PhantasieIII"/>: index = DOS slot, so
/// Accuracy costs 3 MP and Party Accuracy 4); outside a fight they are combat-only. Healing also cures body areas
/// (<see cref="P3BodyAreas.HealingCurePoints"/>).
/// </summary>
public static class P3Spells
{
    /// <summary>#55 Accuracy (0-based; DOS slot 54, root <c>0xca34</c>).</summary>
    public const int Accuracy = 54;

    /// <summary>#56 Party Accuracy (0-based; DOS slot 55, root <c>0xca46</c>).</summary>
    public const int PartyAccuracy = 55;

    /// <summary>0-based indices of III's own spells (spell number − 1).</summary>
    public static IReadOnlySet<int> OwnSpells { get; } = new HashSet<int> { 12, 13, 14, 15, 52, Accuracy, PartyAccuracy, 56 };

    /// <summary>True when <paramref name="rules"/> play III and <paramref name="spellIndex"/> is one of III's own spells.</summary>
    public static bool NotOnTheSharedCaster(GameOptions? rules, int spellIndex) =>
        rules?.Game == GameId.P3 && OwnSpells.Contains(spellIndex);

    /// <summary>Resurrection's CON floor in III (root <c>0x18b6</c>).</summary>
    public const int ResurrectionConFloor = 3;

    /// <summary>
    /// III's Resurrection (#44, root <c>0x18b6</c>) after the shared caster raised <paramref name="raised"/>: HP 1, magic
    /// full, all six body areas Okay, CON − <c>rnd(2)</c> with a floor of 3 (III loses no CON at death, unlike I; the
    /// manual agrees). The raise chance itself is the shared caster's (I's) `[~]`.
    /// </summary>
    public static void AfterResurrection(GameOptions? rules, Character raised, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(raised);
        if (rules?.Game != GameId.P3)
            return;
        raised.HpCur = 1;
        raised.MagicCur = raised.MagicMax;
        raised.BodyCondition = new P3BodyAreas().ToCondition();
        raised.Status = CharacterStatus.Ok;
        raised.Stats = raised.Stats with
        {
            Con = Math.Max(ResurrectionConFloor, raised.Stats.Con - P3Dice.Rnd(rng, 2)),
        };
    }

    /// <summary>
    /// After a Healing I–IV cast in III: the target's body areas take the tier's cure points (root <c>0x194d</c>, manual
    /// D3-031 order, D3-014 tier IV), and its status follows the body.
    /// </summary>
    public static void CureBody(GameOptions? rules, Character target, int spellIndex)
    {
        if (rules?.Game != GameId.P3 || spellIndex > SpellId.HealingIv || target.BodyCondition is null)
            return;
        var body = P3BodyAreas.Of(target);
        if (body.AllOkay)
            return;
        body.Heal(P3BodyAreas.HealingCurePoints(spellIndex + 1, rules), rules);
        target.BodyCondition = body.ToCondition();
        Phantasie3Campaign.ApplyBodyStatus(target, body);
    }
}
