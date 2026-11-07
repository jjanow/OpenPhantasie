namespace OpenPhantasie.Core;

/// <summary>
/// Dungeon fight cells — OV2 <c>0x2249</c> (cells <c>0x08..0x11</c> and <c>0x80..0x89</c>) and the end-of-combat cell
/// rewrite <c>0x2a26</c> (dungeon-representation.md §Fight cells [C], s39). <c>rand0_99</c> = <c>rng.Next(0, 100)</c>.
/// </summary>
public static class DungeonEncounters
{
    /// <summary><c>[0x1a5a]</c> := 7 on every dungeon entry (OV2 state 13, <c>0x82</c>).</summary>
    public const int ChanceOnEntry = 7;

    /// <summary>Level buffer byte <c>0x4e7</c> → <c>[0x64f0]</c> on entry (<c>0x1e32</c>): the level's monster tier T (0..5).</summary>
    public const int TierOffset = 0x4e7;

    /// <summary>Forced-fight table: cell <c>b</c> (<c>0x80..0x89</c>) fights monster <c>buf[0x468 + b]</c> (0-based).</summary>
    public const int ForcedFightTableBase = 0x468;

    /// <summary>DNG6 (<c>[0x64ec] == 6</c>): a wandering fight there is TROLKIN (0-based 33) when a second roll is &gt; 50.</summary>
    public const int TrolkinLevel = 6;
    public const int TrolkinMonsterId = 0x22 - 1;

    public static int Tier(DungeonLevel level) => level.GetBufferByte(TierOffset);

    /// <summary>Every encounter build (<c>0x3316</c>): chance − 1, then 1 once it is below 5 (7 → 6 → 5 → 1 → 1 …).</summary>
    public static int AfterBuild(int chance)
    {
        chance--;
        return chance < 5 ? 1 : chance;
    }

    /// <summary>
    /// Wandering cell <paramref name="cell"/> (<c>0x08..0x11</c>, OV2 <c>0x2259..0x22b6</c>): fight when
    /// <c>rand0_99 ≤ chance</c>; first monster (0-based) <c>rand0_99/33 + cell + T − 8</c>; in DNG6 a third roll
    /// <c>&gt; 50</c> makes it TROLKIN. Null = no fight (the cell then shows message <c>cell + 3</c>).
    /// </summary>
    /// <remarks>
    /// <paramref name="rules"/> (null = I): II (<c>encounters.md</c> §5, <c>0x10b60</c>) rolls 0..100, multiplies the tier
    /// by 12 (<c>r/33 + 12·T + b − 8</c>) and has no TROLKIN override.
    /// </remarks>
    public static int? RollWandering(DungeonLevel level, int levelId, byte cell, int chance, IRandomSource rng,
        EncounterRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(rng);
        rules ??= EncounterRules.PhantasieI;
        if (rules.Roll100(rng) > chance)
            return null;
        var monster = rules.Roll100(rng) / 33 + cell + rules.DungeonTierFactor * Tier(level) - 8;
        if (levelId == rules.WanderingOverrideLevel && rules.Roll100(rng) > 50)
            monster = rules.WanderingOverrideMonster;
        return monster;
    }

    /// <summary>Forced-fight cell <c>0x80..0x89</c> (<c>0x2383</c>): monster <c>buf[0x468 + b]</c>, no roll.</summary>
    public static int ForcedFightMonster(DungeonLevel level, byte cell) =>
        level.GetBufferByte(ForcedFightTableBase + cell);

    /// <summary>
    /// <c>0x2a26</c>: after a fight that kept its treasure flag <c>[0x74b4]</c>, the cell under the party loses its fight:
    /// <c>0x80..0x89</c> → <c>0x8a</c>, <c>0xb9..0xc7</c> → <c>0xb9</c>, <c>0xc8..0xd6</c> → <c>0xc8</c> (open doors).
    /// Wandering cells and every other byte are left alone.
    /// </summary>
    public static void ClearFightCell(DungeonLevel level, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!level.InBounds(x, y))
            return;
        var b = level.Get(x, y);
        if (b is >= 0x80 and < 0x8a)
            level.Set(x, y, 0x8a);
        else if (b is > 0xb8 and < 0xc8)
            level.Set(x, y, 0xb9);
        else if (b is > 0xc7 and < 0xd7)
            level.Set(x, y, 0xc8);
    }
}

/// <summary>
/// Dungeon traps — OV2 <c>0x2f8e(a, x, y)</c> [C] (s39). There is no difficulty table: the trap kind <c>a</c> (the cell byte
/// 2..6, or script op 16's argument) sets the avoid odds and the damage. Party loops run over every member in party
/// order and skip DEAD ones.
/// </summary>
public static class DungeonTraps
{
    /// <summary>The cell becomes plain floor after the trap is disarmed or sprung (<c>0x2a02(…, 7)</c>).</summary>
    public const byte SpentCell = 7;

