namespace OpenPhantasie.Core;

/// <summary>
/// The numbers of Phantasie II's Toss Rock and its TOSS skill (<c>toss-rock.md</c>; data: <c>toss_rock</c> in
/// <c>GameData/phantasie2/rules.json</c>). The module is II's, so these are II's values in every game that switches it on;
/// <see cref="PhantasieII"/> holds the same numbers as the shipped JSON (a test pins the two together).
/// </summary>
/// <param name="BonusBase">TOSS bonus <c>@62</c> constant term (10).</param>
/// <param name="BonusPerLevel">… times the level being left (2).</param>
/// <param name="BonusPerDex">… times DEX (4).</param>
/// <param name="BonusPerStr">… times STR (1).</param>
/// <param name="RaceBonus">… plus a bonus for the displayed race (DWARF 15, GNOME 30).</param>
/// <param name="CoefficientColumn">The skill-coefficient column of the creation tables TOSS grows by (6: the record slot
/// <c>@42</c>; 0 everywhere except MONK at level 9).</param>
/// <param name="RockDamage">The rock's own damage <c>trunc(RockDamage · rnd / 100)</c> before the monster's armor (12).</param>
/// <param name="TargetTries">How many slot picks a toss makes before it misses for want of a living target (3).</param>
public sealed record TossRockRules(
    int BonusBase,
    int BonusPerLevel,
    int BonusPerDex,
    int BonusPerStr,
    IReadOnlyDictionary<Race, int> RaceBonus,
    int CoefficientColumn,
    int RockDamage,
    int TargetTries)
{
    /// <summary>PHANT.PRG <c>level_up 0x0c71a</c> (bonus, growth) and the toss block <c>0x0772e…0x079aa</c> [C].</summary>
    public static TossRockRules PhantasieII { get; } = new(
        BonusBase: 10, BonusPerLevel: 2, BonusPerDex: 4, BonusPerStr: 1,
        RaceBonus: new Dictionary<Race, int> { [Race.Dwarf] = 15, [Race.Gnome] = 30 },
        CoefficientColumn: 6, RockDamage: 12, TargetTries: 3);

    /// <summary>
    /// <c>@62 = 2·L + 4·DEX + STR + 10</c> (+15 DWARF, +30 GNOME; the displayed race), <paramref name="fromLevel"/> = the level
    /// being left (0 at creation) [C] live (KLEPTO GNOME DEX 17 STR 18, level 0 → 126).
    /// </summary>
    public int Bonus(Character m, int fromLevel)
    {
        ArgumentNullException.ThrowIfNull(m);
        return BonusPerLevel * fromLevel + BonusPerDex * m.Stats.Dex + BonusPerStr * m.Stats.Str + BonusBase
               + (RaceBonus.TryGetValue(m.Race, out var b) ? b : 0);
    }

    /// <summary>
    /// TOSS growth at creation and every level-up (<c>toss-rock.md</c> §3), run before the other skills: the bonus is
    /// recomputed from the current stats, then <c>TOSS = max(TOSS, min(255, ((coef + TOSS) &gt;&gt; 1) + @62 − 49))</c>.
    /// </summary>
    public void Grow(Character m, int fromLevel, CharacterCreationTables tables)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(tables);
        m.TossRockBonus = Bonus(m, fromLevel);
        var row = tables.SkillCoefficientsByClassLevel[(int)m.Class][fromLevel];
        var coef = CoefficientColumn < row.Count ? row[CoefficientColumn] : 0;
        var t = ((coef + m.TossRock) >> 1) + m.TossRockBonus - 49;
        m.TossRock = Math.Max(m.TossRock, Math.Min(t, 255));
    }

    public bool Equals(TossRockRules? other) =>
        other is not null
        && (BonusBase, BonusPerLevel, BonusPerDex, BonusPerStr, CoefficientColumn, RockDamage, TargetTries)
        == (other.BonusBase, other.BonusPerLevel, other.BonusPerDex, other.BonusPerStr, other.CoefficientColumn,
            other.RockDamage, other.TargetTries)
        && RaceBonus.Count == other.RaceBonus.Count
        && RaceBonus.All(kv => other.RaceBonus.TryGetValue(kv.Key, out var v) && v == kv.Value);

    public override int GetHashCode() => HashCode.Combine(BonusBase, BonusPerDex, RockDamage, RaceBonus.Count);

    /// <summary>Throws <see cref="ArgumentException"/> on a value no formula can use.</summary>
    public TossRockRules Validate()
    {
        ArgumentNullException.ThrowIfNull(RaceBonus);
        if (CoefficientColumn is < 0 or >= CharacterSkills.RecordSlotCount)
            throw new ArgumentException($"toss_rock: coefficient column must be 0..{CharacterSkills.RecordSlotCount - 1}.");
        if (RockDamage < 0) throw new ArgumentException("toss_rock: rock damage must not be negative.");
        if (TargetTries < 1) throw new ArgumentException("toss_rock: at least one target try.");
        return this;
    }
}

/// <summary>
/// Phantasie II's "7) Toss Rock" as a feature module (<see cref="FeatureModuleRegistry.RockThrowing"/>, <c>toss-rock.md</c>):
/// any OK member, any class, any rank, unlimited rocks; hit on <c>rnd ≤ TOSS</c>; damage from the rock less the monster's
/// armor plus a share of <c>@28</c>. The order is packed as <see cref="CombatAction.Toss"/> (the ST packs <c>−rank</c>;
/// the remaster uses a positive block so I's round loop, which skips every order ≤ 4, still runs it).
/// </summary>
public sealed class P2RockThrowingModule : ICombatActionModule
{
    private static readonly CombatChoice[] MenuOrder = [CombatChoice.Toss];

