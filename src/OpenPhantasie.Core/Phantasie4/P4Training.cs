namespace OpenPhantasie.Core;

/// <summary>Why the training hall turns a character away (<c>_trang</c>, <c>training.md</c> §1).</summary>
public enum P4TrainingGate { Allowed, PoorCondition, NothingMoreToTeach }

/// <summary>
/// Phantasie IV level-up: guild training, level-up gold, HP / magic level / skills / learnable spells, magic training
/// (<c>reverse-engineering/x68000/phantasie4/logic-specs/training.md</c>, <c>skills.md</c>, <c>FUNCTIONS.md</c>
/// <c>_calpow</c>). D4-015 (Priest levels), D4-016 (Fighter spells), D4-019 (Thief HP) and D4-031 (gold cap) are
/// <c>p4.*</c> options. Abilities are Str, Int, Dex, Con, Cha, Luck.
/// </summary>
public static class P4Training
{
    /// <summary>
    /// Experience needed for the next level: trunc(1350 (1.8^L − 0.8) / r), r = the class's ability sum / divisor (0 →
    /// 0.00001; Sage and Lord have no case), and − 3333 at level 0 (§2). Computed in double: the code's float → long
    /// overflow from about level 24–26 is <c>[?]</c> (§4) and not reproduced.
    /// </summary>
    public static long ExperienceNeeded(P4TrainingTables t, int cls, IReadOnlyList<int> abilities, int level)
    {
        var r = 0.0;
        if (cls is >= 0 and < P4Ids.PlayerClasses)
        {
            var sum = 0;
            for (var i = 0; i < 6; i++)
                sum += t.XpWeights[cls][i] * abilities[i];
            r = sum / t.XpDivisors[cls];
        }
        if (r == 0)
            r = 0.00001;
        var need = (long)Math.Truncate(1350 * (Math.Pow(1.8, level) - 0.8) / r);
        return level == 0 ? need - 3333 : need;
    }

    /// <summary>Training cost from party gold: min(25,000, trunc(L^2.2 (20 − Cha)² T / 100)) (§3).</summary>
    public static int TrainingCost(P4TrainingTables t, int level, int charisma, int priceIndex)
    {
        var c = Math.Truncate(Math.Pow(level, 2.2) * (20 - charisma) * (20 - charisma) * priceIndex / 100.0);
        return (int)Math.Min(t.CostCap, c);
    }

    /// <summary>The highest level a class reaches: 30, or 35 for Priests under the manual (D4-015, user s82).</summary>
    public static int MaxLevel(GameOptions o, P4TrainingTables t, int cls) =>
        cls == P4Ids.Priest && !o.P4Code(P4RuleOptions.PriestLevels) ? t.PriestManualMaxLevel : t.MaxLevel;

    /// <summary>
    /// The hall's gate (§1): condition must be Okay; "nothing more I can teach" when Score ≥ 125 or level &gt; the class's max
    /// level (the code: level ≥ 31). A character at exactly the max level passes the gate, pays and gains nothing, as the code
    /// (<c>_calpow</c> returns at once) — <see cref="CanLevelUp"/>.
    /// </summary>
    public static P4TrainingGate Gate(GameOptions o, P4TrainingTables t, int cls, int level, int score, bool okay) =>
        !okay ? P4TrainingGate.PoorCondition
        : score >= t.ScoreGate || level > MaxLevel(o, t, cls) ? P4TrainingGate.NothingMoreToTeach
        : P4TrainingGate.Allowed;

    /// <summary><c>_calpow</c> raises the level only below the class's max level.</summary>
    public static bool CanLevelUp(GameOptions o, P4TrainingTables t, int cls, int level) => level < MaxLevel(o, t, cls);

    /// <summary>
    /// Level-up gold into the account (§4): social^social × L × 30, L = the level before the level-up (0 at creation: none).
    /// Cap: 65,000 under the manual (D4-031, every path), none under <c>uncapped</c>. Outcast's 8^8 overflow of the code is
    /// not reproduced (computed in long).
    /// </summary>
    public static long LevelUpGold(GameOptions o, P4TrainingTables t, P4TownTables towns, int social, int level, long account)
    {
        var bonus = social == 0 ? 0L : (long)Math.Pow(social, social);
        var total = account + bonus * level * t.GoldPerLevel;
        return o.P4GoldUncapped() ? total : Math.Min(towns.GoldCapManual, total);
    }

