namespace OpenPhantasie.Core;

/// <summary>Phantasie III's random number: <c>rnd(n) = rand() % n + 1</c>, 1…n (root <c>0x2c31</c>).</summary>
public static class P3Dice
{
    public static int Rnd(IRandomSource rng, int n)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return n <= 1 ? 1 : rng.Next(1, n + 1);
    }
}

/// <summary>A character's six body areas in record order (<c>+0xf2 + 2k</c>, <c>body-areas.md</c> §1).</summary>
public enum P3BodyArea
{
    Head = 0,
    LeftArm = 1,
    RightArm = 2,
    Torso = 3,
    LeftLeg = 4,
    RightLeg = 5,
}

/// <summary>An area's state: what it costs to cure, too (Injured 1, Broken 2, Gone 3).</summary>
public enum P3AreaState
{
    Okay = 0,
    Injured = 1,
    Broken = 2,
    Gone = 3,
}

/// <summary>
/// Phantasie III's status as the body sets it (characters: Dead 0, Okay 1, Asleep 2, Out 3, Down 4). For a monster
/// <see cref="Out"/> is its "Unconscious" state (head or torso Broken; status 4 in the monster record).
/// </summary>
public enum P3Status
{
    Dead = 0,
    Okay = 1,
    Asleep = 2,
    Out = 3,
    Down = 4,
}

/// <summary>Which row of the hit-location table a hit uses (<c>body-areas.md</c> §1, <c>DS:0x12cc</c>).</summary>
public enum P3LocationRow
{
    /// <summary>A character's bow hit on a monster.</summary>
    Bow = 0,

    /// <summary><c>[?]</c> Not traced to a caller (spells per the manual <c>[~]</c>).</summary>
    Spell = 1,

    /// <summary>A character's melee hit, and every monster hit on a character.</summary>
    Melee = 2,

    /// <summary>A character's Aim blow.</summary>
    Aim = 3,

    /// <summary>Any hit on a monster while the "tall monster" flag is set.</summary>
    Tall = 4,
}

/// <summary>
/// The hit-location odds (<c>DS:0x12cc</c>): 5 rows × 6 <b>cumulative</b> percentages in the monster area order Head,
/// Torso, Right Leg, Right Arm, Left Leg, Left Arm. Data (III's game data), handed in by the caller.
/// </summary>
public sealed record P3HitLocationTable(IReadOnlyList<IReadOnlyList<int>> Rows)
{
    /// <summary>Column k of a row → the area it names (root <c>0xe9ee</c> maps it onto record areas 0, 3, 5, 2, 4, 1).</summary>
    public static IReadOnlyList<P3BodyArea> ColumnAreas { get; } =
        [P3BodyArea.Head, P3BodyArea.Torso, P3BodyArea.RightLeg, P3BodyArea.RightArm, P3BodyArea.LeftLeg, P3BodyArea.LeftArm];

    /// <summary>The area a roll of 1…100 lands on: the first column whose cumulative value is ≥ the roll, else the last.</summary>
    public P3BodyArea AreaFor(P3LocationRow row, int roll)
    {
        var r = Rows[(int)row];
        for (var k = 0; k < 6; k++)
            if (r[k] >= roll)
                return ColumnAreas[k];
        return ColumnAreas[5];
    }

    public P3HitLocationTable Validate()
    {
        ArgumentNullException.ThrowIfNull(Rows);
        if (Rows.Count != 5 || Rows.Any(r => r.Count != 6))
            throw new ArgumentException("Hit locations: 5 rows of 6 cumulative values expected.");
        return this;
    }
}

/// <summary>What one hit did to the body: the area it landed on and whether its state got worse.</summary>
public sealed record P3Injury(P3BodyArea Area, P3AreaState Severity, bool Worsened);