    /// <summary>
    /// Spot roll (<c>0x2fa0</c>): each non-DEAD member rolls <c>rand0_99 ≤ spot trap @36</c>; the <b>last</b> one who
    /// succeeds is the spotter. Null = nobody (the trap springs). Every roll of this class takes the game's scale
    /// <paramref name="roll"/>: II (ST <c>trap_disarm 0x10db0</c> [C] s92) draws <c>rand / 327</c> (0…100) with I's tests.
    /// </summary>
    public static int? FindSpotter(Party party, IRandomSource rng, TerrainRoll roll = TerrainRoll.Percent) =>
        LastSuccess(party, rng, m => m.Skills.SpotTrap, inclusive: true, roll);

    /// <summary>Disarm roll (<c>0x3021</c>): <c>rand0_99 &gt; disarm trap @38</c> fails ("FAILS", then the trap springs).</summary>
    public static bool DisarmSucceeds(Character disarmer, IRandomSource rng, TerrainRoll roll = TerrainRoll.Percent) =>
        roll.Draw(rng) <= disarmer.Skills.DisarmTrap;

    /// <summary>
    /// "WHO WILL DISARM ?" (<c>0x5a0a</c> mode 1) lists only members whose status is OK (1): DEAD and SLEEP are not
    /// offered. No such member → the menu returns −1 (step back).
    /// </summary>
    public static bool CanDisarm(Character member) => member.Status == CharacterStatus.Ok;

    /// <summary>
    /// The trap springs (<c>0x306f</c>): MESS message <c>a + 4</c>, "IN THE TRAP:", then per non-DEAD member: an OK member
    /// avoids it when <c>22·rand0_99 &lt; 10·DEX·(a + 3)</c> (SLEEP members get no avoid roll); otherwise
    /// <c>d = 5·rand0_99·(a − 1)/100</c> through <c>0x124a</c> (floor 1, DEAD skipped, CON loss on death). The report
    /// says DIES, TAKES d DAMAGE, or AVOIDS THE TRAP when <c>d = 0</c> (1 HP is still lost) [C quirk].
    /// Returns true when no non-DEAD member is left (state 6).
    /// </summary>
    public static bool Spring(Party party, int a, IRandomSource rng, List<string> log, out int totalDamage,
        TerrainRoll roll = TerrainRoll.Percent)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);
        totalDamage = 0;
        log.Add(GameText.S("core.dungeon.in_the_trap"));
        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Dead)
                continue;
            if (m.Status == CharacterStatus.Ok && roll.Draw(rng) * 22 < m.Stats.Dex * (a + 3) * 10)
            {
                log.Add(GameText.F("core.dungeon.x_avoids_the_trap", m.Name));
                continue;
            }
            var d = roll.Draw(rng) * (a - 1) * 5 / 100;
            var hit = Math.Max(1, d); // 0x124a floor
            totalDamage += CombatResolver.ApplyPartyDamage(party, m, hit, DamageSource.Trap, rng, log, trapLevel: a);
            if (m.Status == CharacterStatus.Dead)
                log.Add(GameText.F("core.dungeon.trap_x_dies", m.Name));
            else if (d > 0)
                log.Add(GameText.F("core.dungeon.trap_x_takes_x_damage", m.Name, d));
            else
                log.Add(GameText.F("core.dungeon.x_avoids_the_trap", m.Name));
        }
        return party.Members.All(m => m.Status == CharacterStatus.Dead);
    }

    /// <summary>Last non-DEAD member (party order) whose <c>rand0_99</c> passes <paramref name="skill"/>.</summary>
    internal static int? LastSuccess(Party party, IRandomSource rng, Func<Character, int> skill, bool inclusive,
        TerrainRoll roll = TerrainRoll.Percent)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        int? last = null;
        for (var i = 0; i < party.Members.Count; i++)
        {
            var m = party.Members[i];
            if (m.Status == CharacterStatus.Dead)
                continue;
            var r = roll.Draw(rng);
            if (inclusive ? r <= skill(m) : r < skill(m))
                last = i;
        }
        return last;
    }
}

/// <summary>What a lock / ambush cell is (<c>0x2e03</c>): door kind <c>di = b − 0xb8</c>, minus 15 above 15.</summary>
public enum DungeonDoorKind
{
    /// <summary>Kind 1 (<c>0xb9</c>, <c>0xc8</c>): an open door; nothing happens.</summary>
    Open,

    /// <summary>Kinds 2..13: listen roll, then a monster ambush (or GO BACK).</summary>
    Ambush,

    /// <summary>Kind 14 (<c>0xc6</c>, <c>0xd5</c>): pick the lock.</summary>
    Locked,