    /// <summary>
    /// The HP growth row of a class for levels 1–15 (TABL 0x000). D4-019 (manual, user s82): a Thief's value is the lowest of
    /// every other player class at that level − 1, floor 1.
    /// </summary>
    public static IReadOnlyList<int> HpGrowthRow(GameOptions o, P4TrainingTables t, int cls)
    {
        if (cls != P4Ids.Thief || o.P4Code(P4RuleOptions.ThiefHp))
            return t.HpGrowth[cls];
        return Enumerable.Range(0, t.HpGrowth[cls].Count)
            .Select(l => Math.Max(1, Enumerable.Range(1, P4Ids.PlayerClasses - 1).Min(c => t.HpGrowth[c][l]) - 1))
            .ToList();
    }

    /// <summary>
    /// New max HP at a level-up to the level after <paramref name="levelBefore"/> (<c>_calpow</c>): n = (value × (Con + 5))
    /// &gt;&gt; 4 with value = the row at min(L, 14); the sum of n rolls of rand(3) <c>[~]</c> (FUNCTIONS.md "Σ of n × rand(3)");
    /// never below old max + 1; cap 255.
    /// </summary>
    public static int NewMaxHp(GameOptions o, P4TrainingTables t, IRandomSource rng, int cls, int levelBefore, int con, int oldMax)
    {
        var value = HpGrowthRow(o, t, cls)[Math.Min(levelBefore, 14)];
        var n = (value * (con + 5)) >> 4;
        var hp = 0;
        for (var i = 0; i < n; i++)
            hp += P4Dice.Rand(rng, 3);
        return Math.Min(255, Math.Max(hp, oldMax + 1));
    }

    /// <summary>Magic level (record +36) for a level (TABL 0x14A at min(L, 14)); max MP at creation = trunc(0.75 × it).</summary>
    public static int MagicLevel(P4TrainingTables t, int cls, int levelBefore) => t.MagicLevel[cls][Math.Min(levelBefore, 14)];

    /// <summary>The ability bonus of a skill (<c>_calpow</c>, <c>skills.md</c> §1). B(a) = 0 below 10, else 2a − 20.</summary>
    public static int SkillBonus(P4Skill skill, IReadOnlyList<int> abilities, int race)
    {
        static int B(int a) => a < 10 ? 0 : 2 * a - 20;
        return skill switch
        {
            P4Skill.Attack => B(abilities[0]),
            P4Skill.Parry or P4Skill.Disarm or P4Skill.PickLock => B(abilities[2]),
            P4Skill.SpotItem or P4Skill.SpotTrap or P4Skill.Listen => B(abilities[1]),
            P4Skill.Swim => B(abilities[3]),
            P4Skill.FireBow => Math.Max(0, abilities[0] + 4 * abilities[2] + (race == P4Ids.Elf ? 15 : 0) - 50),
            P4Skill.Throwing => Math.Max(0, 6 * abilities[2] - 84),
            _ => 0,
        };
    }

    /// <summary>
    /// <c>_calsk</c> (<c>skills.md</c> §2): round((old + G[class][min(L, 14)]) / 2 + bonus), at least 1. L is the level passed
    /// in (0 at creation, the level before a level-up). No working 255 cap in the code; a byte would wrap <c>[~]</c>.
    /// </summary>
    public static int SkillValue(P4TrainingTables t, int cls, P4Skill skill, int level, int old, int bonus)
    {
        var g = t.SkillGrowth[(int)skill][cls][Math.Min(level, 14)];
        var v = Math.Max((old + g) / 2.0 + bonus, 1.0);
        return (int)Math.Truncate(v + 0.5);
    }

    /// <summary>Picks per level-up (<c>_sklup</c>: three, the same skill may be picked again).</summary>
    public const int SkillPicksPerLevel = 3;

