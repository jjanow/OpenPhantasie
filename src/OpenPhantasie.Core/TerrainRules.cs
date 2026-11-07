namespace OpenPhantasie.Core;

/// <summary>What a wilderness step onto a terrain class does, beyond moving and costing time (per game).</summary>
public enum TerrainKind
{
    /// <summary>A plain cell: the random-encounter roll.</summary>
    Ordinary,

    /// <summary>
    /// Phantasie I class 7 (OV2 <c>0x6094</c>): a certain fight once per trip against monster
    /// <c>MonsterPerDigit·T + MonsterOffset</c> (0-based; I: <c>12·T + 9</c>).
    /// </summary>
    FixedFightOncePerTrip,

    /// <summary>Phantasie I class 11: the magical pool, handed to the campaign (<see cref="OverworldMoveKind.SpecialSite"/>).</summary>
    SpecialSite,

    /// <summary>Water (class 10 in I and II): every member rolls swim (<see cref="Swimming"/>).</summary>
    Swim,

    /// <summary>
    /// Phantasie II mist and dark voids (classes 11 and 7, <c>terrain.md</c> §3): while the trip's fight flag is clear,
    /// <c>r ≤ FightRollMax</c> starts a fight against <c>MonsterPerDigit·T + MonsterOffset + r′ / MonsterSpread</c>
    /// (0-based), and sets the flag; no random-encounter roll, no damage.
    /// </summary>
    TerrainFight,

    /// <summary>
    /// Phantasie II molten lava (class 8, <c>terrain.md</c> §4): every living member takes <c>r·T / DamageDivisor + 1</c>;
    /// digit 0 is harmless; no random-encounter roll (<see cref="Lava"/>).
    /// </summary>
    Lava,
}

/// <summary>How the wilderness step rolls its random numbers.</summary>
public enum TerrainRoll
{
    /// <summary>Phantasie I's Core rolls: <c>Next(0, 100)</c> (DOS <c>rand / 0x148</c>, 0…99).</summary>
    Percent,

    /// <summary>Phantasie II: <c>rand / 327</c> (<c>0xfd40</c>), 0…100, where 100 has weight 68 / 32768.</summary>
    Rand327,
}

/// <summary>Drawing a percent roll on a game's scale.</summary>
public static class TerrainRollExtensions
{
    /// <summary><see cref="TerrainRoll.Percent"/>: <c>Next(0, 100)</c>; <see cref="TerrainRoll.Rand327"/>: <c>rand / 327</c>, 0…100.</summary>
    public static int Draw(this TerrainRoll roll, IRandomSource rng) =>
        roll == TerrainRoll.Rand327 ? rng.Next(0, 32768) / 327 : rng.Next(0, 100);
}

/// <summary>One terrain class's rule (<see cref="TerrainRules.Classes"/>); the numbers each kind uses, 0 where unused.</summary>
public sealed record TerrainClassRule(
    TerrainKind Kind,
    int MonsterPerDigit = 0,
    int MonsterOffset = 0,
    int FightRollMax = 0,
    int MonsterSpread = 0,
    int DamageDivisor = 0);

