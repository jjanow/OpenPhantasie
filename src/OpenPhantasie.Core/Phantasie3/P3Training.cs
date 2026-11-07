namespace OpenPhantasie.Core;

/// <summary>III's nine skills in record order (<c>+0x30 + 4k</c>).</summary>
public enum P3Skill
{
    Attack = 0,
    Parry = 1,
    FindItem = 2,
    FindTrap = 3,
    Disarm = 4,
    Listen = 5,
    FireBow = 6,
    PickLock = 7,
    Swim = 8,
}

/// <summary>
/// III's training tables (game data, <c>training.md</c>): handed in by the caller so the rules don't depend on the data files.
/// </summary>
/// <param name="ExperienceFactors">G (<c>DS:0xb88</c>), index L − 1 for level L (180, 324, 583, …).</param>
/// <param name="TrainingCosts">T (<c>DS:0xb6c</c>), index L − 1 (1, 5, 11, 21, …; levels 16+ run into G in DOS).</param>
/// <param name="HitDice">HP dice per class, index level − 1 (<c>DS:0xa78 + 22·class</c>; twice each is the manual's CON-10 chart).</param>
/// <param name="SkillTargets">Class tables <c>C_k[class][L]</c> per skill (Fire Bow has none), index L − 1, levels 1–15.</param>
public sealed record P3TrainingTables(
    IReadOnlyList<long> ExperienceFactors,
    IReadOnlyList<int> TrainingCosts,
    IReadOnlyDictionary<CharacterClass, IReadOnlyList<int>> HitDice,
    IReadOnlyDictionary<P3Skill, IReadOnlyDictionary<CharacterClass, IReadOnlyList<int>>> SkillTargets);

/// <summary>A member at the Guild's Train Member (<c>training.md</c> §1–§4).</summary>
public sealed record P3Trainee(
    CharacterClass Class, CharacterStats Stats, int Level, long Experience, int Score, int SocialClass,
    int HpMax, P3Status Status, bool IsUndead);

/// <summary>What a level-up did: new level and HP, the HP gained, the gold paid into the bank (D3-023).</summary>
public sealed record P3LevelUp(int Level, int HpMax, int HpGain, long LevelGold);

/// <summary>
/// Phantasie III's Guild training (<c>training.md</c>): who may train, XP needed, the gold cost, the level-up (HP with or
/// without Constitution, D3-022; level gold, D3-023) and the three skill picks.
/// </summary>
public static class P3Training
{
    /// <summary>
    /// The skills in the order DOS's pick screen lists them (ovl2 <c>0x18b0…0x19b7</c>: row i shows name table
    /// <c>DS:0x1438</c>[i], "Attack", "Parry", "Swimming", "Listen", "Find Trap", "Disarm Trap", "Find Item", "Pick Lock",
    /// "Fire Bow"; the menu answer i picks row i) [C] s97 static. Menu digit n = <c>ScreenOrder[n − 1]</c>.
    /// </summary>
    public static IReadOnlyList<P3Skill> ScreenOrder { get; } =
    [
        P3Skill.Attack, P3Skill.Parry, P3Skill.Swim, P3Skill.Listen, P3Skill.FindTrap, P3Skill.Disarm, P3Skill.FindItem,
        P3Skill.PickLock, P3Skill.FireBow,
    ];

    /// <summary>Training stops here (level is clamped to it; a member at it is refused).</summary>
    public const int MaxLevel = 21;

    /// <summary>The highest level the skill tables have.</summary>
    public const int SkillTableLevels = 15;

    public const int SkillCap = 250;

    /// <summary>D3-023 manual: each level gained pays social class × this (the starting-gold rule, user s81).</summary>
    public const int LevelGoldPerSocialClass = 256;

    /// <summary>
    /// The Guild refuses (string 417) a member who is Dead or Out, Undead, or at level 21 or more. Learn Spells uses the
    /// same test without the level.
    /// </summary>
    public static bool MayTrain(P3Trainee t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return t.Status is not (P3Status.Dead or P3Status.Out) && !t.IsUndead && t.Level < MaxLevel;
    }