    /// <summary>
    /// The spells (numbers) that become learnable when a class reaches the level after <paramref name="levelBefore"/>
    /// (TABL 0x6EA row min(L, 14); <c>_calpow</c>). D4-016 (manual): a Fighter gets only the first 4 spells of his schedule.
    /// D4-015 (manual): a Priest reaching 31–35 gets 6 of the spells missing from his schedule each level, in number order.
    /// </summary>
    public static IReadOnlyList<int> SpellsGranted(GameOptions o, P4TrainingTables t, int cls, int levelBefore,
        P4ServiceTables? s = null)
    {
        if (cls == P4Ids.Priest && !o.P4Code(P4RuleOptions.PriestLevels) && levelBefore + 1 >= t.PriestManualFirstExtraLevel)
        {
            var have = t.SpellsGranted[cls].SelectMany(l => l).ToHashSet();
            var missing = Enumerable.Range(1, t.SpellCount).Where(s => !have.Contains(s)).ToList();
            var k = levelBefore + 1 - t.PriestManualFirstExtraLevel;
            return missing.Skip(k * t.PriestManualSpellsPerLevel).Take(t.PriestManualSpellsPerLevel).ToList();
        }
        var row = SpellRow(t, s, cls, levelBefore);
        if (row is null)
            return [];
        if (cls != P4Ids.Fighter || o.P4Code(P4RuleOptions.FighterSpells))
            return row;
        var allowed = t.SpellsGranted[cls].SelectMany(l => l).Take(t.FighterSpellLimitManual).ToHashSet();
        return row.Where(allowed.Contains).ToList();
    }

    /// <summary>
    /// The TABL 0x6EA row <c>_calpow</c> reads for a level-up from <paramref name="levelBefore"/> (training.md §4.1): x = L + 1
    /// (none below 2, but <paramref name="levelBefore"/> 0 gives row 0: creation's starting spells); above 15 x = x mod 15,
    /// so the schedule starts again; row x − 1, and row −1 (L = 29) is the previous class's level-15 row — for Thieves the
    /// three TABL bytes before the table (only 1–65 are spells).
    /// </summary>
    public static IReadOnlyList<int>? SpellRow(P4TrainingTables t, P4ServiceTables? s, int cls, int levelBefore)
    {
        var x = levelBefore + 1;
        if (x < 2)
            return levelBefore == 0 ? t.SpellsGranted[cls][0] : null;
        var period = s?.LevelRowPeriod ?? t.SpellsGranted[cls].Count;
        if (x > period)
            x %= period;
        if (x >= 1)
            return t.SpellsGranted[cls][x - 1];
        if (cls > 0)
            return t.SpellsGranted[cls - 1][period - 1];
        return s?.ThiefRowBeforeTable.Where(n => n is >= 1 and <= P4Member.SpellCount).ToList() ?? [];
    }

    /// <summary>
    /// The level <c>_calpow</c> writes after a level-up from <paramref name="levelBefore"/> (D4-060): L + 1 (<c>msx2</c>, as
    /// MSX2 adds 1 to the byte) or the X68000's wrapped row index ((L + 1) mod 15 above 15, so level 15 trains to level 1).
    /// </summary>
    public static int LevelAfter(GameOptions o, P4ServiceTables s, int levelBefore)
    {
        var x = levelBefore + 1;
        return o.P4X68000LevelWrap() && x > s.LevelRowPeriod ? x % s.LevelRowPeriod : x;
    }

    /// <summary>Magic level +36 after a level-up from L: TABL 0x14A[class][min(L, 14)] + (L − 14 from L = 15), cap 255.</summary>
    public static int MagicLevelAfter(P4TrainingTables t, int cls, int levelBefore) =>
        Math.Min(255, MagicLevel(t, cls, levelBefore) + (levelBefore >= 15 ? levelBefore - 14 : 0));

    /// <summary>
    /// Magic hall quota (<c>_mage</c>, §5): L × Int^1.177 / 90 × (class term); refused when the known count exceeds it or
    /// nothing is open. Whether known = quota still allows one more is read from a float compare (<c>bgt</c>) <c>[~]</c>.
    /// </summary>
    public static bool MayLearnMore(P4TrainingTables t, int cls, int level, int intelligence, int known, int open) =>
        open > 0 && known <= level * Math.Pow(intelligence, 1.177) / 90.0 * t.MagicQuotaClassTerm[cls];

    /// <summary>Price per spell: trunc(9000 × T / 100) / Cha / Int × L / (9 − class), each division truncating (§5).</summary>
    public static int SpellPrice(P4TrainingTables t, int cls, int level, int intelligence, int charisma, int priceIndex)
    {
        var p = t.MagicPriceBase * priceIndex / 100;
        p = charisma == 0 ? 0 : p / charisma;
        p = intelligence == 0 ? 0 : p / intelligence;
        return p * level / (9 - cls);
    }

    /// <summary>A lesson succeeds if rand(100) &lt; 5 × Int (§5 item 3); either way it costs the price and 2/26 year.</summary>
    public static bool LessonSucceeds(IRandomSource rng, int intelligence) => P4Dice.Rand(rng, 100) < 5 * intelligence;
}