/// <summary>
/// The per-game terrain rules of the wilderness step (<c>terrain</c> in <c>GameData/&lt;game&gt;/rules.json</c>): which
/// terrain class does what, and how the step rolls. The same tile number means different rules per game (I's class 7 is a
/// fixed fight and class 11 the pool; II's are a 9 % terrain fight, and II's class 8 is lava), so the rules are data.
/// Towns (40), wilderness inns (41), dungeons (class 5) and the digit cases 6/7/8 (text, fixed fight, plain move) are the
/// same in I and II and stay in <see cref="OverworldTraversal.TryMove"/>.
/// </summary>
public sealed record TerrainRules(
    IReadOnlyDictionary<int, TerrainClassRule> Classes,
    TerrainRoll Roll,
    int RandomEncounterBelow)
{
    /// <summary>
    /// Phantasie I (OV2 <c>0x5ea2</c>, <c>overworld-movement.md</c>): class 7 fixed fight <c>12·T + 9</c>, class 10 swim,
    /// class 11 pool; 6 % random roll. The shipped <c>phantasie1/rules.json</c> holds the same values (pinned by a test).
    /// </summary>
    public static TerrainRules PhantasieI { get; } = new(
        new Dictionary<int, TerrainClassRule>
        {
            [7] = new(TerrainKind.FixedFightOncePerTrip, MonsterPerDigit: 12, MonsterOffset: 9),
            [10] = new(TerrainKind.Swim),
            [11] = new(TerrainKind.SpecialSite),
        },
        TerrainRoll.Percent,
        RandomEncounterBelow: 6);

    /// <summary>The rule of <paramref name="terrainClass"/>; <see cref="TerrainKind.Ordinary"/> when none is listed.</summary>
    public TerrainClassRule For(int terrainClass) =>
        Classes.TryGetValue(terrainClass, out var r) ? r : OrdinaryRule;

    private static readonly TerrainClassRule OrdinaryRule = new(TerrainKind.Ordinary);

    /// <summary>One roll on this game's scale (<see cref="Roll"/>).</summary>
    public int RollOnce(IRandomSource rng) =>
        Roll == TerrainRoll.Rand327 ? rng.Next(0, 32768) / 327 : rng.Next(0, 100);

    public bool Equals(TerrainRules? other) =>
        other is not null && Roll == other.Roll && RandomEncounterBelow == other.RandomEncounterBelow
        && Classes.Count == other.Classes.Count
        && Classes.All(kv => other.Classes.TryGetValue(kv.Key, out var v) && v == kv.Value);

    public override int GetHashCode() => HashCode.Combine(Roll, RandomEncounterBelow, Classes.Count);

    /// <summary>Throws <see cref="ArgumentException"/> on a rule its kind can't use.</summary>
    public TerrainRules Validate()
    {
        ArgumentNullException.ThrowIfNull(Classes);
        foreach (var (cls, r) in Classes)
        {
            if (cls is < 0 or > 25) throw new ArgumentException($"terrain: class {cls} is not a terrain class (0..25).");
            if (cls == 5)
                throw new ArgumentException("terrain: class 5 (dungeons) is the step's own, not a rule.");
            switch (r.Kind)
            {
                case TerrainKind.TerrainFight when r.MonsterSpread <= 0 || r.FightRollMax < 0:
                    throw new ArgumentException($"terrain: class {cls} terrain fight needs monster_spread > 0 and fight_roll_max ≥ 0.");
                case TerrainKind.Lava when r.DamageDivisor <= 0:
                    throw new ArgumentException($"terrain: class {cls} lava needs damage_divisor > 0.");
            }
        }
        if (RandomEncounterBelow < 0) throw new ArgumentException("terrain: random_encounter_below must not be negative.");
        return this;
    }
}

/// <summary>
/// Phantasie II's lava step (<c>terrain.md</c> §4, state 8 = <c>0xfac2(0)</c>) [C][L]: title " L A V A :", then for every
/// party slot in order a living member takes <c>r·T / 10 + 1</c> (<c>r</c> = <c>rand / 327</c>, rolled per member), printed
/// before it is applied; a member at HP ≤ 0 dies ("DIES" replaces its TAKES row). No skill or save roll, but the routine it
/// shares with swimming (<c>0xfac2</c>) draws each member's swim roll first and ignores it (s83 [C]).
/// </summary>
public static class Lava
{
    /// <summary>Damage for one member: <c>r·T / divisor + 1</c> (tile 81 → 1–11, 82 → 1–21, 83 → 1–31, 85 → 1–51).</summary>
    public static int Damage(int rnd, int digit, int divisor = 10) => rnd * digit / divisor + 1;

    /// <summary>Burn the party; true when nobody is left alive (the party-wiped state 6).</summary>
    public static bool Resolve(Party party, int digit, TerrainClassRule rule, IRandomSource rng, List<string> log)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);
        log.Add(GameText.S("core.overworld.lava"));
        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Dead)
                continue; // dead members take nothing and get no line
            _ = rng.Next(0, 32768) / 327; // [C] 0xfb5c: the shared swim/lava loop draws the swim roll first, unused for lava
            var d = Damage(rng.Next(0, 32768) / 327, digit, rule.DamageDivisor);
            var takes = log.Count;
            log.Add(GameText.F("core.combat.x_takes_x_damage", m.Name, d));
            CombatResolver.ApplyPartyDamage(party, m, d, DamageSource.Lava, rng, log);
            if (m.Status == CharacterStatus.Dead)
                log[takes] = GameText.F("core.combat.x_dies", m.Name); // [C] 0xfc92: "DIES" is printed over the TAKES row
        }
        return party.Members.All(m => m.Status == CharacterStatus.Dead);
    }
}
