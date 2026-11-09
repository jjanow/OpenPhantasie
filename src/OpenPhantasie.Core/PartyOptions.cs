namespace OpenPhantasie.Core;

/// <summary>PARTY OPTIONS menu in DOS order (menu <c>0x18</c>, labels at COM DS <c>0xd4ff</c>); value = what the menu returns.</summary>
public enum PartyOption
{
    BegMercy = 1,
    Threaten = 2,
    Greetings = 3,
    Fight = 4,
    Flee = 5,
}

/// <summary>Morale check at the top of the round (OV2 <c>0x3b98</c>).</summary>
public enum MoraleOutcome
{
    /// <summary>Monsters stay; show PARTY OPTIONS.</summary>
    Stand,

    /// <summary>"MONSTERS RUN" and they get away: combat over, rewards paid.</summary>
    Ran,

    /// <summary>"MONSTERS RUN" but they don't get away: every monster loses this round, THREATEN factor 15 → 45.</summary>
    RunFailed,

    /// <summary>A spell cast last round ended the fight (<c>[0x74b0]</c>, checked before the morale roll at <c>0x3b84</c>).</summary>
    EndedBySpell,
}

public enum PartyOptionOutcome
{
    /// <summary>Give orders as usual (FIGHT, or a refused BEG/THREATEN/GREET).</summary>
    GiveOrders,

    /// <summary>FLEE failed: every member's order is "none" (4); resolve the round without orders.</summary>
    LoseRound,

    /// <summary>The encounter is over; <see cref="PartyOptionResult.End"/> holds the rewards.</summary>
    CombatOver,
}

public sealed class PartyOptionResult
{
    public required PartyOptionOutcome Outcome { get; init; }
    public CombatResult? End { get; init; }

    /// <summary>BEG: the party's found gold (<c>[0x63f6]</c>) is lost and found-item slots 0, 2, …, 48 are cleared.</summary>
    public bool ForfeitsSack { get; init; }

    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Per-round party options, OV2 <c>0x3b70</c> (<c>combat-resolution.md</c> §Party options). Each round starts with
/// <see cref="MoraleCheck"/>, then the player picks a <see cref="PartyOption"/>. Every way out of combat here goes
/// through the normal end of combat (<c>0x3f29</c>), so the XP earned from kills so far is always paid.
/// </summary>
public static class PartyOptions
{
    /// <summary>THREATEN factor <c>[0xc432]</c>: 15 at build (<c>0x332e</c>), 45 after a failed monster run (<c>0x3e13</c>).</summary>
    public const int ThreatenFactorStart = 15;
    public const int ThreatenFactorAfterFailedRun = 45;

    /// <param name="terrainClass">Last overworld tile / 10, <c>[0x649a]</c>; also used in dungeons (stale value).</param>
    public static MoraleOutcome MoraleCheck(CombatEncounter encounter, IRandomSource rng, int terrainClass,
        List<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);

        var living = encounter.AllMonsters.Count(m => m.IsAlive);
        if (living == 0)
            return MoraleOutcome.Stand;

        // XP·(rand + 100) / XP₀ ≥ (rand + 200)·living  (0x3b98..0x3bdb)
        var lhs = encounter.RewardXp * (Roll0To99(rng) + 100) / LeadXp(encounter);
        var rhs = (long)(Roll0To99(rng) + 200) * living;
        if (lhs < rhs)
            return MoraleOutcome.Stand;

        log?.Add(GameText.S("core.combat.monsters_run"));
        var penalty = Math.Abs(10 * (1 - terrainClass)); // 0x3de4
        if (Roll0To99(rng) < 80 - penalty)
            return MoraleOutcome.Ran;

        // 0x3e13: every living monster gets state 2 (done this round). In DOS that also overwrites state 1, so a failed
        // run wakes sleepers [C]; not applied to D-020 SLEEP (Asleep flag kept) — manual↔DOS question pending (s33).
        if (encounter.Rules.IsDos(RuleOptionRegistry.FailedRun)) // D-030 dos: state 2 overwrites asleep too
            foreach (var mon in encounter.AllMonsters.Where(m => m.IsAlive))
                mon.StatusFlags &= ~MonsterStatusFlags.Asleep;
        encounter.ThreatenFactor = ThreatenFactorAfterFailedRun;
        encounter.MonstersLoseRound = true;
        return MoraleOutcome.RunFailed;
    }

    /// <summary>Ending after <see cref="MoraleOutcome.Ran"/>: full gold, kill XP.</summary>
    public static CombatResult MonstersRan(CombatEncounter encounter) =>
        EndCombat(encounter, encounter.RewardGold, new List<string>());