/// <summary>
/// Six body areas (a character's or one monster's) and Phantasie III's body rules (<c>body-areas.md</c> §2–§4): injury from
/// the size of a hit, status from the body, combat restrictions, healing by cure points and the inn. Stored on a
/// <see cref="Character"/> through <see cref="BodyCondition"/> (<see cref="ToCondition"/> / <see cref="FromCondition"/>).
/// </summary>
public sealed class P3BodyAreas
{
    /// <summary>Data ids of the areas in <see cref="BodyCondition.Areas"/>, by <see cref="P3BodyArea"/>.</summary>
    public static IReadOnlyList<string> AreaIds { get; } = ["head", "left-arm", "right-arm", "torso", "left-leg", "right-leg"];

    /// <summary>Data ids of the states, by <see cref="P3AreaState"/>.</summary>
    public static IReadOnlyList<string> StateIds { get; } = ["okay", "injured", "broken", "gone"];

    /// <summary>D3-014 manual: Healing IV / Potion 10 spend 6 points (two Gone areas). Tiers I–III spend 1 / 2 / 3.</summary>
    public const int ManualHealingIvPoints = 6;

    /// <summary>D3-014 DOS: Healing IV / Potion 10 spend 4 points (one Gone + one Injury at most).</summary>
    public const int DosHealingIvPoints = 4;

    /// <summary>Every inn stay of the manual (and DOS's wilderness and independent inns): 1 Break or 2 Injuries.</summary>
    public const int InnPoints = 2;

    private readonly P3AreaState[] _areas = new P3AreaState[6];

    public P3AreaState this[P3BodyArea area]
    {
        get => _areas[(int)area];
        set => _areas[(int)area] = value;
    }

    public P3BodyAreas Clone()
    {
        var c = new P3BodyAreas();
        Array.Copy(_areas, c._areas, 6);
        return c;
    }

    public bool AllOkay => _areas.All(a => a == P3AreaState.Okay);

    /// <summary>The body as data ids for <see cref="Character.BodyCondition"/>.</summary>
    public BodyCondition ToCondition() =>
        new(Enumerable.Range(0, 6).ToDictionary(k => AreaIds[k], k => StateIds[(int)_areas[k]], StringComparer.Ordinal));

    /// <summary>A body read back from data ids; a missing area or unknown state reads as Okay; null → all Okay.</summary>
    public static P3BodyAreas FromCondition(BodyCondition? condition)
    {
        var b = new P3BodyAreas();
        if (condition is null) return b;
        for (var k = 0; k < 6; k++)
            if (condition.Areas.TryGetValue(AreaIds[k], out var s))
            {
                var i = StateIds.ToList().IndexOf(s);
                if (i > 0) b._areas[k] = (P3AreaState)i;
            }
        return b;
    }

    /// <summary>The six states as numbers (<see cref="P3AreaState"/>, area order) for a save.</summary>
    public int[] ToStates() => _areas.Select(a => (int)a).ToArray();

    /// <summary>A body from saved numbers; a short array or an unknown number reads as Okay.</summary>
    public static P3BodyAreas FromStates(IReadOnlyList<int>? states)
    {
        var b = new P3BodyAreas();
        for (var k = 0; k < 6 && states is not null && k < states.Count; k++)
            if (states[k] is >= 0 and <= (int)P3AreaState.Gone)
                b._areas[k] = (P3AreaState)states[k];
        return b;
    }

    /// <summary>The body of <paramref name="member"/> (all Okay when the record has none).</summary>
    public static P3BodyAreas Of(Character member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return FromCondition(member.BodyCondition);
    }

    /// <summary>
    /// How bad a hit of <paramref name="damage"/> on <paramref name="hpBefore"/> HP is (<c>body-areas.md</c> §2):
    /// <c>q = max(0, HP − damage) / max(1, damage)</c>; 0 → Gone, 1 → Broken, 2 → Injured, otherwise none (null).
    /// </summary>
    public static P3AreaState? Severity(int hpBefore, int damage)
    {
        var left = Math.Max(0, hpBefore - damage);
        return (left / Math.Max(1, damage)) switch
        {
            0 => P3AreaState.Gone,
            1 => P3AreaState.Broken,
            2 => P3AreaState.Injured,
            _ => null,
        };
    }

