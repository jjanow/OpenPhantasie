namespace OpenPhantasie.Core;

/// <summary>
/// Level-up gains — OV1 <c>0x9ee</c> [C] (town-menus.md §Level-up gains). DOS runs the same
/// routine to build a new character (<c>L = 0</c>) and to train one (<c>L</c> = current level).
/// Table rows are indexed by <c>L</c> (the level being left); only the spell check uses <c>L + 1</c>.
/// </summary>
public static class LevelUp
{
    public const int MaxLevel = 15;

    /// <summary>Apply the gains for leaving level <paramref name="fromLevel"/> (0 at creation).</summary>
    /// <remarks>
    /// <paramref name="dosHitDice"/> = the original's dice count (<c>p1.hit-dice</c> D-007, <c>p2.hit-dice</c> D2-013).
    /// <paramref name="rules"/> = the game's level-up deltas (<see cref="TrainingRules.SpellMarks"/>,
    /// <see cref="TrainingRules.HpTie"/>); null = Phantasie I's. <paramref name="toss"/> = grow the TOSS skill first
    /// (<see cref="TossRockRules.Grow"/>; set while the Toss Rock module is on).
    /// </remarks>
    public static void Apply(
        Character m, int fromLevel, CharacterCreationTables tables, IDosRandomSource rng, bool dosHitDice = false,
        TrainingRules? rules = null, TossRockRules? toss = null)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);
        if (fromLevel is < 0 or >= MaxLevel)
            return;

        var cls = (int)m.Class;

        // MP max (@16) is not raised here — only the Inn raises it toward the ceiling.
        m.MpCeiling = tables.MpCeilingByClass[cls][fromLevel];

        // II level_up 0x0c71a: the TOSS bonus @62 is recomputed before the skill loop, then TOSS grows like a skill.
        toss?.Grow(m, fromLevel, tables);

        var coeffs = tables.SkillCoefficientsByClassLevel[cls][fromLevel];
        var skills = m.Skills.ToRecordSlots();
        for (var i = 0; i < skills.Length; i++)
        {
            var bonus = i < m.SkillBonuses.Length ? m.SkillBonuses[i] : 0;
            var t = ((coeffs[i] + skills[i]) >> 1) + bonus - 49;
            skills[i] = Math.Max(skills[i], Math.Min(t, 255)); // never decreases
        }
        m.Skills = CharacterSkills.FromRecordSlots(skills);

        // HP max is re-rolled from scratch; a roll that isn't higher still gives +1. HP current unchanged.
        if (m.HpMax < 255)
        {
            var roll = RollHitDice(tables.HpFactorsByClass[cls][fromLevel], m.Stats.Con, rng, dosHitDice);
            var takesRoll = rules?.HpTie == HpRollTie.NewMaximum ? roll >= m.HpMax : roll > m.HpMax;
            m.HpMax = takesRoll ? Math.Min(roll, 255) : m.HpMax + 1;
        }

        m.Level = fromLevel + 1;

        var spellLevels = tables.SpellLevelsByClass[cls];
        for (var s = 0; s < spellLevels.Count && s < m.SpellsKnown.Length; s++)
        {
            var marks = rules?.SpellMarks == SpellMarkRule.UpToNewLevel
                ? spellLevels[s] > 0 && spellLevels[s] <= fromLevel + 1 // II: every unknown spell up to the new level
                : spellLevels[s] == fromLevel + 1;
            if (marks && m.SpellsKnown[s] == 0)
                m.SpellsKnown[s] = 2; // "can learn"
        }
    }

    /// <summary>
    /// Number of d3 hit dice. DOS: <c>(factor · (CON + 5)) &gt;&gt; 4</c>. The remaster divides by 15
    /// so the average matches the manual's HP chart (DIFFERENCES.md D-007, user decision).
    /// </summary>
    public static int HitDiceCount(int hpFactor, int constitution, bool dos = false) =>
        dos ? (hpFactor * (constitution + 5)) >> 4 : hpFactor * (constitution + 5) / 15;

    /// <summary>Sum of <see cref="HitDiceCount"/> d3 rolls, each <c>rand/10923 + 1</c> [C].</summary>
    public static int RollHitDice(int hpFactor, int constitution, IDosRandomSource rng, bool dos = false)
    {
        var dice = HitDiceCount(hpFactor, constitution, dos);
        var hp = 0;
        for (var i = 0; i < dice; i++)
            hp += rng.NextDosWord() / 10923 + 1;
        return hp;
    }
}
