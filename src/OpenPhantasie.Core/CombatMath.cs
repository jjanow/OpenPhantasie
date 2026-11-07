namespace OpenPhantasie.Core;

/// <summary>
/// Pure combat formulas from <c>combat-resolution.md</c> [C].
/// Methods take explicit rolls so unit tests can pin values without an RNG.
/// </summary>
public static class CombatMath
{
    /// <summary>
    /// Newton integer square root matching <c>FUN_0000_3675</c> / <c>dfa9</c>.
    /// Quirk: <c>IntegerSqrt(1) == 0</c> (not <c>floor(sqrt(1))</c>).
    /// </summary>
    public static int IntegerSqrt(int n)
    {
        if (n < 0) n = 0;
        var result = 0;
        var guess = n / 2;
        if (guess == 0)
            return 0;

        do
        {
            result = (n / guess + guess) / 2;
            var delta = guess - result;
            if (guess < result)
                delta = -delta;
            guess = result;
            if (delta <= 1)
                break;
        } while (true);

        return result;
    }

    /// <summary>
    /// Combat prep defense rating, stored at record @12 (OV2 <c>0x3298..0x32f7</c>) [C]:
    /// <c>(DEX·20 − armor·50 + parry·10/4) / 10</c>, floor 0, plus shield/3, cap 255.
    /// Reproduces @12 of all six pass-3 fixture characters.
    /// </summary>
    public static int ComputeDefenseRating(int dexterity, int armorSoakBase, int parrySkill, int shieldSoak)
    {
        var def = (dexterity * 20 + armorSoakBase * -50 + (parrySkill * 10) / 4) / 10;
        if (def < 0)
            def = 0;
        def += shieldSoak / 3;
        if (def > 255)
            def = 255;
        return def;
    }

    /// <summary>Spell / breath CV to-hit chance: <c>(atk_CV - def_CV) * 5 + 50</c>. Unclamped.</summary>
    public static int CvHitChance(int attackerCv, int defenderCv) =>
        (attackerCv - defenderCv) * 5 + 50;

    /// <summary>Hit if <paramref name="roll0To99"/> &lt; chance (melee / <c>FUN_0000_1a7b</c> style).</summary>
    public static bool HitsStrict(int roll0To99, int chance) => roll0To99 < chance;

    /// <summary>Hit if <paramref name="roll0To99"/> &lt;= chance (some spell paths).</summary>
    public static bool HitsInclusive(int roll0To99, int chance) => roll0To99 <= chance;

    /// <summary>Party → monster melee chance: <c>weapon_hit / swings - monster_defense</c>.</summary>
    public static int PartyMeleeHitChance(int weaponHitEff, int swings, int monsterDefense)
    {
        if (swings < 1) swings = 1;
        return weaponHitEff / swings - monsterDefense;
    }

    /// <summary>
    /// Monster → party melee metric: <c>(atk / swings - party_def) * 10</c>.
    /// Hit if <c>roll * 6 &lt; metric</c>.
    /// </summary>
    public static int MonsterMeleeMetric(int monsterAttackPower, int swings, int partyDefense)
    {
        if (swings < 1) swings = 1;
        return (monsterAttackPower / swings - partyDefense) * 10;
    }

    public static bool MonsterMeleeHits(int roll0To99, int metric) => roll0To99 * 6 < metric;

    /// <summary>
    /// Monster → party damage before floor (≥1 applied by <see cref="ApplyDamageFloor"/>).
    /// </summary>
    public static int MonsterToPartyDamage(
        int roll0To99,
        int monsterDamageField,
        int partyArmorEff,
        bool isParrying,
        int shieldSoak,
        out bool parryConsumed)
    {
        parryConsumed = false;
        var coef = IntegerSqrt(roll0To99);
        var damage = (coef * monsterDamageField) / 15 - partyArmorEff;
        if (isParrying && damage > 0)
        {
            parryConsumed = true;
            damage -= shieldSoak;
        }
        return damage;
    }

    /// <summary>
    /// Party → monster damage before floor.
    /// Action penalty <c>(action*100)/67</c> makes THRUST hit harder than SLASH per swing.
    /// </summary>
    public static int PartyToMonsterDamage(
        int roll0To99,
        int weaponDamageEff,
        int monsterArmor,
        int packedActionCode)
    {
        var coef = IntegerSqrt(roll0To99);
        var product = (long)weaponDamageEff * coef;
        product -= (long)monsterArmor * 10;
        product += 35; // 0x23
        product -= (packedActionCode * 100L) / 67;
        return (int)(product / 10);
    }

    /// <summary>Spell AoE sample path (<c>FUN_0000_1a7b</c>).</summary>
    public static int SpellScaledDamage(int roll0To99, int param, int param2, int armorEff)
    {
        var scaled = (roll0To99 * param) / 100;
        return param2 == 2
            ? scaled / 10 - armorEff
            : scaled / 10;
    }

    /// <summary><c>FUN_0000_124a</c> floors applied damage to ≥1.</summary>
    public static int ApplyDamageFloor(int damage) => damage < 1 ? 1 : damage;

    /// <summary>Monster swing count from attack power: <c>max(1, attack_power / 0x52)</c>.</summary>
    public static int MonsterSwingCount(int attackPower)
    {
        var swings = attackPower / 0x52;
        return swings < 1 ? 1 : swings;
    }

    /// <summary>
    /// Monster HP rolled at encounter build (OV2 <c>0x3a27</c>):
    /// <c>((rand0_99 + 50) · hp_base + 50) / 100</c> — 50 %…149 % of the record's base.
    /// </summary>
    public static int MonsterHitPoints(int roll0To99, int hpBase) =>
        (int)(((long)(roll0To99 + 50) * hpBase + 50) / 100);

    /// <summary>Pass numbers of one melee round (OV2 <c>0x00e4</c>: starts at 1, <c>+= 2</c> while ≤ 5).</summary>
    public static ReadOnlySpan<int> RoundPasses => new[] { 1, 3, 5 };

    /// <summary>Party readiness in pass <paramref name="pass"/> (OV2 <c>0x1b9</c>): <c>roll &lt; 3·level + 5·p²</c>.</summary>
    public static bool PartyActsInPass(int roll0To99, int level, int pass) =>
        roll0To99 < 3 * level + 5 * pass * pass;

    /// <summary>Monster readiness (OV2 <c>0xcf1</c>): <c>7·roll ≤ 50·p² + 30·L</c>, L = encounter level <c>[0x7508]</c>.</summary>
    public static bool MonsterActsInPass(int roll0To99, int pass, int monsterLevel) =>
        7 * roll0To99 <= 50 * pass * pass + 30 * monsterLevel;

    /// <summary>
    /// Monster melee target (OV2 <c>0xdd4</c>): slot <c>((roll/10 + 10·k)·partySize / n) / 10</c> for the k-th of n
    /// monsters in its group, then step forward (wrapping) past DEAD members. Sleepers can be hit. −1 if all dead.
    /// </summary>
    public static int MonsterTargetSlot(int roll0To99, int slot, int groupCount, IReadOnlyList<Character> party)
    {
        var size = party.Count;
        if (size == 0 || party.All(c => c.Status == CharacterStatus.Dead))
            return -1;
        var t = (roll0To99 / 10 + 10 * slot) * size / Math.Max(1, groupCount) / 10;
        while (t >= size || party[t].Status == CharacterStatus.Dead)
            t = (t + 1) % size;
        return t;
    }
}