    /// <summary>
    /// The injury step after a hit (<c>damage_character</c> root <c>0xbbb6</c> / <c>damage_monster</c> <c>0xb116</c>), only
    /// for damage &gt; 0 and a hit not flagged "no injury" (breath). A small hit rolls nothing; otherwise <c>rnd(100)</c> picks
    /// the area on <paramref name="row"/> and the area only ever gets worse, straight to the severity. The message (and the
    /// limbless monsters' "body" wording) is the caller's.
    /// </summary>
    public P3Injury? TakeHit(int hpBefore, int damage, P3HitLocationTable table, P3LocationRow row, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (damage <= 0 || Severity(hpBefore, damage) is not { } severity)
            return null;
        var area = table.AreaFor(row, P3Dice.Rnd(rng, 100));
        var worse = this[area] < severity;
        if (worse) this[area] = severity;
        return new P3Injury(area, severity, worse);
    }

    /// <summary>
    /// Status from the body (characters root <c>0x107b</c>, monsters <c>0xec1a</c>), in order: HP ≤ 0 or already dead → Dead;
    /// Head or Torso Gone → Dead; Head or Torso Broken → Out (a monster's Unconscious); already Asleep → Asleep; a leg ≥
    /// Broken → Down; else Okay.
    /// </summary>
    public P3Status Status(int hp, P3Status current)
    {
        if (hp <= 0 || current == P3Status.Dead) return P3Status.Dead;
        return BodyStatus(current == P3Status.Asleep);
    }

    /// <summary>The body part of <see cref="Status"/> (used after healing, which skips the HP and Asleep tests).</summary>
    public P3Status BodyStatus(bool asleep = false)
    {
        if (this[P3BodyArea.Head] == P3AreaState.Gone || this[P3BodyArea.Torso] == P3AreaState.Gone) return P3Status.Dead;
        if (this[P3BodyArea.Head] == P3AreaState.Broken || this[P3BodyArea.Torso] == P3AreaState.Broken) return P3Status.Out;
        if (asleep) return P3Status.Asleep;
        if (this[P3BodyArea.LeftLeg] >= P3AreaState.Broken || this[P3BodyArea.RightLeg] >= P3AreaState.Broken)
            return P3Status.Down;
        return P3Status.Okay;
    }

    /// <summary>Right Arm Broken or Gone: every combat action becomes Parry (root <c>0xb550</c>).</summary>
    public bool ForcesParry => this[P3BodyArea.RightArm] >= P3AreaState.Broken;

    /// <summary>Any arm or leg Broken or Gone: Fire Bow becomes Thrust (<c>bows.md</c> §3).</summary>
    public bool BlocksBow =>
        this[P3BodyArea.LeftArm] >= P3AreaState.Broken || this[P3BodyArea.RightArm] >= P3AreaState.Broken
        || this[P3BodyArea.LeftLeg] >= P3AreaState.Broken || this[P3BodyArea.RightLeg] >= P3AreaState.Broken;

    /// <summary>
    /// D3-031 manual order for equal states, "the most vital area it can" `[~]`: the manual's Inspect order Head, Torso,
    /// Arms, Legs (III PDF 7 / pr.8), left before right.
    /// </summary>
    private static readonly P3BodyArea[] VitalOrder =
    [
        P3BodyArea.Head, P3BodyArea.Torso, P3BodyArea.LeftArm, P3BodyArea.RightArm, P3BodyArea.LeftLeg, P3BodyArea.RightLeg,
    ];