    /// <param name="inDungeon"><c>[0x64e8] == 3</c>; else wilderness (<c>2</c>).</param>
    /// <param name="terrainClass">See <see cref="MoraleCheck"/>.</param>
    /// <param name="averagePartyLevel"><c>[0x63ec]</c> as last written at town Exit (stale during the trip, s41);
    /// null = compute it from the party now.</param>
    public static PartyOptionResult Choose(CombatEncounter encounter, PartyOption option, IRandomSource rng,
        bool inDungeon, int terrainClass, int? averagePartyLevel = null)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);
        var log = new List<string>();

        switch (option)
        {
            case PartyOption.BegMercy:
                // DOS (0x3bea) rolls rand < Σreaction + 90 and fights on a miss; D-017 (manual): begging always works.
                if (encounter.Rules.IsDos(RuleOptionRegistry.BegMercy) && Roll0To99(rng) >= encounter.ReactionSum + 90)
                    return Refused(log); // `core.combat.monsters_dont_respond` → orders (0x6a2a)
                log.Add(GameText.S("core.combat.monsters_take_gold"));
                encounter.TreasureForfeited = true;
                return Over(EndCombat(encounter, 0, log), forfeitsSack: true);

            case PartyOption.Threaten:
            {
                // rand ≤ [0xc432]·XP/XP₀ + 3·[0x63ec] − 3·L  (0x3c5b); [0x63ec] is the town-Exit average (s41)
                var odds = encounter.ThreatenFactor * encounter.RewardXp / LeadXp(encounter)
                           + 3L * (averagePartyLevel ?? AveragePartyLevel(encounter.Party)) - 3L * encounter.MonsterLevel;
                if (Roll0To99(rng) > odds)
                    return Refused(log);
                log.Add(GameText.S("core.combat.monsters_surrender"));
                encounter.SurrenderTreasure = inDungeon; // [0x74b2]: dungeon surrender hands over the treasure
                return Over(EndCombat(encounter, encounter.RewardGold, log));
            }

            case PartyOption.Greetings:
                // Never once a monster has been damaged ([0x74b6], set by 0x1597); else rand < Σreaction + 10.
                if (encounter.MonsterDamaged || Roll0To99(rng) >= encounter.ReactionSum + 10)
                    return Refused(log);
                log.Add(GameText.S("core.combat.monsters_greet_you"));
                encounter.TreasureForfeited = true;
                return Over(EndCombat(encounter, 0, log));

            case PartyOption.Fight:
                return new PartyOptionResult { Outcome = PartyOptionOutcome.GiveOrders, Log = log };

            case PartyOption.Flee:
            {
                // Wilderness 10·(1 − terrain class), dungeon 5·party size; no abs here (0x3d27).
                var penalty = inDungeon ? 5 * encounter.Party.Members.Count : 10 * (1 - terrainClass);
                if (Roll0To99(rng) < 80 - penalty)
                {
                    encounter.TreasureForfeited = true;
                    return Over(EndCombat(encounter, 0, log));
                }

                // 0x3d84: every order := 4, surprise cleared, monster states reset by 0x33c9 (undoes a failed run).
                encounter.EnsurePrep();
                foreach (var prep in encounter.Prep)
                    prep.PackedAction = CombatAction.Cleared;
                encounter.MonstersLoseRound = false;
                encounter.MonstersFrozen = false; // [0x64a0] := 0 (0x3d9f): no pass-1 wait in a first round
                encounter.OrdersSkipped = true;   // round (5) without the orders phase 0x6a2a: DOS's living list stays
                return new PartyOptionResult { Outcome = PartyOptionOutcome.LoseRound, Log = log };
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(option));
        }
    }

    /// <summary>
    /// <c>[0x63ec]</c> (OV1 <c>0x3269</c>, party set-up on leaving town): Σ level over every party slot, DEAD included,
    /// divided by the party size (truncated). Set when the party size is 6 at most, so a summoned elemental never
    /// counts (s33): only <see cref="Party.Adventurers"/> are averaged.
    /// </summary>
    public static int AveragePartyLevel(Party party)
    {
        var members = party.Adventurers.ToList();
        return members.Count == 0 ? 0 : members.Sum(m => m.Level) / members.Count;
    }

    /// <summary>Group 0's per-kill XP (<c>[0x753c]</c>, build order; re-ranking only moves the order table).</summary>
    private static long LeadXp(CombatEncounter encounter)
    {
        var xp = encounter.Ranks.FirstOrDefault(r => r.RankIndex == 0)?.Members.FirstOrDefault()?.ExperienceValue ?? 0;
        return xp > 0 ? xp : 1; // every record has XP ≥ 50; guard only
    }

    private static PartyOptionResult Refused(List<string> log)
    {
        log.Add(GameText.S("core.combat.monsters_dont_respond"));
        return new PartyOptionResult { Outcome = PartyOptionOutcome.GiveOrders, Log = log };
    }

    private static PartyOptionResult Over(CombatResult end, bool forfeitsSack = false) => new()
    {
        Outcome = PartyOptionOutcome.CombatOver,
        End = end,
        ForfeitsSack = forfeitsSack,
        Log = end.Log,
    };

    /// <summary>End of combat with monsters still alive (<c>0x3f29</c>: no "MONSTERS KILLED").</summary>
    private static CombatResult EndCombat(CombatEncounter encounter, long gold, List<string> log)
    {
        if (!encounter.Rules.SurvivorShares) // D-053 dos: DOS prints only the shares
            log.Add(GameText.F("core.combat.x_xp_and_x_gp", encounter.RewardXp, gold));
        return new CombatResult
        {
            EncounterOver = true,
            PartyWon = false,
            RewardXp = encounter.RewardXp,
            RewardGold = gold,
            Log = log,
        };
    }

    private static int Roll0To99(IRandomSource rng) => rng.Next(0, 100);
}
