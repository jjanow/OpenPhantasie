namespace OpenPhantasie.Core;

/// <summary>One ancestor as creation sees it: race, class and the five stored abilities (Str, Int, Dex, Con, Cha).</summary>
/// <param name="HallSlot">The Hall of Glory slot for a Hall person (0–10), null for a "new" person.</param>
public sealed record P4Ancestor(int Race, int Class, IReadOnlyList<int> Abilities, int? HallSlot = null, int Social = 0,
    IReadOnlyList<int>? GameResults = null)
{
    public bool IsHallPerson => HallSlot is not null;

    /// <summary>True when the Hall person completed any of Phantasie I–III (a result ≥ 100; <c>_chkwon</c>).</summary>
    public bool CompletedAGame => GameResults?.Any(r => r >= 100) == true;
}

/// <summary>How the second ancestor is looked for (<c>_make5</c>): S same race, D different race, H a Hall person's race.</summary>
public enum P4SecondAncestorWay { SameRace, DifferentRace, HallPerson }

/// <summary>The outcome of choosing ancestor 2 (<c>_mopta</c> / <c>_moptb</c>).</summary>
/// <param name="NotPossible">"Not possible" (不可能です): creation starts again from ancestor 1.</param>
/// <param name="Sorry">"Sorry, the ancestor you found was …": the S / D roll did not give what was asked.</param>
public sealed record P4SecondAncestorResult(bool NotPossible, int Ancestor2Race = 0, int DescendantRace = 0, bool Sorry = false)
{
    public static P4SecondAncestorResult Impossible { get; } = new(true);

    /// <summary>
    /// s152: how many races <c>_mopta</c> drew while picking (<c>_dsp_race</c> and a <c>_noise</c> for each race that
    /// combines, <c>0x13ACC</c>; 0 when the roll kept ancestor 1's race, and for <c>_moptb</c>).
    /// </summary>
    public int RacesShown { get; init; }
}

/// <summary>
/// Phantasie IV character creation and ancestry (<c>reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md</c>), with
/// D4-001 (Nikademus), D4-010 (social bias), D4-012 (strides) and D4-013 (Gargoyle classes) as <c>p4.*</c> options.
/// </summary>
public static class P4Creation
{
    public const int SameRaceThreshold = 50, DifferentRaceThreshold = 25;

    /// <summary>
    /// <c>_combi</c> (§4): the descendant race of races <paramref name="a"/> and <paramref name="b"/>, null when the pair has
    /// none. The plain table (the code's 4-bit key quirk aliases races ≥ 16, which the shipped game never passes).
    /// </summary>
    public static int? Combine(P4CreationTables t, int a, int b)
    {
        if (a < P4Ids.BaseRaces && b < P4Ids.BaseRaces && a >= 0 && b >= 0)
        {
            var r = t.Combine5x5[a][b];
            return r < 0 ? null : r;
        }
        var (hi, lo) = a > b ? (a, b) : (b, a);
        foreach (var p in t.CombinePairs)
        {
            if (p.A == hi && p.B == lo)
                return p.Result;
        }
        return null;
    }

    /// <summary>The race a Hall person combines as (D4-001: Nikademus counts as Human under the manual).</summary>
    public static int CombiningRace(GameOptions o, P4CreationTables t, P4Ancestor a) =>
        a.HallSlot == t.NikademusHallSlot && !o.P4Code(P4RuleOptions.Nikademus) ? t.NikademusCountsAsRace : a.Race;

    /// <summary>
    /// The classes <paramref name="race"/> may take (class mask, §2 item 3), with D4-013: a Gargoyle may also be a Thief or
    /// Fighter under the manual.
    /// </summary>
    public static bool ClassAllowed(GameOptions o, P4CreationTables t, int race, int cls)
    {
        if (cls < 0 || cls >= P4Ids.PlayerClasses || race < 0 || race >= t.ClassMaskByRace.Count)
            return false;
        var mask = race == t.GargoyleRace && !o.P4Code(P4RuleOptions.GargoyleClasses)
            ? t.GargoyleManualMask
            : t.ClassMaskByRace[race];
        return mask[cls] == '1';
    }

