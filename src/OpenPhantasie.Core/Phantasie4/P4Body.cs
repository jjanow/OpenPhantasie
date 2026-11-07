namespace OpenPhantasie.Core;

/// <summary>Body parts in IV's order (<c>_body</c>, <c>combat.md</c>): the wound word packs them head first.</summary>
public enum P4BodyPart { Head = 0, Torso = 1, RightArm = 2, LeftArm = 3, RightLeg = 4, LeftLeg = 5 }

/// <summary>
/// Phantasie IV wounds (<c>combat.md</c> §5–§6, <c>items.md</c> §3): six parts at levels 0 none, 1 light, 2 serious, 3
/// lost; healing points; hit location; healing potions (D4-035) and losing the head or torso (D4-043) as <c>p4.*</c> options.
/// </summary>
public static class P4Body
{
    public const int Parts = 6;
    public const int Light = 1, Serious = 2, Lost = 3;

    /// <summary>
    /// <c>_heal(member, k)</c>: k is a budget of wound points; starting at level min(k, 3) every part at that level is cleared
    /// and its level taken from the budget, then the next lower level, while budget remains (never a level above the budget
    /// left). 1 = a light wound, 2 = a serious or two light, 3 = a lost part, or serious + light, or three light.
    /// </summary>
    public static void Heal(int[] levels, int points)
    {
        ArgumentNullException.ThrowIfNull(levels);
        var budget = points;
        var level = Math.Min(budget, Lost);
        while (level >= 1 && budget > 0)
        {
            for (var p = 0; p < Parts && budget >= level; p++)
            {
                if (levels[p] == level)
                {
                    levels[p] = 0;
                    budget -= level;
                }
            }
            level = Math.Min(level - 1, budget);
        }
    }

    /// <summary>
    /// A Healing potion n (1–10): HP restored and wound points healed. Manual (D4-035, Tables 2–3): HP 1, 4, 9, 16, 25, 35,
    /// 49, 64, 81, 100 and potions 1–3 / 4–6 / 7–9 / 10 = Healing 1 / 2 / 3 / 4. Code: HP n², points trunc((n + 1) / 3).
    /// </summary>
    public static (int Hp, int HealPoints) HealingPotion(GameOptions o, P4TownTables t, int n)
    {
        if (n is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(n));
        return o.P4Code(P4RuleOptions.PotionWounds)
            ? (n * n, (n + 1) / 3)
            : (t.PotionHpManual[n - 1], t.PotionHealPointsManual[n - 1]);
    }

    /// <summary>A potion goes bad with rand(100) &gt; 90 (<c>_spoile</c>; manual silent, the code stands).</summary>
    public static bool PotionSpoiled(IRandomSource rng) => P4Dice.Rand(rng, 100) > 90;

    public static bool HeadOrTorsoLost(IReadOnlyList<int> levels) =>
        levels[(int)P4BodyPart.Head] >= Lost || levels[(int)P4BodyPart.Torso] >= Lost;

    /// <summary>
    /// D4-043: true when the member dies of the wounds regardless of HP — a lost head or torso under the manual (p. 46); the
    /// code only makes him "Out" (combat condition 4) and healable.
    /// </summary>
    public static bool WoundsKill(GameOptions o, IReadOnlyList<int> levels) =>
        !o.P4Code(P4RuleOptions.HeadTorso) && HeadOrTorsoLost(levels);

    /// <summary>
    /// Hit location (<c>_plget</c> / <c>_mlget</c>, §6): part = trunc(6 × u^(type / 2)), u = rand(100) / 100 for a party
    /// member, (rand(100) − 1) / 100 for a monster; part 6 → 3 (left arm); type 5 → rand(3) + 2 (never the right arm).
    /// </summary>
    public static P4BodyPart HitLocation(IRandomSource rng, int attackType, bool partyMember)
    {
        if (attackType == 5)
            return (P4BodyPart)(P4Dice.Rand(rng, 3) + 2);
        var r = P4Dice.Rand(rng, 100);
        var u = partyMember ? r / 100.0 : (r - 1) / 100.0;
        var part = (int)Math.Truncate(6 * Math.Pow(u, attackType / 2.0));
        return part >= 6 ? P4BodyPart.LeftArm : (P4BodyPart)part;
    }

    /// <summary>
    /// D4-058 (manual): an Aim Blow's hit location, the head or the torso. The share is the code's melee roll (u^0.5 on
    /// <c>u = (rand(100) − 1) / 100</c>) cut to those two parts: u 0.00–0.02 head, 0.03–0.11 torso, so 3 in 12 head.
    /// </summary>
    public static P4BodyPart AimedLocation(IRandomSource rng) =>
        P4Dice.Rand(rng, 12) <= 3 ? P4BodyPart.Head : P4BodyPart.Torso;

    /// <summary>
    /// Where a party hit lands on a monster (<c>_mlget</c>): <see cref="AimedLocation"/> for an <paramref name="aimed"/> blow
    /// (D4-058, manual), except on type 5, which no blow reaches at the head or torso (manual p. 46); else
    /// <see cref="HitLocation"/>.
    /// </summary>
    public static P4BodyPart MonsterHitLocation(IRandomSource rng, int attackType, bool aimed) =>
        aimed && attackType != 5 ? AimedLocation(rng) : HitLocation(rng, attackType, partyMember: false);

    /// <summary>
    /// Wound level after a hit (<c>_getpd</c> / <c>_getmd</c>): w = trunc(u (d / (H + 0.0001) + 0.2 old) / 0.15 + 0.5), u =
    /// rand(100) / 100; the part rises to min(3, w) when w &gt; old, never lowers. H = max HP (party) or current HP (monster).
    /// </summary>
    public static int WoundLevel(IRandomSource rng, int damage, int hp, int old)
    {
        var u = P4Dice.Frand(rng);
        var w = (int)Math.Truncate(u * (damage / (hp + 0.0001) + 0.2 * old) / 0.15 + 0.5);
        return w > old ? Math.Min(Lost, w) : old;
    }
}
