namespace OpenPhantasie.Core;

/// <summary>What a town entry's age check does to one character (<c>_townent</c>, <c>towns.md</c> §2.2).</summary>
public enum P4AgeEvent { None, GrownOlder, DiedOfOldAge }

/// <summary>
/// Phantasie IV aging (<c>towns.md</c> §2.2, <c>ancestry.md</c> §7 items 10–11): ages are kept in 1/26 years (record
/// <c>+1E</c>). D4-033 (lifespans) and D4-034 (what growing older does) are <c>p4.*</c> options.
/// </summary>
public static class P4Aging
{
    /// <summary>Age units per year (record <c>+1E</c>).</summary>
    public const int UnitsPerYear = 26;

    /// <summary>The age cap of record <c>+1E</c> (<c>_townent</c>: 65,000).</summary>
    public const int AgeCap = 65000;

    /// <summary>
    /// The X68000 aging factor F (record <c>+20</c>): Luck × the race factor (Elf 19, Dwarf / Halfling / Gnome 2.5, Human /
    /// Pixie / Sprite 1, races 7 and up ÷ 2.5), truncated, cap 255. The code tests the race of guild slot 0 instead of the
    /// new character's (original bug, <c>ancestry.md</c> §7 item 10): a faithful caller passes that race.
    /// </summary>
    public static int AgingFactor(P4AgingTables t, int race, int luck)
    {
        var f = t.FactorByRace.TryGetValue(race, out var x) ? x : t.FactorOther;
        return Math.Min(t.FactorCap, (int)Math.Truncate(luck * f));
    }

    /// <summary>
    /// The age cap: the record's 65,000 (<see cref="AgeCap"/>, about 2,500 years), lifted under QoL <c>shared-aging</c>,
    /// whose Elves live 2,700–3,300 years (the remaster keeps the age in an int; nothing writes the u16 record).
    /// </summary>
    public static int AgeCapFor(GameOptions o) => o.SharedAging ? int.MaxValue : AgeCap;

    /// <summary>
    /// QoL <c>shared-aging</c> (<c>13-aging-normalised.md</c>): the lifespan in age units from the shared model
    /// (<see cref="SharedAgingModel.Lifespan"/>, IV's Luck 9–14); 0 with the option off or no shared data.
    /// </summary>
    public static int SharedLifespan(GameOptions o, P4Tables t, int race, int luck) =>
        o.SharedAging && t.SharedAging is { } s ? s.Lifespan(GameId.P4, race, luck) : 0;

    /// <summary>
    /// QoL <c>shared-aging</c>: the start age has I's shape (10 years + 10–12.5 % of the lifespan,
    /// <see cref="CharacterCreation.StartingAgeClock"/>) on IV's own draw: trunc(260 + L (0.1 + (rand(100) − 1) / 4000)),
    /// so 10 years + 10–12.475 % of the lifespan L, and the draws after it stay where they were.
    /// </summary>
    public static int SharedStartingAge(IRandomSource rng, int lifespan) =>
        (int)Math.Truncate(260 + lifespan * (0.1 + (P4Dice.Rand(rng, 100) - 1) / 4000.0));

    /// <summary>Starting age in 1/26 years: trunc(260 + 256 F (0.1 + (rand(100) − 1) / 4000)) (§7 item 11).</summary>
    public static int StartingAge(IRandomSource rng, int agingFactor) =>
        (int)Math.Truncate(260 + 256.0 * agingFactor * (0.1 + (P4Dice.Rand(rng, 100) - 1) / 4000.0));

    /// <summary>
    /// D4-033 (manual): the death age in years = the race's base (Elf 2,000, Dwarf / Gnome 200, Human 80, every other race
    /// 45) × (0.9 + 0.04 (Luck − 9)), Luck clamped to 9…14, so ±10 % (values set s83, manual-silent detail).
    /// </summary>
    public static double ManualLifespanYears(P4AgingTables t, int race, int luck)
    {
        var baseYears = t.ManualBaseYears.TryGetValue(race, out var b) ? b : t.ManualBaseYearsOther;
        var l = Math.Clamp(luck, t.ManualLuckMin, t.ManualLuckMax);
        return baseYears * (t.ManualSpreadMin + t.ManualSpreadPerLuck * (l - t.ManualLuckMin));
    }

    /// <summary>
    /// The town-entry check for one living character. Manual (D4-033): grown older at 70 % of the lifespan, dead at 100 %.
    /// X68000: A = age / 256 as a signed 16-bit shift (negative from age 32,768: skipped for good); grown older at A ≥ 0.7 F,
    /// dead at A ≥ F. "Grown older" happens once (<paramref name="hasAged"/>); the code's re-scaling of an already-aged
    /// member with a left-over factor is a quirk not reproduced <c>[~]</c>.
    /// </summary>
    /// <param name="lifespan">
    /// QoL <c>shared-aging</c>: the member's stored lifespan in age units (<see cref="P4Member.Lifespan"/>); under the option
    /// it decides (I's comparisons, <see cref="Aging.IsPastDecline"/> / <see cref="Aging.IsPastLifespan"/>) and Luck plays no
    /// part, so a decline never shortens it; 0 = none, never ages out.
    /// </param>
    public static P4AgeEvent Check(GameOptions o, P4AgingTables t, int race, int luck, int agingFactor, int ageUnits,
        bool hasAged, int lifespan = 0)
    {
        bool older, dead;
        if (o.SharedAging)
        {
            if (lifespan <= 0)
                return P4AgeEvent.None;
            dead = Aging.IsPastLifespan(ageUnits, lifespan);
            older = Aging.IsPastDecline(ageUnits, lifespan);
        }
        else if (o.P4Code(P4RuleOptions.Lifespans))
        {
            var a = (short)(ushort)ageUnits >> 8;
            if (a < 0)
                return P4AgeEvent.None;
            dead = a >= agingFactor;
            older = a >= t.DeclineAt * agingFactor;
        }
        else
        {
            var years = ageUnits / (double)UnitsPerYear;
            var life = ManualLifespanYears(t, race, luck);
            dead = years >= life;
            older = years >= t.DeclineAt * life;
        }
        if (dead)
            return P4AgeEvent.DiedOfOldAge;
        return older && !hasAged ? P4AgeEvent.GrownOlder : P4AgeEvent.None;
    }

    /// <summary>
    /// D4-034: what "grown older" does to the six abilities and the defence byte (<c>+2F…+35</c>): × 0.8 truncated once
    /// (<c>msx2</c>, the default) or 0 (<c>x68000</c>: 0.8 converted to the integer 0). Death of old age sets them to 0.
    /// </summary>
    public static int ScaleOnAging(GameOptions o, int value) =>
        o.P4X68000Aging() && !o.SharedAging ? 0 : value * 80 / 100; // shared-aging: × 4/5, as I and MSX2

    /// <summary>Age units a town entry adds: trunc(days / 0x700) from the party's day counter (<c>_par</c>+16).</summary>
    public static int AgeUnitsFromDays(P4AgingTables t, int days) => days / t.DaysPerAgeUnit;
}