    /// <summary>The combat stride (move base, <c>_moverate</c>): race + class, with D4-012's Halfling 5 / Orc 8 under the manual.</summary>
    public static int Stride(GameOptions o, P4CreationTables t, int race, int cls)
    {
        var r = !o.P4Code(P4RuleOptions.Strides) && t.StrideManualOverrides.TryGetValue(race, out var m) ? m : t.StrideRace[race];
        return r + t.StrideClass[cls];
    }

    /// <summary>
    /// The Random Creature list (<c>_rrace</c>, §2 item 2 / §3.1): the normal list, or with rand(100) &lt; 26 the rare list
    /// (Ogre, Troll, Hill Giant, Gargoyle, Spriggan; without the last two when the guild already has a Hill Giant or
    /// Gargoyle). For ancestor 1 the lists are 3 / 4.
    /// </summary>
    public static IReadOnlyList<int> RandomRaceList(P4CreationTables t, IRandomSource rng, bool forAncestorOne,
        bool guildHasGiantOrGargoyle)
    {
        var rare = P4Dice.Rand(rng, 100) < t.RandomRaceRareChance;
        var list = forAncestorOne ? (rare ? 4 : 3) : rare ? (guildHasGiantOrGargoyle ? 2 : 1) : 0;
        return t.RandomRaceLists[list];
    }

    /// <summary>
    /// One ability roll (<c>_mdice</c>, §5.1): three dice 6·u^k (u = rand(100) / 100), trunc of the sum + 3, + class
    /// modifier capped at 18, + race modifier floored at 3.
    /// </summary>
    public static int RollAbility(P4CreationTables t, IRandomSource rng, int race, int cls, int ability, double k)
    {
        var sum = 0.0;
        for (var i = 0; i < 3; i++)
            sum += 6.0 * Math.Pow(P4Dice.Frand(rng), k);
        var roll = (int)Math.Truncate(sum) + 3;
        roll += t.ClassAbilityMod[cls][ability];
        if (roll > 18)
            roll = 18;
        roll += t.RaceAbilityMod[race][ability];
        return roll < 3 ? 3 : roll;
    }

    /// <summary>A new person's five abilities (<c>_newattr</c>, §5.3: k = 0.6).</summary>
    public static int[] RollNewPerson(P4CreationTables t, IRandomSource rng, int race, int cls) =>
        Enumerable.Range(0, 5).Select(a => RollAbility(t, rng, race, cls, a, t.NewPersonExponent)).ToArray();

    /// <summary>
    /// <c>_get_modifer</c> (§5.2): the exponent for an ancestors' average. Below 5.0 the code runs out of table and returns
    /// a denormal (≈ 5.7e-313; CPU-checked s82), so every die rolls 6. D4-056 (manual silent; user s84): a bug, fixed by
    /// default with the table's last exponent (k = 5.635, weak dice); <c>p4.weak-ancestors</c> = <c>x68000</c> keeps the run-off.
    /// </summary>
    public static double InheritanceExponent(GameOptions o, P4CreationTables t, double average)
    {
        for (var i = 0; i < t.InheritanceExponent.Count; i++)
        {
            if (average >= 18.0 - 0.5 * i)
                return t.InheritanceExponent[i];
        }
        return o.P4Code(P4RuleOptions.WeakAncestors) ? BelowTableExponent : t.InheritanceExponent[^1];
    }

    /// <summary>The double <c>0x0000001B_00000000</c> the exhausted lookup leaves in d0:d1 (ancestry.md §5.2).</summary>
    public static readonly double BelowTableExponent = BitConverter.Int64BitsToDouble(0x0000001B_00000000);

    /// <summary>
    /// <c>_sub_bonus</c>: an ancestor's ability without its own class and race modifiers. Hall persons of class 8 / 9 have no
    /// class row and subtract the bytes the code reads instead (§5.2).
    /// </summary>
    public static int RawAbility(P4CreationTables t, P4Ancestor a, int ability)
    {
        var classMod = a.Class < P4Ids.PlayerClasses ? t.ClassAbilityMod[a.Class][ability]
            : t.SubBonusPseudoRows.TryGetValue(a.Class, out var row) ? row[ability] : 0;
        var raceMod = a.Race < t.RaceAbilityMod.Count ? t.RaceAbilityMod[a.Race][ability] : 0;
        return a.Abilities[ability] - classMod - raceMod;
    }

