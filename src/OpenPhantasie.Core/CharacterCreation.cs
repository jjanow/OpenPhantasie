namespace OpenPhantasie.Core;

/// <summary>
/// DOS-style 0..32767 word RNG used by creation formulas (<c>floor(rand / N)</c>).
/// </summary>
public interface IDosRandomSource : IRandomSource
{
    /// <summary>Uniform integer in <c>[0, 32768)</c>.</summary>
    int NextDosWord();
}

public sealed class SeededDosRandomSource : IDosRandomSource
{
    private readonly Random _rng;

    public SeededDosRandomSource(int seed) => _rng = new Random(seed);

    public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    public int NextDosWord() => _rng.Next(0, 32768);
}

/// <summary>
/// Static creation tables (from TWNS.INT). Indexed by class/race enums.
/// </summary>
public sealed class CharacterCreationTables
{
    public required IReadOnlyList<IReadOnlyList<int>> HpFactorsByClass { get; init; }
    /// <summary>MP ceiling by class and level — <c>TWNS.INT @0x1c2</c>, the manual's "Maximum Magic Points" table.</summary>
    public required IReadOnlyList<IReadOnlyList<int>> MpCeilingByClass { get; init; }
    /// <summary>Skill coefficients <c>[class][level 0..14][skill slot 0..9]</c> — <c>TWNS.INT @0x21c</c>.</summary>
    public required IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> SkillCoefficientsByClassLevel { get; init; }

    /// <summary>
    /// Level at which each spell becomes learnable, <c>[class][spell]</c>, 0 = never —
    /// <c>TWNS.INT @0x05a</c> with the D-006 Monk change applied in the data.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<int>> SpellLevelsByClass { get; init; }

    /// <summary>Remaster lifespan band per race in years, (lo, hi) — DIFFERENCES.md D-003.</summary>
    public required IReadOnlyList<(int Lo, int Hi)> LifespanBandsByRace { get; init; }
    public required IReadOnlyList<IReadOnlyList<int>> ClassAttributeModifiers { get; init; }
    public required IReadOnlyList<IReadOnlyList<int>> RaceAttributeModifiers { get; init; }

    /// <summary>
    /// The DOS cells and lifespan rule these tables replace by the manual (<c>dos_tables</c> in <c>creation-tables.json</c>;
    /// D-001, D-003, D-006). Null when the data has none. Applied per save by <see cref="ForRules"/>.
    /// </summary>
    public DosCreationTables? Dos { get; init; }

    /// <summary>
    /// D-003 <c>dos</c> (<c>p1.lifespans</c>): when set, <see cref="CharacterCreation.LifespanFor"/> follows DOS,
    /// <c>@252 = @10 · num / den</c> in <see cref="Aging.DosLifespanStep"/> steps, instead of <see cref="LifespanBandsByRace"/>.
    /// </summary>
    public IReadOnlyList<(int Num, int Den)>? LifespanFactorsByRace { get; init; }

    /// <summary>
    /// These tables with the chosen DOS sides of <see cref="Dos"/> applied (P3.2 Authentic mode, locked per save):
    /// <paramref name="dosRaceModifiers"/> = D-001 (Halfling DEX 11), <paramref name="dosSpellLevels"/> = D-006 (Monk SLEEP
    /// at level 7), <paramref name="dosLifespans"/> = D-003 (DOS lifespan factors). This instance when none applies or the
    /// data has no DOS block.
    /// </summary>
    public CharacterCreationTables ForRules(bool dosRaceModifiers, bool dosSpellLevels, bool dosLifespans,
        GameId? sharedAging = null)
    {
        var shared = sharedAging is not null && SharedAging is not null;
        if (!shared && (Dos is not { } || !(dosRaceModifiers || dosSpellLevels || dosLifespans)))
            return this;
        var dos = Dos;
        return new CharacterCreationTables
        {
            HpFactorsByClass = HpFactorsByClass,
            MpCeilingByClass = MpCeilingByClass,
            SkillCoefficientsByClassLevel = SkillCoefficientsByClassLevel,
            SpellLevelsByClass = dos is not null && dosSpellLevels ? Patch(SpellLevelsByClass, dos.SpellLevels) : SpellLevelsByClass,
            // QoL shared-aging: the shared model's bands, whatever the game's own lifespans option says.
            LifespanBandsByRace = shared ? SharedAging!.BandsByRace : LifespanBandsByRace,
            ClassAttributeModifiers = ClassAttributeModifiers,
            RaceAttributeModifiers = dos is not null && dosRaceModifiers
                ? Patch(RaceAttributeModifiers, dos.RaceAttributeModifiers)
                : RaceAttributeModifiers,
            Dos = dos,
            LifespanFactorsByRace = shared ? null
                : dos is not null && dosLifespans && dos.LifespanFactorsByRace.Count > 0
                    ? dos.LifespanFactorsByRace
                    : LifespanFactorsByRace,
            SharedAging = SharedAging,
            SharedAgingGame = shared ? sharedAging : null,
        };
    }