    public P2RockThrowingModule(TossRockRules? rules = null) => Rules = rules ?? TossRockRules.PhantasieII;

    public TossRockRules Rules { get; }

    public FeatureModuleDescriptor Descriptor => FeatureModuleRegistry.RockThrowing;

    public IReadOnlyList<CombatChoice> Choices => MenuOrder;

    /// <summary>
    /// TOSS for any member at the chosen rank, no roll [C] (<c>combat_choose_action 0x0f298</c> case 6, <c>pick_rank
    /// 0x0f14c</c>). A repeated order's rank is clamped to the living ranks by the caller, as the ST's repeat does.
    /// </summary>
    public bool TryChoose(in CombatOrderRequest request, IRandomSource rng, out int packedAction)
    {
        if (request.Choice != CombatChoice.Toss)
        {
            packedAction = CombatAction.Cleared;
            return false;
        }
        packedAction = CombatAction.Toss(Math.Clamp(request.RankIndex, 0, CombatAction.MaxTossRank));
        return true;
    }

    public bool TryResolve(
        CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng, List<string> log)
    {
        if (!CombatAction.TryDecodeToss(packedAction, out var rankIndex))
            return false;
        Resolve(encounter, memberIndex, rankIndex, rng, log);
        return true;
    }

    /// <summary>
    /// One toss, <c>toss-rock.md</c> §2 [C] (block <c>0x0772e…0x079aa</c>). RNG order: one <c>rnd</c> per slot try, then the
    /// hit roll, then the rock roll, then the <c>@28</c> roll; <c>rnd</c> = <c>rand / 327</c>, 0…100.
    /// </summary>
    public void Resolve(CombatEncounter encounter, int memberIndex, int rankIndex, IRandomSource rng, List<string> log)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);
        var party = encounter.Party;
        var ch = party.Members[memberIndex];
        log.Add(GameText.F("core.combat.x_throws_a_rock", ch.Name));

        // [C] The group as the ST holds it: every monster death runs 0xb16e (called from the damage routine at 0x9b20,
        // I's 0x1694), which moves the living monsters to the front and recounts, so the slots are the living monsters in
        // order: a living rank costs one slot draw, a rank emptied earlier this round three, then the toss misses (live s78).
        var living = rankIndex < encounter.Ranks.Count
            ? encounter.Ranks[rankIndex].Members.Where(m => m.IsAlive).ToList()
            : [];
        Monster? target = null;
        for (var t = 0; t < Rules.TargetTries && target is null; t++)
        {
            var k = TargetSlot(memberIndex, party.Members.Count, living.Count - 1, Rnd(rng));
            if (k >= 0 && k < living.Count && living[k].IsAlive)
                target = living[k];
        }
        if (target is null || Rnd(rng) > ch.TossRock)
        {
            log.Add(GameText.F("core.combat.x_misses", ch.Name));
            return;
        }

        var dmg = Damage(Rnd(rng), Rnd(rng), target.Armor, ch.WeaponDamageA, Rules.RockDamage);
        // dmg before the floor: presentation only (II ST sparkles the rock sprite dmg / 32 + 1 times first, 0x7964 -> 0x18b16).
        CombatResolver.ApplyMonsterDamage(encounter, target, CombatMath.ApplyDamageFloor(dmg), DamageSource.PartyRock, rng, log,
            spellDamage: dmg);
    }

    /// <summary>
    /// The aimed slot <c>trunc((i + 0.02·rnd) / (N + 2) · (L + 1))</c>: thrower <paramref name="memberIndex"/> of
    /// <paramref name="partySize"/>, group last slot <paramref name="lastSlot"/> [C]. Computed as the ST does, in its FFP
    /// routines and in this order (<c>0x77bc..0x784e</c>, <see cref="MotorolaFfp"/>): 58 of the 42,420 cases of
    /// N 1..7, rnd 0..100, L + 1 ≤ 15 land one slot lower than exact arithmetic (s83, checked against the program's own
    /// code in a 68000 emulator). An emptied rank (L = −1) gives 0, a dead slot.
    /// </summary>
    public static int TargetSlot(int memberIndex, int partySize, int lastSlot, int rnd)
    {
        var sum = MotorolaFfp.Add(MotorolaFfp.FromInt(memberIndex), MotorolaFfp.Mul(MotorolaFfp.FromInt(rnd), MotorolaFfp.Two100ths));
        var share = MotorolaFfp.Div(sum, MotorolaFfp.FromInt(partySize + 2));
        return MotorolaFfp.ToInt(MotorolaFfp.Mul(share, MotorolaFfp.FromInt(Math.Max(0, lastSlot + 1))));
    }

    /// <summary>
    /// <c>max(0, trunc(12·r1/100) − armor) + trunc(r2·@28/100)</c> before the 1-point floor [C]; <c>@28</c> =
    /// <see cref="Character.WeaponDamageA"/> (STRENGTH, which raises the fight-start copy, does not help).
    /// </summary>
    public static int Damage(int rockRoll, int strengthRoll, int monsterArmor, int weaponDamageA, int rockDamage = 12) =>
        Math.Max(0, rockDamage * rockRoll / 100 - monsterArmor) + strengthRoll * weaponDamageA / 100;

    /// <summary><c>rand() / 327</c> (<c>0x0fd40</c>): 0…100, where 100 has weight 68 / 32768.</summary>
    public static int Rnd(IRandomSource rng) => rng.Next(0, 32768) / 327;
}