    /// <summary>The descendant's five abilities (<c>_cmbattr</c>, §5.2).</summary>
    public static int[] Inherit(GameOptions o, P4CreationTables t, IRandomSource rng, P4Ancestor first, P4Ancestor second,
        int race, int cls) =>
        Enumerable.Range(0, 5).Select(a =>
        {
            var avg = (RawAbility(t, first, a) + RawAbility(t, second, a)) / 2.0;
            return RollAbility(t, rng, race, cls, a, InheritanceExponent(o, t, avg));
        }).ToArray();

    /// <summary>
    /// Ancestor 2 by S or D (<c>_mopta</c>, §3.2): with r = rand(100) above the threshold (S 50, D 25) a race x of I's 15
    /// whose combination with ancestor 1 exists is picked at random; otherwise ancestor 2 is ancestor 1's race and so is the
    /// descendant. D4-001: with Nikademus as ancestor 1 the code says "Not possible" (after the race roll, which it makes
    /// first: <c>_mopta</c> tests <c>__reg_fame</c> once the dice are drawn); the manual lets him count as Human.
    /// </summary>
    public static P4SecondAncestorResult SecondBySameOrDifferent(GameOptions o, P4CreationTables t, IRandomSource rng,
        P4Ancestor first, bool same)
    {
        var a = CombiningRace(o, t, first);
        var threshold = same ? SameRaceThreshold : DifferentRaceThreshold;
        var result = new P4SecondAncestorResult(false, a, a, Sorry: !same);
        var shown = 0;
        if (P4Dice.Rand(rng, 100) > threshold)
        {
            var candidates = Enumerable.Range(0, 15).Where(x => Combine(t, a, x) is not null).ToList();
            shown = candidates.Count;
            if (candidates.Count > 0)
            {
                var x = candidates[P4Dice.Rand(rng, candidates.Count) - 1];
                result = new P4SecondAncestorResult(false, x, Combine(t, a, x)!.Value, Sorry: same ? x != a : x == a);
            }
        }
        return (first.HallSlot == t.NikademusHallSlot && o.P4Code(P4RuleOptions.Nikademus)
            ? P4SecondAncestorResult.Impossible
            : result) with { RacesShown = shown };
    }

    /// <summary>
    /// <c>_moptb</c>'s first test: with the code's Nikademus rule and Nikademus as ancestor 1, H says "Not possible" before
    /// the Hall list is shown.
    /// </summary>
    public static bool HallRefusedAtOnce(GameOptions o, P4CreationTables t, P4Ancestor first) =>
        first.HallSlot == t.NikademusHallSlot && o.P4Code(P4RuleOptions.Nikademus);

    /// <summary>
    /// Ancestor 2 from the Hall (<c>_moptb</c>, §3.2 H): "Not possible" when the races do not combine, when the same person is
    /// picked twice, or when both are among the shipped three (slots 0–2); and under the code also whenever Nikademus is
    /// either ancestor (D4-001: the manual lets him count as Human).
    /// </summary>
    public static P4SecondAncestorResult SecondFromHall(GameOptions o, P4CreationTables t, P4Ancestor first, P4Ancestor hall)
    {
        ArgumentNullException.ThrowIfNull(hall);
        if (hall.HallSlot is not { } slot)
            throw new ArgumentException("The second ancestor must be a Hall person.", nameof(hall));
        if (o.P4Code(P4RuleOptions.Nikademus) && (first.HallSlot == t.NikademusHallSlot || slot == t.NikademusHallSlot))
            return P4SecondAncestorResult.Impossible;
        if (first.HallSlot == slot || (first.HallSlot is < 3 && slot < 3))
            return P4SecondAncestorResult.Impossible;
        var race = Combine(t, CombiningRace(o, t, first), CombiningRace(o, t, hall));
        return race is { } r ? new P4SecondAncestorResult(false, hall.Race, r) : P4SecondAncestorResult.Impossible;
    }

    /// <summary>
    /// <c>_get_social</c> (§6 step 1): rand(100) for races 0–4 (rand(75) otherwise) against 29 / 59 / 89. D4-010: under the
    /// manual Elves and Dwarves roll rand(100) + 10, capped at 100.
    /// </summary>
    public static int RollSocial(GameOptions o, P4CreationTables t, IRandomSource rng, int race)
    {
        var r = P4Dice.Rand(rng, race < P4Ids.BaseRaces ? t.SocialDieBaseRaces : t.SocialDieOtherRaces);
        if (!o.P4Code(P4RuleOptions.SocialBias) && t.SocialBiasRaces.Contains(race))
            r = Math.Min(t.SocialBiasCap, r + t.SocialBias);
        var social = 0;
        while (social < t.SocialThresholds.Count && r > t.SocialThresholds[social])
            social++;
        return social;
    }