    /// <summary>
    /// The shared aging model (<c>GameData/shared/aging.json</c>, QoL <c>shared-aging</c>); null when the data has none.
    /// Used only by tables built with <see cref="ForRules"/>'s <c>sharedAging</c> (<see cref="SharedAgingGame"/> set).
    /// </summary>
    public SharedAgingModel? SharedAging { get; init; }

    /// <summary>
    /// The game whose characters these tables make under QoL <c>shared-aging</c>: <see cref="CharacterCreation.LifespanFor"/>
    /// then asks <see cref="SharedAging"/>. Null: the game's own lifespans.
    /// </summary>
    public GameId? SharedAgingGame { get; init; }

    private static IReadOnlyList<IReadOnlyList<int>> Patch(
        IReadOnlyList<IReadOnlyList<int>> table, IReadOnlyList<(int Row, int Column, int Value)> cells)
    {
        var rows = table.Select(r => r.ToArray()).ToArray();
        foreach (var (row, column, value) in cells)
            rows[row][column] = value;
        return rows;
    }
}

/// <summary>
/// The DOS side of the creation tables (<c>dos_tables</c>): single cells the manual changed (row = race or class index,
/// column = attribute or spell slot) and DOS's lifespan factor per race (D-003, OV1 <c>0x0e84..0x0eef</c>).
/// </summary>
public sealed record DosCreationTables(
    IReadOnlyList<(int Row, int Column, int Value)> RaceAttributeModifiers,
    IReadOnlyList<(int Row, int Column, int Value)> SpellLevels,
    IReadOnlyList<(int Num, int Den)> LifespanFactorsByRace);

public sealed class CharacterCreationRequest
{
    public required string Name { get; init; }
    public Race Race { get; init; }
    public CharacterClass Class { get; init; }

    /// <summary>
    /// When true, <see cref="Race"/> is ignored and RANDOM race selection runs
    /// (with the DOS duplicate-strong-race guard against <see cref="ExistingRaces"/>).
    /// </summary>
    public bool RandomRace { get; init; }

    public IReadOnlyCollection<Race> ExistingRaces { get; init; } = Array.Empty<Race>();

    /// <summary>The original's hit-dice count for the starting HP (<c>p1.hit-dice</c> = <c>dos</c> D-007, <c>p2.hit-dice</c> = <c>st</c> D2-013).</summary>
    public bool DosHitDice { get; init; }

    /// <summary>Grow the TOSS skill at creation (<c>level_up(0)</c>; set while the Toss Rock module is on).</summary>
    public TossRockRules? TossRock { get; init; }

    /// <summary>
    /// T5 lineage (<see cref="LineageRules"/>, <c>ancestry.md</c> section 13): the five abilities (STR, INT, DEX, CON, CHA)
    /// inherited from two ancestors, used instead of the attribute roll after clamping to this game's own range
    /// (<see cref="CharacterCreation.AttributeRange"/>). Null = the game's own roll. Luck and every later draw are the
    /// game's own.
    /// </summary>
    public IReadOnlyList<int>? Abilities { get; init; }
}