    /// <summary>
    /// Spends <paramref name="points"/> cure points; an area costs its state (Gone 3, Broken 2, Injured 1) and is cured
    /// outright. Manual (D3-031, user s82): each cure picks the worst area the points left can pay for, the most vital one
    /// among equals, starting over every time. Under <c>p3.heal-order</c> = <c>dos</c>, <c>heal_body_areas(P)</c> (root
    /// <c>0xaafc</c>) as written: a cure sets the level to the points left <b>inside</b> the same pass, so a later area of
    /// the new level is cured before an earlier one (with 4 points, Gone at Left Arm and Injured at Head and Torso, the
    /// Torso is cured, not the Head). Points smaller than every area left are lost. Returns the areas cured, in order.
    /// </summary>
    public IReadOnlyList<P3BodyArea> Heal(int points, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.P3DosHealOrder() ? HealDos(points) : HealManual(points);
    }

    private List<P3BodyArea> HealManual(int points)
    {
        var cured = new List<P3BodyArea>();
        var remaining = points;
        while (true)
        {
            P3BodyArea? best = null;
            foreach (var area in VitalOrder)
            {
                var state = (int)this[area];
                if (state == 0 || state > remaining) continue;
                if (best is null || state > (int)this[best.Value]) best = area;
            }
            if (best is not { } pick) return cured;
            remaining -= (int)this[pick];
            this[pick] = P3AreaState.Okay;
            cured.Add(pick);
        }
    }

    private List<P3BodyArea> HealDos(int points)
    {
        var cured = new List<P3BodyArea>();
        var remaining = points;
        var level = points;
        while (level > 0)
        {
            var found = false;
            for (var k = 0; k < 6; k++)
            {
                if (level <= 0 || (int)_areas[k] != level) continue; // level 0 would only "cure" Okay areas
                _areas[k] = P3AreaState.Okay;
                cured.Add((P3BodyArea)k);
                remaining -= level;
                level = remaining;
                found = true;
            }
            if (!found) level--;
        }
        return cured;
    }

    /// <summary>
    /// Cure points of a healing tier (spells Healing I–IV, root <c>0x194d</c>): 1, 2, 3, and for IV 6 (manual, D3-014) or 4
    /// (<c>p3.healing-iv</c> = <c>dos</c>).
    /// </summary>
    public static int HealingCurePoints(int tier, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return tier switch
        {
            1 or 2 or 3 => tier,
            4 => rules.P3DosHealingIv() ? DosHealingIvPoints : ManualHealingIvPoints,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Healing tiers are 1..4."),
        };
    }

    /// <summary>Healing Potion n (1–10, items 101–110, root <c>0x94a5</c>): tier 1 for 1–3, 2 for 4–6, 3 for 7–9, 4 for 10.</summary>
    public static int PotionCurePoints(int potion, GameOptions rules) => potion switch
    {
        >= 1 and <= 3 => HealingCurePoints(1, rules),
        >= 4 and <= 6 => HealingCurePoints(2, rules),
        >= 7 and <= 9 => HealingCurePoints(3, rules),
        10 => HealingCurePoints(4, rules),
        _ => throw new ArgumentOutOfRangeException(nameof(potion), potion, "Healing potions are 1..10."),
    };

    /// <summary>
    /// The body part of an inn stay (ovl2 <c>0x347d</c>; HP, magic and age are the inn's own): manual (D3-015) every inn cures
    /// 1 Break or 2 Injuries (2 cure points); under <c>p3.inn-healing</c> = <c>dos</c> a <b>town</b> inn sets every area
    /// that is not Gone to Okay, while the wilderness and independent inns spend 2 points as in the manual.
    /// </summary>
    public IReadOnlyList<P3BodyArea> InnStay(bool inTown, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!(inTown && rules.P3DosInnHealing()))
            return Heal(InnPoints, rules);
        var cured = new List<P3BodyArea>();
        for (var k = 0; k < 6; k++)
            if (_areas[k] is P3AreaState.Injured or P3AreaState.Broken)
            {
                _areas[k] = P3AreaState.Okay;
                cured.Add((P3BodyArea)k);
            }
        return cured;
    }

    /// <summary>Resurrection (#44) and <c>clear_body_areas</c> (root <c>0x1dbb</c>): all six areas Okay.</summary>
    public void ClearAll() => Array.Clear(_areas);
}