    /// <summary>
    /// §6 step 2: each Hall ancestor in order — Super Hero (4) makes the descendant Royalty, Dark Lord (5) an Outcast; the
    /// last match wins; other Hall classes and "new" ancestors pass nothing on.
    /// </summary>
    public static int HallSocial(P4CreationTables t, int rolled, IEnumerable<P4Ancestor> ancestors)
    {
        var social = rolled;
        foreach (var a in ancestors.Where(a => a.IsHallPerson))
        {
            if (a.Social == 4)
                social = t.HallSuperHeroGives;
            else if (a.Social == 5)
                social = t.HallDarkLordGives;
        }
        return social;
    }

    /// <summary>Starting gold into the account: social × 256 (§7 item 3; manual p. 25: Noble 768, Peasant 0).</summary>
    public static int StartingGold(P4CreationTables t, int social) => social * t.GoldPerSocial;

    /// <summary>Divine Aid (§7 item 4, <c>_chkwon</c>): known when the last Hall ancestor completed one of I–III.</summary>
    public static bool KnowsDivineAid(IEnumerable<P4Ancestor> ancestors) =>
        ancestors.LastOrDefault(a => a.IsHallPerson)?.CompletedAGame == true;

    /// <summary>A Hall person's descendant count after being used once more (§7 item 4, cap 99).</summary>
    public static int NextDescendantCount(P4CreationTables t, int count) => Math.Min(t.MaxDescendants, count + 1);

    /// <summary>Origin index (<c>_origin</c>) for a character made on map <paramref name="map"/> (§7 item 5) [C] expression.</summary>
    public static int Origin(int map) =>
        2 + (map == 2 ? 1 : 0) + 2 * (map == 9 ? 1 : 0) + 4 * (map > 13 ? 1 : 0) + 3 * (map == 13 ? 1 : 0) - 2 * (map == 8 ? 1 : 0);

    /// <summary>Luck = rand(6) + 8 (9…14; §7 item 9).</summary>
    public static int RollLuck(IRandomSource rng) => P4Dice.Rand(rng, 6) + 8;

    /// <summary>Starting spells (spell numbers) and whether the character carries Darts (§7 item 7).</summary>
    public static (IReadOnlyList<int> Spells, int? Item) StartingKit(P4CreationTables t, int cls) =>
        (t.StartingSpells.TryGetValue(cls, out var s) ? s : [], cls == t.StartingItemExceptClass ? null : t.StartingItem);

    public static bool IsFlier(P4CreationTables t, int race) => t.FlierRaces.Contains(race);
}

/// <summary>
/// A new Phantasie IV character record as <c>_mkdetail</c> builds it (<c>ancestry.md</c> §7): the fields the rules set at
/// creation. Spells are spell numbers; <see cref="LearnableSpells"/> are those the level-1 row opens (state ≥ 2).
/// </summary>
public sealed record P4NewCharacter(
    string Name,
    int Race,
    int Class,
    int Social,
    IReadOnlyList<int> Abilities,
    long Account,
    int Level,
    int HpMax,
    int MagicLevel,
    int MpMax,
    IReadOnlyList<int> Skills,
    IReadOnlyList<int> KnownSpells,
    IReadOnlyList<int> LearnableSpells,
    int? Item,
    int Stride,
    bool Flier,
    int Origin,
    int AgingFactor,
    int AgeUnits,
    IReadOnlyList<int> HallAncestorSlots,
    int Lifespan = 0);