    /// <summary>Kind 15 (<c>0xc7</c>, <c>0xd6</c>): "NO ONE CAN PICK THE LOCK", push back. A failed pick turns a kind-14 door into this.</summary>
    Jammed,
}

/// <summary>Dungeon locks and lock ambushes — OV2 <c>0x2e03(b, x, y)</c> [C] (s39).</summary>
public static class DungeonLocks
{
    /// <summary>Door kind <c>di</c> (1..15) and bank base <c>si</c> (0 for <c>0xb9..0xc7</c>, 15 for <c>0xc8..0xd6</c>).</summary>
    public static (int Kind, int Bank) Decode(byte cell)
    {
        var di = cell - 0xb8;
        var si = 0;
        if (di > 15)
        {
            si = 15;
            di -= 15;
        }
        return (di, si);
    }

    public static DungeonDoorKind Classify(byte cell) => Decode(cell).Kind switch
    {
        < 2 => DungeonDoorKind.Open,
        14 => DungeonDoorKind.Locked,
        > 14 => DungeonDoorKind.Jammed,
        _ => DungeonDoorKind.Ambush,
    };

    /// <summary>
    /// Pick roll (<c>0x2e2e</c>): last non-DEAD member with <c>rand0_99 &lt; pick lock @44</c>; null = nobody. II
    /// (ST <c>door_lock 0x111d6</c> [C] s92): the same test on <c>rand / 327</c> (<paramref name="roll"/>).
    /// </summary>
    public static int? FindPicker(Party party, IRandomSource rng, TerrainRoll roll = TerrainRoll.Percent) =>
        DungeonTraps.LastSuccess(party, rng, m => m.Skills.PickLock, inclusive: false, roll);

    /// <summary>Listen roll (<c>0x2ec6</c>): last non-DEAD member with <c>rand0_99 &lt; listen @40</c>; null = nobody heard.</summary>
    public static int? FindListener(Party party, IRandomSource rng, TerrainRoll roll = TerrainRoll.Percent) =>
        DungeonTraps.LastSuccess(party, rng, m => m.Skills.Listen, inclusive: false, roll);

    /// <summary>Picked: the cell becomes the bank's open door <c>0xb9 + si</c>.</summary>
    public static byte OpenedCell(byte cell) => (byte)(0xb9 + Decode(cell).Bank);

    /// <summary>Not picked (or jammed): the cell becomes kind 15, <c>0xc7 + si</c> — it can never be picked in this buffer.</summary>
    public static byte JammedCell(byte cell) => (byte)(0xc7 + Decode(cell).Bank);

    /// <summary>Ambush monster (0-based): <c>[0x64e6] = 12·T + di − 1</c> (<c>0x2f15</c>), T = level tier.</summary>
    public static int AmbushMonster(DungeonLevel level, byte cell) =>
        12 * DungeonEncounters.Tier(level) + Decode(cell).Kind - 2;
}

/// <summary>
/// Swimming — OV2 state 10, <c>0x685c</c> [C] (s39): every wilderness step onto a water tile (<c>tile / 10 == 10</c>,
/// <c>0x6156</c>; no random-encounter roll there). "SWIMMING:", then per non-DEAD member: <c>rand0_99 &lt; swim @48</c>
/// swims; otherwise <c>d = (5·rand0_99 + 100)/100</c> (1..5) through <c>0x124a</c> → "SINKS AND TAKES d DAMAGE" or
/// "DIES" (CON loss on death). Nobody left → state 6 (party defeat); else <c>[0x64fe] := 1</c> (write-only, no reader found) and state 2.
/// </summary>
public static class Swimming
{
    /// <param name="roll">The game's roll scale: I's <c>rand0_99</c> (<see cref="TerrainRoll.Percent"/>), or II's
    /// <c>rand / 327</c> (0…100, <c>0xfac2(1)</c> [C] s83), which also lets the damage reach 6.</param>
    public static bool Resolve(Party party, IRandomSource rng, List<string> log, TerrainRoll roll = TerrainRoll.Percent)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);
        log.Add(GameText.S("core.dungeon.swimming"));
        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Dead)
                continue;
            if (roll.Draw(rng) < m.Skills.Swim)
            {
                log.Add(GameText.F("core.dungeon.x_swims", m.Name));
                continue;
            }
            var d = (roll.Draw(rng) * 5 + 100) / 100;
            CombatResolver.ApplyPartyDamage(party, m, d, DamageSource.Swimming, rng, log);
            log.Add(m.Status == CharacterStatus.Dead ? GameText.F("core.dungeon.swim_x_dies", m.Name) : GameText.F("core.dungeon.x_sinks_and_takes_x_damage", m.Name, d));
        }
        return party.Members.All(m => m.Status == CharacterStatus.Dead);
    }
}