    /// <summary>
    /// The class score in integer maths: Fighter <c>(2·STR + DEX)·100/33</c>, Thief <c>(2·DEX + STR)·100/20</c>, Priest
    /// <c>(2·INT + STR)·100/36</c>, Ranger <c>(STR + INT + CHA)·100/53</c>, Monk <c>(STR + INT + DEX)·100/50</c>, Wizard
    /// <c>(2·INT + LCK)·100/43</c>; at least 17.
    /// </summary>
    public static int ClassScore(CharacterClass cls, CharacterStats s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var v = cls switch
        {
            CharacterClass.Fighter => (2 * s.Str + s.Dex) * 100 / 33,
            CharacterClass.Thief => (2 * s.Dex + s.Str) * 100 / 20,
            CharacterClass.Priest => (2 * s.Int + s.Str) * 100 / 36,
            CharacterClass.Ranger => (s.Str + s.Int + s.Cha) * 100 / 53,
            CharacterClass.Monk => (s.Str + s.Int + s.Dex) * 100 / 50,
            CharacterClass.Wizard => (2 * s.Int + s.Luck) * 100 / 43,
            _ => 1,
        };
        return Math.Max(v, 17);
    }

    /// <summary>
    /// XP needed for the next level (ovl2 <c>0x1031</c>): <c>(1000·G[L] − 80000) / s</c>, ÷ 6 at score ≥ 250, ÷ 2 at ≥ 200.
    /// Level 0 (a transfer under the manual, D3-016) reads level 1's cell (<see cref="TableLevel"/>).
    /// </summary>
    public static long ExperienceNeeded(P3Trainee t, P3TrainingTables tables)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(tables);
        var need = (1000 * tables.ExperienceFactors[TableLevel(t.Level) - 1] - 80000) / ClassScore(t.Class, t.Stats);
        if (t.Score >= 250) need /= 6;
        else if (t.Score >= 200) need /= 2;
        return need;
    }

    /// <summary>
    /// The table row a level reads. III's tables start at level 1 (DOS's level-0 cells are the neighbouring data:
    /// G = 25,362,764). The manual's transfer starts characters at level 0 (D3-016, user s81: "the missing table values are
    /// a manual-silent detail filled in when built"): level 0 reads level 1's cells, so the first training costs and needs
    /// what level 1's does.
    /// </summary>
    public static int TableLevel(int level) => Math.Max(level, 1);

    /// <summary>XP still short, 0 = may train.</summary>
    public static long Shortfall(P3Trainee t, P3TrainingTables tables) =>
        Math.Max(0, ExperienceNeeded(t, tables) - t.Experience);

    /// <summary>
    /// The price: <c>T[L] · price index · (20 − CHA)² / 100</c>, between 5 and 15,000; <c>20 − CHA</c> is not clamped. The
    /// town's price index is data (<c>DS:0x10fc</c>; Pendragon 150).
    /// </summary>
    public static long Cost(int level, int charisma, int priceIndex, P3TrainingTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var k = 20L - charisma;
        var cost = tables.TrainingCosts[TableLevel(level) - 1] * priceIndex * k * k / 100;
        return Math.Max(5, Math.Min(cost, 15000));
    }

    /// <summary>
    /// The number of HP dice: the table's value, scaled by Constitution with Phantasie I's rule <c>dice·(CON + 5)/15</c>
    /// (manual, D3-022; CON 10 gives the table's value) or the table as it is (<c>p3.hit-points</c> = <c>dos</c>).
    /// </summary>
    public static int HitDiceCount(int tableDice, int constitution, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.P3HpIgnoresCon() ? tableDice : tableDice * (constitution + 5) / 15;
    }

    /// <summary>Creation HP (ovl2 <c>0x227d</c>): one <c>rnd(2) + 1</c> (2–3) per level-1 die.</summary>
    public static int CreationHp(CharacterClass cls, int constitution, P3TrainingTables tables, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var dice = HitDiceCount(tables.HitDice[cls][0], constitution, rules);
        var hp = 0;
        for (var i = 0; i < dice; i++)
            hp += P3Dice.Rnd(rng, 2) + 1;
        return hp;
    }

    /// <summary>
    /// The level-up after "Do you wish to train, now?" Yes (the caller takes the cost from the party gold): level + 1
    /// (at most 21); HP re-rolled as one <c>rnd(3)</c> per die of the new level, a roll not above the old maximum gives
    /// max + 1; HP max and current = the roll. Level gold (D3-023): social class · 256 per level (manual) or none
    /// (<c>p3.level-gold</c> = <c>dos</c>); it goes into the member's bank account, as the starting gold (the caller applies
    /// the account cap). The three skill picks follow (<see cref="SkillAfter"/>).
    /// </summary>
    public static P3LevelUp LevelUp(P3Trainee t, P3TrainingTables tables, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rules);
        var level = Math.Min(t.Level + 1, MaxLevel);
        var dice = HitDiceCount(tables.HitDice[t.Class][level - 1], t.Stats.Con, rules);
        var roll = 0;
        for (var i = 0; i < dice; i++)
            roll += P3Dice.Rnd(rng, 3);
        if (roll <= t.HpMax) roll = t.HpMax + 1;
        var gold = rules.P3NoLevelGold() ? 0 : (long)t.SocialClass * LevelGoldPerSocialClass;
        return new P3LevelUp(level, roll, roll - t.HpMax, gold);
    }

    /// <summary>
    /// The skill seeds (root <c>0xa1f2</c>): <c>2·attr + 30</c> with STR for Attack, DEX for Parry, Disarm and Pick Lock, INT
    /// for Find Item, Find Trap and Listen, CON for Swim; Fire Bow <see cref="P3Combat.FireBowSeed"/>.
    /// </summary>
    public static int Seed(P3Skill skill, int level, CharacterStats s, CharacterClass cls, Race race)
    {
        ArgumentNullException.ThrowIfNull(s);
        return skill switch
        {
            P3Skill.Attack => 2 * s.Str + 30,
            P3Skill.Parry or P3Skill.Disarm or P3Skill.PickLock => 2 * s.Dex + 30,
            P3Skill.FindItem or P3Skill.FindTrap or P3Skill.Listen => 2 * s.Int + 30,
            P3Skill.Swim => 2 * s.Con + 30,
            P3Skill.FireBow => P3Combat.FireBowSeed(level, s, cls, race),
            _ => throw new ArgumentOutOfRangeException(nameof(skill)),
        };
    }

    /// <summary>
    /// One skill pick (ovl2 <c>0x16d0</c>): <c>after = ((C[class][min(level, 15)] + now) &gt;&gt; 1) + seed − 49</c>, Fire Bow
    /// <c>(now &gt;&gt; 1) + seed − 49</c>, clamped to 1…250. No "never decrease" test: a pick can lower the skill.
    /// <paramref name="level"/> is the new level (seeds are re-read from the current stats and level).
    /// </summary>
    public static int SkillAfter(
        P3Skill skill, int now, int level, CharacterStats s, CharacterClass cls, Race race, P3TrainingTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var seed = Seed(skill, level, s, cls, race);
        var c = skill == P3Skill.FireBow ? 0 : tables.SkillTargets[skill][cls][Math.Min(level, SkillTableLevels) - 1];
        return Math.Clamp(((c + now) >> 1) + seed - 49, 1, SkillCap);
    }

    /// <summary>A skill at creation (root <c>0x9ea0</c>): <see cref="SkillAfter"/> with <c>now = 0</c> at level 1, at least 1.</summary>
    public static int SkillAtCreation(P3Skill skill, CharacterStats s, CharacterClass cls, Race race, P3TrainingTables tables) =>
        SkillAfter(skill, 0, 1, s, cls, race, tables);

    /// <summary>Starting gold (ovl2 <c>0x227d</c>): social class · 256.</summary>
    public static long StartingGold(int socialClass) => (long)socialClass * LevelGoldPerSocialClass;
}