/// <summary>
/// Guild "New Member" creation — OV1.OVR @ 0x0BAC formulas.
/// </summary>
public static class CharacterCreation
{
    public const int StartingShieldId = 4;
    public const int StartingWeaponId = 48; // CLUB
    public const int StartingArmorId = 22;  // ROBES
    public const int SpellCount = 55;

    private static readonly int[] SkillAttrIndex =
    [
        0, // attack → STR
        2, // parry → DEX
        1, // find → INT
        1, // spot → INT
        2, // disarm → DEX
        1, // listen → INT
        -1, // unused @42
        2, // pick → DEX
        -1, // unused @46
        3, // swim → CON
    ];

    /// <summary>Create under Phantasie I's rules (<see cref="RulesProfile.PhantasieI"/>).</summary>
    public static Character Create(
        CharacterCreationRequest request,
        CharacterCreationTables tables,
        IDosRandomSource rng) =>
        Create(request, tables, rng, RulesProfile.PhantasieI);

    /// <summary>
    /// Create a character; it starts with <see cref="RulesProfile.StartingGold"/> in the bank (Phantasie I: 256 GP).
    /// </summary>
    public static Character Create(
        CharacterCreationRequest request,
        CharacterCreationTables tables,
        IDosRandomSource rng,
        RulesProfile rules)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(rules);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));
        if (request.Name.Length > GuildHall.MaxNameLength)
            throw new ArgumentException($"Name must be at most {GuildHall.MaxNameLength} characters.", nameof(request));

        var race = request.RandomRace
            ? RollRandomRace(rng, request.ExistingRaces)
            : request.Race;

        var stats = request.Abilities is { } given
            ? GivenAttributes(race, request.Class, tables, rng, given)
            : RollAttributes(race, request.Class, tables, rng);

        var character = new Character
        {
            Name = request.Name.Trim(),
            Race = race,
            BirthRace = race,
            Class = request.Class,
            Status = CharacterStatus.Ok,
            Level = 0,
            Stats = stats,
            SkillBonuses = SkillBonusSeeds(stats),
            GoldInBank = rules.StartingGold,
            Experience = 0,
            Score = 0,
            // [C] OV1 0x0cbe: @90 := 1 (pool-visit product, Phantasie1CharacterData.Word90). II writes it too [C] (ST
            // create_character 0xca3a, after clearing the 318-B record); there only script field 55 (0x12078) could read it
            // and no II event does, so it is a harmless shared-record word, not an I-only write.
            Phantasie1 = { Word90 = 1 },
            WornShield = StartingShieldId,
            WornArmor = StartingArmorId,
            WornWeapon = StartingWeaponId,
            SpellsKnown = new int[SpellCount],
        };

        // [C] OV1 0x9ee with L = 0: MP ceiling, skills, HP max, level 1, level-1 spells flagged 2.
        LevelUp.Apply(character, 0, tables, rng, request.DosHitDice, rules.Training, request.TossRock);
        character.HpCur = character.HpMax;
        character.MagicMax = character.MagicCur = (3 * character.MpCeiling) >> 2; // OV1 0x0e66
        ApplyStartingSpell(character.SpellsKnown, request.Class);

        character.Lifespan = LifespanFor(race, stats.Luck, tables);
        character.AgeClock = StartingAgeClock(character.Lifespan, rng);

        character.InventoryItemIds.Add(StartingShieldId);
        character.InventoryItemIds.Add(StartingWeaponId);
        character.InventoryItemIds.Add(StartingArmorId);
        CombatGear.ApplyWornItemPoints(character);
        return character;
    }

    public static Race RollRandomRace(IDosRandomSource rng, IReadOnlyCollection<Race> existing)
    {
        var race = (Race)(rng.NextDosWord() / 3277 + 5); // PIXIE..MINOTAUR
        if (existing.Contains(race))
            race = (Race)(rng.NextDosWord() / 6554); // HUMAN..GNOME
        return race;
    }

    /// <summary>
    /// The lowest and highest value <see cref="RollAttributes"/> can give attribute <paramref name="attribute"/> (0 = STR …
    /// 4 = CHA) of <paramref name="race"/> / <paramref name="cls"/>: the roll's own formula at q = 0 and q = 17.
    /// </summary>
    public static (int Lo, int Hi) AttributeRange(Race race, CharacterClass cls, CharacterCreationTables tables, int attribute)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var c = tables.ClassAttributeModifiers[(int)cls][attribute];
        var r = tables.RaceAttributeModifiers[(int)race][attribute];
        static int At(int q, int c, int r) => Math.Max(3, Math.Min(18, q + c - 7) + r - 10);
        return (At(0, c, r), At(MaxAttributeQuotient, c, r));
    }

    /// <summary>The roll's largest quotient: <c>32767 / 1821</c>.</summary>
    public const int MaxAttributeQuotient = 32767 / 1821;

    /// <summary>
    /// T5 lineage: <paramref name="abilities"/> clamped to <see cref="AttributeRange"/>, then Luck by the game's own draw
    /// (the five attribute draws are not made).
    /// </summary>
    public static CharacterStats GivenAttributes(
        Race race, CharacterClass cls, CharacterCreationTables tables, IDosRandomSource rng, IReadOnlyList<int> abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(rng);
        if (abilities.Count != 5)
            throw new ArgumentException("Five abilities (STR, INT, DEX, CON, CHA).", nameof(abilities));
        var v = new int[5];
        for (var i = 0; i < 5; i++)
        {
            var (lo, hi) = AttributeRange(race, cls, tables, i);
            v[i] = Math.Clamp(abilities[i], lo, hi);
        }
        var luck = rng.NextDosWord() / 5462 + 8;
        return new CharacterStats(v[0], v[1], v[2], v[3], v[4], luck);
    }

    public static CharacterStats RollAttributes(
        Race race,
        CharacterClass cls,
        CharacterCreationTables tables,
        IDosRandomSource rng)
    {
        var classMods = tables.ClassAttributeModifiers[(int)cls];
        var raceMods = tables.RaceAttributeModifiers[(int)race];
        var values = new int[5];
        for (var i = 0; i < 5; i++)
        {
            var q = rng.NextDosWord() / 1821;
            var attr = q + classMods[i] - 7;
            if (attr > 18) attr = 18;
            attr = attr + raceMods[i] - 10;
            if (attr < 3) attr = 3;
            values[i] = attr;
        }

        var luck = rng.NextDosWord() / 5462 + 8;
        return new CharacterStats(values[0], values[1], values[2], values[3], values[4], luck);
    }

    /// <summary>
    /// Lifespan in age-clock units (2 weeks) — remaster rule, DIFFERENCES.md D-003 (user-approved):
    /// <c>round(26 · (lo + (@10 − 8)·(hi − lo)/5))</c> from the race's band in years. DOS instead
    /// uses <c>@10 × race factor</c> in <c>@264 &gt;&gt; 8</c> steps (OV1 0x0e84..0x0eef).
    /// </summary>
    public static int LifespanFor(Race race, int seed10, CharacterCreationTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        // QoL shared-aging: one model for I–IV (docs/plan/13-aging-normalised.md); the same numbers as the manual bands
        // below for luck 8–13, clamped to the band. UNDEAD has no band, as below (callers pass the living race).
        if (tables.SharedAgingGame is { } game && tables.SharedAging is { } shared)
            return race == Race.Undead ? 0 : shared.Lifespan(game, (int)race, seed10);
        // D-003 dos (p1.lifespans): @252 = @10 · factor, integer math, multiply first; kept in age-clock units.
        if (tables.LifespanFactorsByRace is { } factors)
        {
            var (num, den) = factors[(int)race];
            return seed10 * num / den * Aging.DosLifespanStep;
        }
        // A race with no band (II's data before D2-021's bands are in) gets 0 = unknown, never ages out, as an import does.
        if ((uint)race >= (uint)tables.LifespanBandsByRace.Count)
            return 0;
        var (lo, hi) = tables.LifespanBandsByRace[(int)race];
        var years = lo + (seed10 - 8) * (hi - lo) / 5.0;
        return (int)Math.Round(26 * years, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Lifespan for a character imported from a DOS save: the D-003 band for its birth race,
    /// picked by its <c>@10</c> seed (user decision s17 — DOS-made characters use the remaster
    /// bands, not their DOS <c>@252</c>). <c>@10</c> is clamped to 8..13 because the old-age decline
    /// scales it ×4/5 [~] (an aged character may land one or two band steps low). Returns 0
    /// (unknown, never ages out) for a birth race outside 0..14.
    /// </summary>
    public static int LifespanForImported(Character ch, CharacterCreationTables tables)
    {
        ArgumentNullException.ThrowIfNull(ch);
        ArgumentNullException.ThrowIfNull(tables);
        if ((int)ch.BirthRace >= tables.LifespanBandsByRace.Count)
            return 0;
        return LifespanFor(ch.BirthRace, Math.Clamp(ch.Stats.Luck, 8, 13), tables);
    }

    /// <summary>
    /// Starting age clock [C] (OV1 0x0f04..0x0f4f): 10 years + 10–12.5 % of the lifespan,
    /// <c>260 + trunc(lifespan · (0.1 + rand/1310720))</c>.
    /// </summary>
    public static int StartingAgeClock(int lifespan, IDosRandomSource rng) =>
        260 + (int)(lifespan * (0.1 + rng.NextDosWord() / 1310720.0));

    /// <summary>
    /// GUILD @50 + 2i skill bonus words, seeded at creation [C]: <c>2·linkedAttr + 30</c>,
    /// 0 for the unused slots @42/@46 (fixtures: all 22 records match).
    /// </summary>
    public static int[] SkillBonusSeeds(CharacterStats stats)
    {
        var attrs = new[] { stats.Str, stats.Int, stats.Dex, stats.Con };
        var bonuses = new int[CharacterSkills.RecordSlotCount];
        for (var i = 0; i < bonuses.Length; i++)
            bonuses[i] = SkillAttrIndex[i] < 0 ? 0 : 2 * attrs[SkillAttrIndex[i]] + 30;
        return bonuses;
    }

    public static CharacterSkills ComputeSkills(
        CharacterClass cls,
        CharacterStats stats,
        CharacterCreationTables tables)
    {
        var coeffs = tables.SkillCoefficientsByClassLevel[(int)cls][0];
        var attrs = new[] { stats.Str, stats.Int, stats.Dex, stats.Con };
        var values = new int[10];
        for (var i = 0; i < 10; i++)
        {
            var attrIdx = SkillAttrIndex[i];
            var seed = attrIdx < 0 ? 0 : 2 * attrs[attrIdx] + 30;
            var skill = (coeffs[i] >> 1) + seed - 49;
            values[i] = Math.Clamp(skill, 0, 255);
        }

        return new CharacterSkills(
            values[0], values[1], values[2], values[3],
            values[4], values[5], values[7], values[9]);
    }

    /// <summary>Starting spell, known (1) — set after level-up has flagged the level-1 spells 2 [C] (fixtures).</summary>
    public static void ApplyStartingSpell(int[] spells, CharacterClass cls)
    {
        switch (cls)
        {
            case CharacterClass.Monk:
            case CharacterClass.Wizard:
                spells[4] = 1; // FIREFLASH I
                break;
            case CharacterClass.Ranger:
            case CharacterClass.Priest:
                spells[0] = 1; // HEALING I
                break;
        }
    }

    /// <summary>[D✓] s65: DOS compares names exactly — "PRIESTO" and "priesto" can both exist.</summary>
    public static bool IsNameUnique(string name, IEnumerable<Character> roster) =>
        roster.All(c => !string.Equals(c.Name, name, StringComparison.Ordinal));
}