/// <summary>The last step of IV creation: the record built from the chosen race, class and abilities (<c>_mkdetail</c>).</summary>
public static class P4CreationRecord
{
    /// <summary>
    /// Builds the record of a character whose race, class and five abilities are chosen (<c>_mkdetail</c>, §7), in the
    /// code's order: social class (§6, D4-010), gold, Hall ancestors (Divine Aid; the caller bumps their descendant counts,
    /// <see cref="P4NewCharacter.HallAncestorSlots"/>), origin, flier, starting kit, the level-0 level-up (skills, magic
    /// level, HP, learnable spells; level 1), Luck, MP, aging factor and age. <paramref name="agingRace"/> is the race the
    /// aging factor is computed from: the character's own by default; the code reads guild slot 0's race (original bug,
    /// <c>ancestry.md</c> §7 item 10), which a faithful caller passes.
    /// </summary>
    public static P4NewCharacter Create(GameOptions o, P4Tables t, IRandomSource rng, string name, int race, int cls,
        IReadOnlyList<int> abilities, IReadOnlyList<P4Ancestor> ancestors, int map, int? agingRace = null)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(ancestors);
        if (abilities.Count != 5)
            throw new ArgumentException("Five abilities (Str, Int, Dex, Con, Cha) are chosen at creation.", nameof(abilities));
        if (!P4Creation.ClassAllowed(o, t.Creation, race, cls))
            throw new ArgumentException($"Race {race} may not take class {cls}.", nameof(cls));
        return Build(o, t, rng, name, race, cls, abilities, ancestors, map, agingRace);
    }

    /// <summary>
    /// <see cref="Create"/> without the class-mask test: the record <c>_mkdetail</c> builds for any of IV's player classes
    /// 0–5 and races 0–14 (T5's III → IV Guild join keeps a III character's class, <c>ancestry.md</c> section 8.3 <c>[~]</c>).
    /// </summary>
    public static P4NewCharacter Build(GameOptions o, P4Tables t, IRandomSource rng, string name, int race, int cls,
        IReadOnlyList<int> abilities, IReadOnlyList<P4Ancestor> ancestors, int map, int? agingRace = null)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(ancestors);
        if (abilities.Count != 5)
            throw new ArgumentException("Five abilities (Str, Int, Dex, Con, Cha) are chosen at creation.", nameof(abilities));
        if (cls is < 0 or >= P4Ids.PlayerClasses || race < 0 || race >= t.Creation.ClassMaskByRace.Count)
            throw new ArgumentOutOfRangeException(nameof(race), $"No record for race {race}, class {cls}.");
        var c = t.Creation;
        var social = P4Creation.HallSocial(c, P4Creation.RollSocial(o, c, rng, race), ancestors);
        var (startSpells, item) = P4Creation.StartingKit(c, cls);
        var known = startSpells.ToList();
        if (P4Creation.KnowsDivineAid(ancestors))
            known.Add(c.DivineAidSpell);

        // _calpow(rec, 0): skills from 0 with the level-0 growth column, magic level, HP, the spells level 1 opens.
        int[] six = [.. abilities, 0]; // Luck is rolled after the level-up and feeds no skill bonus
        var skills = Enumerable.Range(0, P4Ids.Skills).Select(k =>
            P4Training.SkillValue(t.Training, cls, (P4Skill)k, 0, 0, P4Training.SkillBonus((P4Skill)k, six, race))).ToList();
        var magicLevel = P4Training.MagicLevel(t.Training, cls, 0);
        var hp = P4Training.NewMaxHp(o, t.Training, rng, cls, 0, abilities[3], 0);
        var learnable = P4Training.SpellsGranted(o, t.Training, cls, 0).Where(s => !known.Contains(s)).ToList();

        var luck = P4Creation.RollLuck(rng);
        var factor = P4Aging.AgingFactor(t.Aging, agingRace ?? race, luck);
        // QoL shared-aging (13-aging-normalised.md): the shared lifespan, and I's start-age shape on the same one draw.
        var lifespan = P4Aging.SharedLifespan(o, t, race, luck);
        var age = lifespan > 0 ? P4Aging.SharedStartingAge(rng, lifespan) : P4Aging.StartingAge(rng, factor);
        return new P4NewCharacter(
            name, race, cls, social, [.. abilities, luck], P4Creation.StartingGold(c, social), 1, hp, magicLevel,
            (int)Math.Truncate(0.75 * magicLevel), skills, known, learnable, item, P4Creation.Stride(o, c, race, cls),
            P4Creation.IsFlier(c, race), P4Creation.Origin(map), factor, age,
            ancestors.Where(a => a.IsHallPerson).Select(a => a.HallSlot!.Value).ToList(), lifespan);
    }
}
